import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { ChatIncomingMessage } from '../../../core/models/chat';
import { ChatService } from '../../../core/services/chat-service/chat.service';
import { ChatNotificationSound } from '../../utils/chat-notification-sound';
import { IconComponent } from '../icon/icon.component';

interface ChatToast {
  conversationId: number;
  name: string;
  preview: string;
  count: number;
}

const TOAST_DURATION_MS = 12_000;

/**
 * Avisa de un mensaje nuevo del chat desde cualquier pantalla de la aplicación:
 * sonido, aviso emergente (salvo que ya se esté mirando la bandeja) y contador en el
 * título de la pestaña. También mantiene abierto el canal en tiempo real que alimenta
 * el contador del menú.
 */
@Component({
  selector: 'app-chat-notifier',
  standalone: true,
  imports: [CommonModule, IconComponent],
  templateUrl: './chat-notifier.component.html',
  styleUrls: ['./chat-notifier.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChatNotifierComponent implements OnInit, OnDestroy {
  private readonly chat = inject(ChatService);
  private readonly router = inject(Router);

  readonly toast = signal<ChatToast | null>(null);

  private readonly subscription = new Subscription();
  private readonly baseTitle = document.title;
  private hideTimer?: ReturnType<typeof setTimeout>;

  constructor() {
    effect(() => {
      const pending = this.chat.summary()?.awaitingReplyCount ?? 0;
      document.title = pending > 0 ? `(${pending}) ${this.baseTitle}` : this.baseTitle;
    });
  }

  ngOnInit(): void {
    void this.chat.connect();
    this.subscription.add(this.chat.messageReceived$.subscribe(payload => this.onMessage(payload)));
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
    if (this.hideTimer) clearTimeout(this.hideTimer);
    document.title = this.baseTitle;
    this.chat.disconnect();
  }

  open(toast: ChatToast): void {
    this.dismiss();
    void this.router.navigate(['/chat'], { queryParams: { c: toast.conversationId } });
  }

  dismiss(): void {
    if (this.hideTimer) clearTimeout(this.hideTimer);
    this.toast.set(null);
  }

  private onMessage(payload: ChatIncomingMessage): void {
    if (payload.message.senderType !== 'visitor') return;

    ChatNotificationSound.play();

    // Con la bandeja a la vista el mensaje ya se ve en la lista: el aviso sería ruido.
    const watching = this.router.url.startsWith('/chat') && document.visibilityState === 'visible';
    if (watching) return;

    const current = this.toast();
    const sameConversation = current?.conversationId === payload.conversation.id;
    this.toast.set({
      conversationId: payload.conversation.id,
      name: payload.conversation.visitorName,
      preview: payload.message.body,
      count: sameConversation ? current.count + 1 : 1
    });

    if (this.hideTimer) clearTimeout(this.hideTimer);
    this.hideTimer = setTimeout(() => this.toast.set(null), TOAST_DURATION_MS);
  }
}
