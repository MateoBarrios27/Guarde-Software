import { HttpClient } from '@angular/common/http';
import { Injectable, OnDestroy, signal } from '@angular/core';
import type { HubConnection } from '@microsoft/signalr';
import { Observable, Subject } from 'rxjs';
import { environment } from '../../../../environments/environments';
import {
  ChatBusinessDay,
  ChatBusinessHours,
  ChatConversationDetail,
  ChatConversationSummary,
  ChatIncomingMessage,
  ChatInboxSummary,
  ChatPresenceChange,
  ChatReplyResult
} from '../../models/chat';

/**
 * Chat en vivo de la web: bandeja de la administración.
 *
 * Las acciones (responder, cerrar, marcar leído) pasan por la API REST; el canal
 * de SignalR solo trae novedades. SignalR se importa de forma diferida para que
 * el menú, que muestra el contador, no sume ese código al arranque de la app.
 */
@Injectable({ providedIn: 'root' })
export class ChatService implements OnDestroy {
  private readonly api = `${environment.apiUrl}/Chat`;
  private readonly hubUrl = environment.apiUrl.replace('/api', '') + '/hubs/chat';

  /** Resumen para el contador del menú. null hasta la primera respuesta. */
  readonly summary = signal<ChatInboxSummary | null>(null);
  readonly connected = signal(false);

  readonly messageReceived$ = new Subject<ChatIncomingMessage>();
  readonly conversationChanged$ = new Subject<ChatConversationSummary>();
  readonly presenceChanged$ = new Subject<ChatPresenceChange>();
  /** Se emite al reconectar: lo recibido mientras no había conexión hay que volver a pedirlo. */
  readonly reconnected$ = new Subject<void>();

  private hub?: HubConnection;
  private connecting?: Promise<void>;
  private epoch = 0;
  private refreshTimer?: ReturnType<typeof setTimeout>;
  private fallbackTimer?: ReturnType<typeof setInterval>;

  constructor(private readonly http: HttpClient) {}

  ngOnDestroy(): void {
    this.disconnect();
  }

  // ----------------------------------------------------------------- conexión

  connect(): Promise<void> {
    if (this.hub || this.connecting) {
      return this.connecting ?? Promise.resolve();
    }

    this.connecting = this.openConnection(this.epoch).finally(() => {
      this.connecting = undefined;
    });
    return this.connecting;
  }

  disconnect(): void {
    // Invalida una conexión que todavía se esté abriendo (por ejemplo, al cerrar sesión).
    this.epoch++;
    if (this.fallbackTimer) clearInterval(this.fallbackTimer);
    if (this.refreshTimer) clearTimeout(this.refreshTimer);
    this.fallbackTimer = undefined;
    this.refreshTimer = undefined;

    const hub = this.hub;
    this.hub = undefined;
    this.connected.set(false);
    this.summary.set(null);
    hub?.stop().catch(() => undefined);
  }

  private async openConnection(epoch: number): Promise<void> {
    this.refreshSummary();
    // Por si el canal en tiempo real se corta sin avisar: el contador no queda desactualizado.
    this.fallbackTimer = setInterval(() => this.refreshSummary(), 60_000);

    const signalR = await import('@microsoft/signalr');
    if (epoch !== this.epoch) return;

    const hub = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl, {
        accessTokenFactory: () => localStorage.getItem('authToken') ?? '',
        transport: signalR.HttpTransportType.WebSockets
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    hub.on('ChatMessage', (payload: ChatIncomingMessage) => {
      this.scheduleSummaryRefresh();
      this.messageReceived$.next(payload);
    });
    hub.on('ChatConversation', (conversation: ChatConversationSummary) => {
      this.scheduleSummaryRefresh();
      this.conversationChanged$.next(conversation);
    });
    hub.on('ChatPresence', (change: ChatPresenceChange) => this.presenceChanged$.next(change));
    hub.onreconnected(() => {
      this.connected.set(true);
      this.refreshSummary();
      this.reconnected$.next();
    });
    hub.onclose(() => this.connected.set(false));

    this.hub = hub;
    try {
      await hub.start();
      if (epoch !== this.epoch) {
        void hub.stop();
        return;
      }
      this.connected.set(true);
    } catch (error) {
      // Sin tiempo real igual se puede trabajar: el contador se actualiza por consulta.
      console.warn('[ChatService] No se pudo abrir el canal en tiempo real.', error);
    }
  }

  // ------------------------------------------------------------------ resumen

  refreshSummary(): void {
    this.getSummary().subscribe({
      next: summary => this.summary.set(summary),
      error: () => undefined
    });
  }

  /** Agrupa ráfagas de eventos en una sola consulta. */
  private scheduleSummaryRefresh(): void {
    if (this.refreshTimer) return;
    this.refreshTimer = setTimeout(() => {
      this.refreshTimer = undefined;
      this.refreshSummary();
    }, 250);
  }

  // --------------------------------------------------------------------- REST

  getSummary(): Observable<ChatInboxSummary> {
    return this.http.get<ChatInboxSummary>(`${this.api}/summary`);
  }

  getConversations(status: 'open' | 'closed' | 'all', search = ''): Observable<ChatConversationSummary[]> {
    return this.http.get<ChatConversationSummary[]>(`${this.api}/conversations`, {
      params: { status, search }
    });
  }

  getConversation(conversationId: number): Observable<ChatConversationDetail> {
    return this.http.get<ChatConversationDetail>(`${this.api}/conversations/${conversationId}`);
  }

  markRead(conversationId: number, upToMessageId?: number): Observable<void> {
    return this.http.post<void>(`${this.api}/conversations/${conversationId}/read`, { upToMessageId });
  }

  reply(conversationId: number, body: string): Observable<ChatReplyResult> {
    return this.http.post<ChatReplyResult>(`${this.api}/conversations/${conversationId}/messages`, { body });
  }

  close(conversationId: number): Observable<ChatConversationSummary> {
    return this.http.post<ChatConversationSummary>(`${this.api}/conversations/${conversationId}/close`, {});
  }

  reopen(conversationId: number): Observable<ChatConversationSummary> {
    return this.http.post<ChatConversationSummary>(`${this.api}/conversations/${conversationId}/reopen`, {});
  }

  getBusinessHours(): Observable<ChatBusinessHours> {
    return this.http.get<ChatBusinessHours>(`${this.api}/business-hours`);
  }

  updateBusinessHours(days: ChatBusinessDay[]): Observable<ChatBusinessHours> {
    return this.http.put<ChatBusinessHours>(`${this.api}/business-hours`, { days });
  }
}
