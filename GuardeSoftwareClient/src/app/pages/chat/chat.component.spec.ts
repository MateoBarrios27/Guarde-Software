import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Subject, of } from 'rxjs';
import {
  ChatConversationSummary,
  ChatIncomingMessage,
  ChatInboxSummary,
  ChatMessage,
  ChatPresenceChange,
  ChatReplyResult
} from '../../core/models/chat';
import { AuthService } from '../../core/services/auth-service/auth.service';
import { ChatService } from '../../core/services/chat-service/chat.service';
import { ChatComponent } from './chat.component';

const BASE = '2026-10-26T13:00:00.000Z';
const minutesAfter = (minutes: number) => new Date(Date.parse(BASE) + minutes * 60_000).toISOString();

function conversation(id: number, overrides: Partial<ChatConversationSummary> = {}): ChatConversationSummary {
  return {
    id, publicId: `00000000-0000-0000-0000-${String(id).padStart(12, '0')}`, visitorName: `Visitante ${id}`,
    visitorEmail: `v${id}@example.com`, visitorPhone: null, sourceUrl: null, status: 'open',
    createdAt: BASE, lastMessageAt: BASE, lastMessagePreview: 'Hola', lastMessageSender: 'visitor',
    awaitingReply: false, awaitingReplySince: null, awaitingOutOfHours: false, unreadCount: 0,
    visitorOnline: false, assignedUserName: null, ...overrides
  };
}

function message(id: number, overrides: Partial<ChatMessage> = {}): ChatMessage {
  return {
    id, senderType: 'visitor', senderName: 'Visitante', body: `mensaje ${id}`, createdAt: BASE,
    receivedOutOfHours: false, emailStatus: null, ...overrides
  };
}

class FakeChatService {
  readonly summary = signal<ChatInboxSummary | null>({
    awaitingReplyCount: 0, unreadConversationCount: 0, oldestAwaitingSince: null, isOpenNow: true, responseTimeMinutes: 2
  });
  readonly connected = signal(true);
  readonly messageReceived$ = new Subject<ChatIncomingMessage>();
  readonly conversationChanged$ = new Subject<ChatConversationSummary>();
  readonly presenceChanged$ = new Subject<ChatPresenceChange>();
  readonly reconnected$ = new Subject<void>();

  conversations: ChatConversationSummary[] = [];
  thread: ChatMessage[] = [];
  replies = new Subject<ChatReplyResult>();

  connect = jasmine.createSpy('connect').and.resolveTo();
  refreshSummary = jasmine.createSpy('refreshSummary');
  getConversations = jasmine.createSpy('getConversations').and.callFake(() => of(this.conversations));
  getConversation = jasmine.createSpy('getConversation').and.callFake((id: number) =>
    of({ conversation: this.conversations.find(item => item.id === id)!, messages: this.thread }));
  markRead = jasmine.createSpy('markRead').and.returnValue(of(undefined));
  reply = jasmine.createSpy('reply').and.callFake(() => this.replies);
  close = jasmine.createSpy('close');
  reopen = jasmine.createSpy('reopen');
  getBusinessHours = jasmine.createSpy('getBusinessHours').and.returnValue(of({
    days: [], status: { isOpen: true, responseTimeMinutes: 2, closesAtLabel: '15:30 hs', nextOpenLabel: null, scheduleLabel: '' }
  }));
}

describe('ChatComponent', () => {
  let chat: FakeChatService;
  let fixture: ComponentFixture<ChatComponent>;
  let component: ChatComponent;

  function create(observer = false): void {
    TestBed.configureTestingModule({
      imports: [ChatComponent],
      providers: [
        provideRouter([]),
        { provide: ChatService, useValue: chat },
        { provide: AuthService, useValue: { isObserver: () => observer, isAdmin: () => true } }
      ]
    });
    fixture = TestBed.createComponent(ChatComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  beforeEach(() => {
    chat = new FakeChatService();
    spyOnProperty(document, 'visibilityState', 'get').and.returnValue('visible');
  });

  it('lo que espera respuesta va primero, con la espera más larga arriba', () => {
    chat.conversations = [
      conversation(1, { awaitingReply: true, awaitingReplySince: minutesAfter(5), lastMessageAt: minutesAfter(5) }),
      conversation(2, { lastMessageAt: minutesAfter(9) })
    ];
    create();
    expect(component.conversations().map(item => item.id)).toEqual([1, 2]);

    chat.conversationChanged$.next(conversation(2, {
      awaitingReply: true, awaitingReplySince: minutesAfter(1), lastMessageAt: minutesAfter(9)
    }));

    expect(component.conversations().map(item => item.id)).toEqual([2, 1]);
  });

  it('el reloj de espera avisa al pasar el plazo prometido de 2 minutos', () => {
    create();
    const waiting = conversation(1, { awaitingReply: true, awaitingReplySince: BASE });

    component.now.set(Date.parse(BASE) + 30_000);
    expect(component.waitLabel(waiting)).toBe('Recién llegó');
    expect(component.isOverdue(waiting)).toBeFalse();

    component.now.set(Date.parse(BASE) + 119_000);
    expect(component.isOverdue(waiting)).toBeFalse();

    component.now.set(Date.parse(BASE) + 3 * 60_000);
    expect(component.waitLabel(waiting)).toBe('Esperando 3 min');
    expect(component.isOverdue(waiting)).toBeTrue();
  });

  it('lo escrito fuera de horario no corre el reloj de los 2 minutos', () => {
    create();
    const overnight = conversation(1, { awaitingReply: true, awaitingReplySince: BASE, awaitingOutOfHours: true });

    component.now.set(Date.parse(BASE) + 10 * 3_600_000);
    expect(component.waitLabel(overnight)).toBe('Esperando 10 h');
    expect(component.isOverdue(overnight)).toBeFalse();
  });

  it('una conversación que no espera respuesta no tiene reloj', () => {
    create();
    const answered = conversation(1);
    expect(component.waitMinutes(answered)).toBeNull();
    expect(component.isOverdue(answered)).toBeFalse();
  });

  it('un mensaje del visitante en la conversación abierta se agrega y se marca como leído', () => {
    chat.conversations = [conversation(1, { awaitingReply: true, awaitingReplySince: BASE, unreadCount: 1 })];
    chat.thread = [message(1)];
    create();
    component.openConversation(1);
    chat.markRead.calls.reset();

    chat.messageReceived$.next({
      conversation: conversation(1, { awaitingReply: true, awaitingReplySince: BASE, unreadCount: 1 }),
      message: message(2, { body: 'una más' })
    });

    expect(component.messages().map(item => item.id)).toEqual([1, 2]);
    expect(chat.markRead).toHaveBeenCalledWith(1, 2);
  });

  it('un mensaje de otra conversación no se mezcla con la que se está leyendo', () => {
    chat.conversations = [conversation(1), conversation(2)];
    chat.thread = [message(1)];
    create();
    component.openConversation(1);

    chat.messageReceived$.next({ conversation: conversation(2, { unreadCount: 1 }), message: message(50) });

    expect(component.messages().map(item => item.id)).toEqual([1]);
    expect(component.conversations().find(item => item.id === 2)?.unreadCount).toBe(1);
  });

  it('si el eco en tiempo real llega antes que la respuesta HTTP no se pierde el estado del email', () => {
    chat.conversations = [conversation(1)];
    chat.thread = [message(1)];
    create();
    component.openConversation(1);
    component.draft.set('Te respondo por acá');

    component.send();
    const staff = message(10, { senderType: 'staff', senderName: 'Ana', body: 'Te respondo por acá' });
    chat.messageReceived$.next({ conversation: conversation(1), message: { ...staff, emailStatus: null } });
    chat.replies.next({ message: { ...staff, emailStatus: 'failed' }, conversation: conversation(1) });

    const saved = component.messages().filter(item => item.id === 10);
    expect(saved.length).toBe(1);
    expect(saved[0].emailStatus).toBe('failed');
    expect(component.draft()).toBe('');
    expect(component.sending()).toBeFalse();
  });

  it('no envía una respuesta vacía', () => {
    chat.conversations = [conversation(1)];
    create();
    component.openConversation(1);

    component.draft.set('   ');
    component.send();

    expect(component.canSend()).toBeFalse();
    expect(chat.reply).not.toHaveBeenCalled();
  });

  it('muestra el error del servidor y conserva lo escrito si la respuesta falla', () => {
    chat.conversations = [conversation(1)];
    create();
    component.openConversation(1);
    component.draft.set('Hola');

    component.send();
    chat.replies.error({ error: { message: 'La conversación está cerrada. Reabrila para responder.' } });

    expect(component.sendError()).toBe('La conversación está cerrada. Reabrila para responder.');
    expect(component.draft()).toBe('Hola');
    expect(component.sending()).toBeFalse();
  });

  it('una conversación cerrada sale de Abiertas', () => {
    chat.conversations = [conversation(1), conversation(2)];
    create();

    chat.conversationChanged$.next(conversation(1, { status: 'closed' }));

    expect(component.conversations().map(item => item.id)).toEqual([2]);
  });

  it('el Observador puede leer pero no ve el campo para responder ni el botón de cerrar', () => {
    chat.conversations = [conversation(1)];
    chat.thread = [message(1)];
    create(true);
    component.openConversation(1);
    fixture.detectChanges();

    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('.chat-composer')).toBeNull();
    expect(host.querySelector('.chat-thread__actions')).toBeNull();
    expect(host.querySelector('.chat-readonly')?.textContent).toContain('solo lectura');
    expect(chat.markRead).not.toHaveBeenCalled();
  });

  it('una conversación cerrada no ofrece responder y propone reabrirla', () => {
    chat.conversations = [conversation(1, { status: 'closed' })];
    chat.thread = [message(1)];
    create();
    component.openConversation(1);
    fixture.detectChanges();

    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('.chat-composer')).toBeNull();
    expect(host.querySelector('.chat-readonly')?.textContent).toContain('Reabrila');
    expect(host.querySelector('.chat-thread__actions')?.textContent).toContain('Reabrir');
  });

  it('explica qué pasó con el email de cada respuesta', () => {
    chat.conversations = [conversation(1)];
    create();
    const subject = conversation(1);

    expect(component.emailNote(message(1, { emailStatus: 'sent' }), subject)?.tone).toBe('ok');
    expect(component.emailNote(message(1, { emailStatus: 'sent' }), subject)?.text).toContain('v1@example.com');
    expect(component.emailNote(message(1, { emailStatus: 'failed' }), subject)?.tone).toBe('bad');
    expect(component.emailNote(message(1, { emailStatus: 'no_email' }), subject)?.tone).toBe('warn');
    expect(component.emailNote(message(1, { emailStatus: 'limit' }), subject)?.tone).toBe('warn');
    expect(component.emailNote(message(1, { emailStatus: 'visitor_online' }), subject)).toBeNull();
    expect(component.emailNote(message(1, { emailStatus: null }), subject)).toBeNull();
  });

  it('agrupa los mensajes por día y arma las iniciales y los enlaces de contacto', () => {
    chat.conversations = [conversation(1)];
    chat.thread = [message(1, { createdAt: '2026-10-25T13:00:00Z' }), message(2, { createdAt: '2026-10-25T13:05:00Z' }), message(3)];
    create();
    component.now.set(Date.parse('2026-10-26T15:00:00Z'));
    component.openConversation(1);

    const days = component.timeline().map(entry => entry.day);
    expect(days).toEqual(['Ayer', null, 'Hoy']);
    expect(component.initials('María Fernanda López')).toBe('ML');
    expect(component.initials('Pedro')).toBe('P');
    expect(component.telHref('+54 9 11 5555-1234')).toBe('tel:+5491155551234');
    expect(component.safeHref('javascript:alert(1)')).toBeNull();
    expect(component.safeHref('https://www.guardeloquequiera.net/')).toBe('https://www.guardeloquequiera.net/');
  });
});
