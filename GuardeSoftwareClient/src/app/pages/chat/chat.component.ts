import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  OnInit,
  ViewChild,
  computed,
  effect,
  inject,
  signal,
  untracked
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, Subscription, debounceTime, distinctUntilChanged } from 'rxjs';
import {
  ChatBusinessHours,
  ChatConversationSummary,
  ChatEmailStatus,
  ChatIncomingMessage,
  ChatMessage
} from '../../core/models/chat';
import { AuthService } from '../../core/services/auth-service/auth.service';
import { ChatService } from '../../core/services/chat-service/chat.service';
import { ChatNotificationSound } from '../../shared/utils/chat-notification-sound';
import { IconComponent } from '../../shared/components/icon/icon.component';
import { ChatHoursModalComponent } from './chat-hours-modal/chat-hours-modal.component';

type ChatTab = 'open' | 'closed';

const MAX_REPLY_LENGTH = 2000;

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [CommonModule, IconComponent, ChatHoursModalComponent],
  templateUrl: './chat.component.html',
  styleUrls: ['./chat.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChatComponent implements OnInit, OnDestroy {
  private readonly chat = inject(ChatService);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly maxReplyLength = MAX_REPLY_LENGTH;

  readonly tab = signal<ChatTab>('open');
  readonly search = signal('');
  readonly conversations = signal<ChatConversationSummary[]>([]);
  readonly loadingList = signal(true);
  readonly listError = signal(false);

  readonly selected = signal<ChatConversationSummary | null>(null);
  readonly messages = signal<ChatMessage[]>([]);
  readonly loadingThread = signal(false);
  readonly threadError = signal(false);

  readonly draft = signal('');
  readonly sending = signal(false);
  readonly sendError = signal<string | null>(null);
  readonly actionPending = signal(false);

  readonly hours = signal<ChatBusinessHours | null>(null);
  readonly showHours = signal(false);
  readonly soundMuted = signal(ChatNotificationSound.isMuted());

  /** Se actualiza cada 10 s para que los relojes de espera avancen solos. */
  readonly now = signal(Date.now());

  readonly canWrite: boolean;
  readonly isAdmin: boolean;

  readonly summary = this.chat.summary;
  readonly awaitingCount = computed(() => this.summary()?.awaitingReplyCount ?? 0);
  readonly longestWaitMinutes = computed(() => {
    const since = this.summary()?.oldestAwaitingSince;
    return since ? Math.max(0, Math.floor((this.now() - Date.parse(since)) / 60_000)) : null;
  });
  readonly isOpenNow = computed(() => this.summary()?.isOpenNow ?? this.hours()?.status.isOpen ?? false);
  readonly responseTimeMinutes = computed(() => this.summary()?.responseTimeMinutes ?? 2);
  readonly draftLength = computed(() => this.draft().length);
  readonly canSend = computed(() =>
    this.draft().trim().length > 0 && this.draftLength() <= MAX_REPLY_LENGTH && !this.sending());

  /** La espera más larga pasó el plazo prometido y hay alguien para atender. */
  readonly isWaitOverdue = computed(() =>
    this.isOpenNow() && (this.longestWaitMinutes() ?? 0) >= this.responseTimeMinutes());
  readonly longestWaitLabel = computed(() => {
    const minutes = this.longestWaitMinutes();
    if (minutes === null) return '—';
    if (minutes < 1) return '< 1 min';
    if (minutes < 60) return `${minutes} min`;
    const hours = Math.floor(minutes / 60);
    return hours < 24 ? `${hours} h` : `${Math.floor(hours / 24)} d`;
  });
  readonly statusHint = computed(() => {
    const status = this.hours()?.status;
    if (!status) return '';
    return this.isOpenNow()
      ? `Atiende hasta ${status.closesAtLabel ?? 'el cierre'}`
      : status.nextOpenLabel ? `Abre ${status.nextOpenLabel}` : 'Sin horario de atención';
  });

  /** Mensajes con el rótulo del día cuando cambia ("Hoy", "Ayer", dd/MM/yyyy). */
  readonly timeline = computed(() => {
    let lastDay = '';
    return this.messages().map(message => {
      const day = new Date(message.createdAt).toDateString();
      const label = day === lastDay ? null : this.dayLabel(message.createdAt);
      lastDay = day;
      return { message, day: label };
    });
  });

  @ViewChild('threadScroll') private threadScroll?: ElementRef<HTMLElement>;
  @ViewChild('composer') private composer?: ElementRef<HTMLTextAreaElement>;

  private readonly subscriptions = new Subscription();
  private readonly searchInput$ = new Subject<string>();
  private tickTimer?: ReturnType<typeof setInterval>;
  private stickToBottom = true;
  private listRequest?: Subscription;
  private threadRequest?: Subscription;
  private readonly visibilityHandler = (): void => {
    if (document.visibilityState === 'visible') this.markSelectedRead();
  };

  constructor() {
    this.canWrite = !this.auth.isObserver();
    this.isAdmin = this.auth.isAdmin();

    // Al llegar mensajes nuevos, bajar sólo si la persona ya estaba leyendo lo último.
    effect(() => {
      this.messages();
      queueMicrotask(() => this.scrollToBottom());
    });

    // Si el chat abre o cierra (o se cambió el horario), los rótulos "abre mañana a las…" se piden de nuevo ya.
    effect(() => {
      const open = this.summary()?.isOpenNow;
      const current = untracked(() => this.hours());
      if (open !== undefined && current && current.status.isOpen !== open) this.loadHours();
    });
  }

  ngOnInit(): void {
    void this.chat.connect();
    this.loadList();
    this.loadHours();

    this.subscriptions.add(this.searchInput$.pipe(debounceTime(300), distinctUntilChanged())
      .subscribe(() => this.loadList()));
    // También reacciona al enlace del aviso emergente cuando la bandeja ya está abierta.
    this.subscriptions.add(this.route.queryParamMap.subscribe(() => this.openFromQuery()));
    this.subscriptions.add(this.chat.messageReceived$.subscribe(payload => this.onIncoming(payload)));
    this.subscriptions.add(this.chat.conversationChanged$.subscribe(conversation => this.onConversationChanged(conversation)));
    this.subscriptions.add(this.chat.presenceChanged$.subscribe(change => this.onPresence(change.publicId, change.online)));
    this.subscriptions.add(this.chat.reconnected$.subscribe(() => {
      this.loadList(true);
      const current = this.selected();
      if (current) this.openConversation(current.id, true);
    }));

    let ticks = 0;
    this.tickTimer = setInterval(() => {
      this.now.set(Date.now());
      // El rótulo "abre el lunes a las 9:30" cambia con la hora: se vuelve a pedir cada minuto.
      if (++ticks % 6 === 0) this.loadHours();
    }, 10_000);
    document.addEventListener('visibilitychange', this.visibilityHandler);
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    this.listRequest?.unsubscribe();
    this.threadRequest?.unsubscribe();
    if (this.tickTimer) clearInterval(this.tickTimer);
    document.removeEventListener('visibilitychange', this.visibilityHandler);
  }

  // ------------------------------------------------------------------- lista

  setTab(tab: ChatTab): void {
    if (this.tab() === tab) return;
    this.tab.set(tab);
    this.loadList();
  }

  onSearch(value: string): void {
    this.search.set(value);
    this.searchInput$.next(value.trim());
  }

  clearSearch(): void {
    this.search.set('');
    this.searchInput$.next('');
  }

  loadList(silent = false): void {
    if (!silent) this.loadingList.set(true);
    this.listError.set(false);
    this.listRequest?.unsubscribe();
    this.listRequest = this.chat.getConversations(this.tab(), this.search().trim()).subscribe({
      next: items => {
        this.conversations.set(items);
        this.loadingList.set(false);
      },
      error: () => {
        this.loadingList.set(false);
        this.listError.set(true);
      }
    });
  }

  /** /chat?c=ID abre esa conversación (enlace desde el aviso emergente). */
  private openFromQuery(): void {
    const id = Number(this.route.snapshot.queryParamMap.get('c'));
    if (!Number.isInteger(id) || id <= 0 || this.selected()?.id === id) return;

    this.openConversation(id);
    void this.router.navigate([], { queryParams: { c: null }, queryParamsHandling: 'merge', replaceUrl: true });
  }

  private loadHours(): void {
    this.chat.getBusinessHours().subscribe({
      next: hours => this.hours.set(hours),
      error: () => undefined
    });
  }

  // ------------------------------------------------------------ conversación

  openConversation(conversationId: number, silent = false): void {
    if (!silent) {
      this.loadingThread.set(true);
      this.messages.set([]);
      this.sendError.set(null);
    }
    this.threadError.set(false);
    this.stickToBottom = true;
    this.threadRequest?.unsubscribe();
    this.threadRequest = this.chat.getConversation(conversationId).subscribe({
      next: detail => {
        this.selected.set(detail.conversation);
        this.messages.set(detail.messages);
        this.loadingThread.set(false);
        this.upsert(detail.conversation);
        this.markSelectedRead();
        queueMicrotask(() => this.composer?.nativeElement.focus({ preventScroll: true }));
      },
      error: () => {
        this.loadingThread.set(false);
        this.threadError.set(true);
      }
    });
  }

  backToList(): void {
    this.selected.set(null);
    this.messages.set([]);
  }

  private markSelectedRead(): void {
    const current = this.selected();
    const last = this.messages().at(-1);
    if (!current || !last || !this.canWrite || document.visibilityState !== 'visible') return;
    if (current.unreadCount === 0) return;

    this.chat.markRead(current.id, last.id).subscribe({ error: () => undefined });
  }

  closeConversation(): void {
    const current = this.selected();
    if (!current || this.actionPending()) return;
    this.runAction(this.chat.close(current.id));
  }

  reopenConversation(): void {
    const current = this.selected();
    if (!current || this.actionPending()) return;
    this.runAction(this.chat.reopen(current.id));
  }

  private runAction(request: ReturnType<ChatService['close']>): void {
    this.actionPending.set(true);
    request.subscribe({
      next: conversation => {
        this.actionPending.set(false);
        this.selected.set(conversation);
        this.upsert(conversation);
      },
      error: error => {
        this.actionPending.set(false);
        this.sendError.set(error?.error?.message ?? 'No se pudo completar la acción.');
      }
    });
  }

  // ---------------------------------------------------------------- responder

  onDraft(value: string, element: HTMLTextAreaElement): void {
    this.draft.set(value);
    this.sendError.set(null);
    element.style.height = 'auto';
    element.style.height = `${Math.min(element.scrollHeight, 160)}px`;
  }

  onComposerKey(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
      event.preventDefault();
      this.send();
    }
  }

  send(): void {
    const current = this.selected();
    if (!current || !this.canSend() || !this.canWrite) return;

    const body = this.draft().trim();
    this.sending.set(true);
    this.sendError.set(null);
    this.chat.reply(current.id, body).subscribe({
      next: result => {
        this.sending.set(false);
        this.draft.set('');
        if (this.composer) this.composer.nativeElement.style.height = 'auto';
        this.stickToBottom = true;
        this.appendMessage(result.message);
        this.selected.set(result.conversation);
        this.upsert(result.conversation);
        this.composer?.nativeElement.focus({ preventScroll: true });
      },
      error: error => {
        this.sending.set(false);
        this.sendError.set(error?.error?.message ?? 'No se pudo enviar la respuesta. Intentá de nuevo.');
      }
    });
  }

  // ------------------------------------------------------------ tiempo real

  private onIncoming(payload: ChatIncomingMessage): void {
    this.upsert(payload.conversation);

    if (this.selected()?.id !== payload.conversation.id) return;

    this.selected.set(payload.conversation);
    this.appendMessage(payload.message);
    if (payload.message.senderType === 'visitor') this.markSelectedRead();
  }

  private onConversationChanged(conversation: ChatConversationSummary): void {
    this.upsert(conversation);
    if (this.selected()?.id === conversation.id) this.selected.set(conversation);
  }

  private onPresence(publicId: string, online: boolean): void {
    this.conversations.update(items => items.map(item =>
      item.publicId === publicId ? { ...item, visitorOnline: online } : item));
    const current = this.selected();
    if (current?.publicId === publicId) this.selected.set({ ...current, visitorOnline: online });
  }

  private appendMessage(message: ChatMessage): void {
    this.messages.update(items => {
      const index = items.findIndex(item => item.id === message.id);
      if (index < 0) return [...items, message];

      // El eco en tiempo real llega sin el estado del email; la respuesta HTTP sí lo trae.
      if (message.emailStatus === null || items[index].emailStatus === message.emailStatus) return items;
      return items.map((item, position) => position === index ? { ...item, emailStatus: message.emailStatus } : item);
    });
  }

  private upsert(conversation: ChatConversationSummary): void {
    const belongs = this.tab() === 'open' ? conversation.status === 'open' : conversation.status === 'closed';
    const others = this.conversations().filter(item => item.id !== conversation.id);
    this.conversations.set(belongs && this.matchesSearch(conversation)
      ? this.sort([...others, conversation])
      : others);
  }

  private matchesSearch(conversation: ChatConversationSummary): boolean {
    const term = this.search().trim().toLowerCase();
    if (!term) return true;
    return [conversation.visitorName, conversation.visitorEmail, conversation.visitorPhone]
      .some(value => value?.toLowerCase().includes(term));
  }

  /** Mismo orden que el servidor: sin responder primero, la espera más larga arriba. */
  private sort(items: ChatConversationSummary[]): ChatConversationSummary[] {
    const time = (value: string | null) => (value ? Date.parse(value) : 0);
    return [...items].sort((a, b) => {
      if (a.awaitingReply !== b.awaitingReply) return a.awaitingReply ? -1 : 1;
      if (a.awaitingReply) {
        const wait = time(a.awaitingReplySince) - time(b.awaitingReplySince);
        if (wait !== 0) return wait;
      }
      return time(b.lastMessageAt) - time(a.lastMessageAt) || b.id - a.id;
    });
  }

  // -------------------------------------------------------------- presentación

  onThreadScroll(element: HTMLElement): void {
    this.stickToBottom = element.scrollHeight - element.scrollTop - element.clientHeight < 80;
  }

  private scrollToBottom(): void {
    const element = this.threadScroll?.nativeElement;
    if (element && this.stickToBottom) element.scrollTop = element.scrollHeight;
  }

  waitMinutes(conversation: ChatConversationSummary): number | null {
    if (!conversation.awaitingReply || !conversation.awaitingReplySince) return null;
    return Math.max(0, Math.floor((this.now() - Date.parse(conversation.awaitingReplySince)) / 60_000));
  }

  /** Pasó el plazo prometido. Lo escrito fuera de horario se contesta al abrir: no corre el reloj. */
  isOverdue(conversation: ChatConversationSummary): boolean {
    const minutes = this.waitMinutes(conversation);
    return minutes !== null && !conversation.awaitingOutOfHours && minutes >= this.responseTimeMinutes();
  }

  waitLabel(conversation: ChatConversationSummary): string {
    const minutes = this.waitMinutes(conversation);
    if (minutes === null) return '';
    if (minutes < 1) return 'Recién llegó';
    if (minutes < 60) return `Esperando ${minutes} min`;
    const hours = Math.floor(minutes / 60);
    return hours < 24 ? `Esperando ${hours} h` : `Esperando ${Math.floor(hours / 24)} d`;
  }

  /** Hoy: la hora. Otro día: dd/MM. */
  listTime(iso: string): string {
    const date = new Date(iso);
    return date.toDateString() === new Date(this.now()).toDateString()
      ? this.messageTime(iso)
      : date.toLocaleDateString('es-AR', { day: '2-digit', month: '2-digit' });
  }

  messageTime(iso: string): string {
    return new Date(iso).toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit', hour12: false });
  }

  private dayLabel(iso: string): string {
    const date = new Date(iso);
    const today = new Date(this.now());
    const yesterday = new Date(today);
    yesterday.setDate(today.getDate() - 1);

    if (date.toDateString() === today.toDateString()) return 'Hoy';
    if (date.toDateString() === yesterday.toDateString()) return 'Ayer';
    return date.toLocaleDateString('es-AR', { day: '2-digit', month: '2-digit', year: 'numeric' });
  }

  initials(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    return ((parts[0]?.[0] ?? '?') + (parts.length > 1 ? parts[parts.length - 1][0] : '')).toUpperCase();
  }

  telHref(phone: string): string {
    return `tel:${phone.replace(/[^\d+]/g, '')}`;
  }

  /** Muestra solo la ruta de la página de origen: el dominio ya es el de la web. */
  sourceLabel(url: string): string {
    try {
      const parsed = new URL(url);
      return `${parsed.hostname.replace(/^www\./, '')}${parsed.pathname === '/' ? '' : parsed.pathname}`;
    } catch {
      return url;
    }
  }

  safeHref(url: string): string | null {
    return /^https?:\/\//i.test(url) ? url : null;
  }

  emailNote(message: ChatMessage, conversation: ChatConversationSummary): { text: string; tone: 'ok' | 'warn' | 'bad' } | null {
    const status: ChatEmailStatus | null = message.emailStatus;
    switch (status) {
      case 'sent': return { text: `También se envió por email a ${conversation.visitorEmail ?? 'su correo'}`, tone: 'ok' };
      case 'failed': return { text: 'No se pudo enviar el email. Si ya se fue de la web, no verá la respuesta', tone: 'bad' };
      case 'no_email': return { text: 'No dejó email: si ya se fue de la web, no verá la respuesta. Llamalo si dejó teléfono', tone: 'warn' };
      case 'limit': return { text: 'Se alcanzó el tope diario de emails para esta conversación', tone: 'warn' };
      default: return null;
    }
  }

  toggleSound(): void {
    const muted = !this.soundMuted();
    ChatNotificationSound.setMuted(muted);
    this.soundMuted.set(muted);
    if (!muted) ChatNotificationSound.play();
  }

  onHoursSaved(hours: ChatBusinessHours): void {
    this.hours.set(hours);
    this.chat.refreshSummary();
    this.showHours.set(false);
  }

  trackConversation = (_: number, item: ChatConversationSummary): number => item.id;
  trackMessage = (_: number, item: ChatMessage): number => item.id;
}
