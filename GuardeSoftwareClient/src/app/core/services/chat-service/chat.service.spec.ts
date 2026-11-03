import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../../environments/environments';
import { ChatInboxSummary } from '../../models/chat';
import { ChatService } from './chat.service';

describe('ChatService', () => {
  const api = `${environment.apiUrl}/Chat`;
  const summary: ChatInboxSummary = {
    awaitingReplyCount: 3, unreadConversationCount: 2, oldestAwaitingSince: '2026-10-26T13:00:00Z',
    isOpenNow: true, responseTimeMinutes: 2
  };
  let service: ChatService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ChatService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('pide las conversaciones con el estado y la búsqueda como parámetros', () => {
    service.getConversations('open', 'ana').subscribe();
    const request = http.expectOne(req => req.url === `${api}/conversations`);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('status')).toBe('open');
    expect(request.request.params.get('search')).toBe('ana');
    request.flush([]);
  });

  it('responde enviando solo el texto: el usuario lo resuelve el servidor desde el token', () => {
    service.reply(7, 'Hola').subscribe();
    const request = http.expectOne(`${api}/conversations/7/messages`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ body: 'Hola' });
    request.flush({});
  });

  it('marca como leído hasta el último mensaje que se vio', () => {
    service.markRead(7, 42).subscribe();
    const request = http.expectOne(`${api}/conversations/7/read`);
    expect(request.request.body).toEqual({ upToMessageId: 42 });
    request.flush(null);
  });

  it('cierra, reabre y guarda el horario con los verbos y rutas del servidor', () => {
    service.close(7).subscribe();
    expect(http.expectOne(`${api}/conversations/7/close`).request.method).toBe('POST');

    service.reopen(7).subscribe();
    expect(http.expectOne(`${api}/conversations/7/reopen`).request.method).toBe('POST');

    const days = [{ dayOfWeek: 1, isOpen: true, opensAt: '09:30', closesAt: '15:30' }];
    service.updateBusinessHours(days).subscribe();
    const request = http.expectOne(`${api}/business-hours`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ days });
  });

  it('refreshSummary alimenta el contador del menú y disconnect lo limpia', () => {
    expect(service.summary()).toBeNull();

    service.refreshSummary();
    http.expectOne(`${api}/summary`).flush(summary);
    expect(service.summary()?.awaitingReplyCount).toBe(3);

    service.disconnect();
    expect(service.summary()).toBeNull();
    expect(service.connected()).toBeFalse();
  });

  it('si falla la consulta del resumen conserva el último valor en vez de borrarlo', () => {
    service.refreshSummary();
    http.expectOne(`${api}/summary`).flush(summary);

    service.refreshSummary();
    http.expectOne(`${api}/summary`).flush('error', { status: 500, statusText: 'Server Error' });
    expect(service.summary()?.awaitingReplyCount).toBe(3);
  });
});
