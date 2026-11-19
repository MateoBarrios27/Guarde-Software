import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { BehaviorSubject, of, Subject, throwError } from 'rxjs';
import { registerLocaleData } from '@angular/common';
import localeEsAr from '@angular/common/locales/es-AR';
import { IndexedDbService, PendingPayment, SyncSnapshot } from './indexed-db.service';
import { SyncService } from './sync.service';
import { OfflineService } from './offline.service';
import { DataRefreshService } from '../data-refresh-service/data-refresh.service';
import { FinancesComponent } from '../../../pages/finances/finances.component';
import { DashboardComponent } from '../../../pages/dashboard/dashboard.component';
import { MonthlyInterestService } from '../monthlyInterest-service/monthly-interest.service';
import { AuthService } from '../auth-service/auth.service';
import { OfflinePaymentsComponent } from '../../../shared/components/offline-payments/offline-payments.component';
import Swal from '../../../shared/services/ui-alert.service';

registerLocaleData(localeEsAr, 'es-AR');
const timestamp = '2026-11-11T12:00:00.000Z';
const snapshot = (name = 'Cliente original'): SyncSnapshot => ({ schemaVersion: 1, generatedAt: timestamp,
  clients: [{ id: 1, fullName: name, balance: -100, currentRent: 100, active: true, preferredPaymentMethodId: 2 }],
  paymentMethods: [{ id: 1, name: 'Efectivo', commission: 0 }, { id: 2, name: 'Transferencia', commission: 5 }],
  rentals: [{ id: 1, clientId: 1, active: true }], monthlyInterest: { defaultPercentage: 10, defaultApplyDay: 11, today: '2026-11-11',
    settings: [{ id: 1, effectiveDate: '2026-11-01', percentage: 12.5, applyDay: 15, cutoffDay: 14, isLocked: false }], runs: [] } });
const payment = (clientId = 1): PendingPayment => ({ localId: crypto.randomUUID(), clientId, clientName: 'Cliente de prueba',
  paymentMethodId: 2, paymentMethodName: 'Transferencia', movementType: 'CREDITO', concept: 'Cobro', amount: 100,
  date: timestamp, createdAt: timestamp, status: 'pending', isAdvancePayment: false, skipFutureProjection: true });
const file = (value: unknown) => new File([JSON.stringify(value)], 'respaldo.json');
const waitFor = async (condition: () => boolean) => {
  for (let i = 0; i < 200 && !condition(); i++) await new Promise(resolve => setTimeout(resolve, 10));
  if (!condition()) throw new Error('La condición de la prueba no se completó.');
};

describe('Offline: importación, cola y conciliación', () => {
  let idb: IndexedDbService;
  let service: SyncService;
  let http: { get: jasmine.Spy; post: jasmine.Spy };
  let online: BehaviorSubject<boolean>;
  let offline: OfflineService;
  let refresh: { notify: jasmine.Spy };
  beforeEach(async () => {
    idb = new IndexedDbService();
    await idb.saveSnapshot(snapshot());
    online = new BehaviorSubject(true);
    offline = { get isOnline() { return online.value; }, networkOnline: true, isOnline$: online.asObservable(),
      reportApiAvailability: (value: boolean) => online.next(value) } as unknown as OfflineService;
    refresh = { notify: jasmine.createSpy('notify') };
    http = { get: jasmine.createSpy('get').and.callFake((url: string) => of(url.endsWith('/status') ? { healthy: true } : snapshot())),
      post: jasmine.createSpy('post').and.callFake((_url: string, body: { payments: PendingPayment[] }) => of({ results: body.payments.map(p => ({ localId: p.localId, success: true })) })) };
    service = new SyncService(http as unknown as HttpClient, offline, idb, refresh as unknown as DataRefreshService);
    spyOn(Swal, 'fire').and.resolveTo({ isConfirmed: false, isDenied: false, isDismissed: true });
  });
  afterEach(async () => {
    service.ngOnDestroy();
    const database = (idb as any).db as IDBDatabase;
    database?.close();
    await new Promise<void>((resolve, reject) => { const request = indexedDB.deleteDatabase('GuardeOfflineDB'); request.onsuccess = () => resolve(); request.onerror = () => reject(request.error); });
  });
  it('importa los tres conjuntos y las reglas de intereses, conservando los cobros', async () => {
    await idb.queuePayment(payment());
    expect(await service.importSnapshot(file(snapshot('Nuevo')))).toBeTrue();
    expect((await idb.getCachedClients())[0].fullName).toBe('Nuevo');
    expect((await idb.getCachedPaymentMethods()).length).toBe(2);
    expect((await idb.getMonthlyInterest())!.settings[0].percentage).toBe(12.5);
    expect((await idb.getPendingPayments()).length).toBe(1);
  });
  for (const [name, mutate] of [
    ['sin alquileres', (s: any) => delete s.rentals],
    ['con fecha inválida', (s: any) => s.generatedAt = 'not-a-date'],
    ['con fecha inexistente', (s: any) => s.generatedAt = '2026-02-30T12:00:00Z'],
    ['con clientes duplicados', (s: any) => s.clients.push({ ...s.clients[0] })],
    ['con monto como texto', (s: any) => s.clients[0].balance = '-100'],
    ['con formato futuro', (s: any) => s.schemaVersion = 500],
    ['con arrays incorrectos', (s: any) => s.clients = {}],
  ] as const) {
    it(`rechaza un archivo ${name} sin modificar la caché`, async () => {
      const invalid = snapshot('No debe guardarse'); mutate(invalid);
      expect(await service.importSnapshot(file(invalid))).toBeFalse();
      expect((await idb.getCachedClients())[0].fullName).toBe('Cliente original');
      expect(await idb.getSnapshotTimestamp()).toBe(timestamp);
      expect((await idb.getMonthlyInterest())!.settings[0].applyDay).toBe(15);
    });
  }
  it('aborta la transacción si un put falla después de encolar los clear', async () => {
    const invalid: any = snapshot('No debe guardarse'); invalid.clients[0].nonCloneable = () => {};
    await expectAsync(idb.saveSnapshot(invalid)).toBeRejected();
    expect((await idb.getCachedClients())[0].fullName).toBe('Cliente original');
  });
  it('exige confirmación para reemplazar por una copia anterior y acepta el formato original', async () => {
    const older = snapshot('Anterior'); older.generatedAt = '2025-01-01T00:00:00Z'; delete older.schemaVersion;
    expect(await service.importSnapshot(file(older))).toBeFalse();
    expect(await service.importSnapshot(file(older), true)).toBeTrue();
  });
  it('descargar también actualiza la caché y el archivo incluye sólo el snapshot', async () => {
    http.get.and.returnValue(of(snapshot('Descargado')));
    let blob!: Blob; spyOn(URL, 'createObjectURL').and.callFake(value => { blob = value as Blob; return 'blob:prueba'; });
    spyOn(HTMLAnchorElement.prototype, 'click');
    await service.downloadSnapshot();
    expect((await idb.getCachedClients())[0].fullName).toBe('Descargado');
    expect(JSON.parse(await blob.text()).clients[0].fullName).toBe('Descargado');
    expect(refresh.notify).toHaveBeenCalled();
  });
  it('espera el commit antes de confirmar un cobro y evita otro local del mismo cliente', async () => {
    await service.queuePayment(payment());
    expect((await idb.getPendingPayments()).length).toBe(1);
    await expectAsync(service.queuePayment(payment())).toBeRejected();
    expect(service.pendingCount).toBe(1);
    expect((await idb.getCachedClients())[0].balance).toBe(-100);
  });
  it('cuenta los cobros fallidos e interrumpidos', async () => {
    await idb.restorePayments([payment(1), payment(2)]);
    const all = await idb.getPendingPayments();
    await idb.updatePaymentStatus(all[0].localId, 'failed', 'Revisar');
    await idb.updatePaymentStatus(all[1].localId, 'syncing');
    await service.refreshPendingCount();
    expect(service.pendingCount).toBe(2); expect(await idb.getPendingPaymentsCount()).toBe(2);
  });
  it('concilia automáticamente al iniciar online, incluidos envíos interrumpidos', async () => {
    const p = payment(); await idb.queuePayment(p); await idb.updatePaymentStatus(p.localId, 'syncing');
    await service.init();
    expect(http.post).toHaveBeenCalledTimes(1); expect(service.pendingCount).toBe(0);
  });
  it('comparte una sola operación ante dos llamadas concurrentes', async () => {
    const p = payment(); await idb.queuePayment(p);
    const replies = new Subject<any>(); http.post.and.returnValue(replies);
    const first = service.syncPendingPayments(); const second = service.syncPendingPayments();
    expect(first).toBe(second);
    await waitFor(() => http.post.calls.count() === 1);
    replies.next({ results: [{ localId: p.localId, success: true }] }); replies.complete();
    await Promise.all([first, second]); expect(http.post).toHaveBeenCalledTimes(1);
  });
  it('conserva un rechazo y su motivo en la lista visible', async () => {
    const p = payment(); await idb.queuePayment(p);
    http.post.and.returnValue(of({ results: [{ localId: p.localId, success: false, errorCode: 'PAYMENT_DECISION_REQUIRED', errorMessage: 'Necesita decisión' }] }));
    const result = await service.syncPendingPayments();
    expect(result.failureCount).toBe(1); expect(service.pendingCount).toBe(1);
    expect(service.payments[0].errorCode).toBe('PAYMENT_DECISION_REQUIRED');
    expect(service.syncStatus).toBe('error');
  });
  it('no declara éxito para una respuesta incompleta y conserva el mismo ID para conciliar', async () => {
    const p = payment(); await idb.queuePayment(p); http.post.and.returnValue(of({ results: [] }));
    const result = await service.syncPendingPayments();
    expect(result.failureCount).toBe(1); expect(service.payments[0].localId).toBe(p.localId);
    expect(service.payments[0].status).toBe('pending'); expect(service.syncStatus).toBe('error');
  });
  it('una respuesta perdida no borra el cobro ni cambia su identidad o importe', async () => {
    const p = payment(); await idb.queuePayment(p); http.post.and.returnValue(throwError(() => new HttpErrorResponse({ status: 0 })));
    await service.syncPendingPayments();
    expect(service.payments[0].localId).toBe(p.localId); expect(service.payments[0].amount).toBe(100);
    expect(service.payments[0].status).toBe('pending');
  });
  it('cancelar una decisión conserva el registro fallido sin reenviarlo', async () => {
    const p = payment(); await idb.queuePayment(p);
    await idb.updatePaymentStatus(p.localId, 'failed', 'Decidir', 'PAYMENT_DECISION_REQUIRED', { code: 'PAYMENT_DECISION_REQUIRED', scenario: 'surplus', message: 'Decidir', surplus: 10, remainingDebt: 0, decisionToken: 'decision', expectedPaymentStateToken: 'state', options: [] });
    await service.reviewPayment(p.localId);
    expect(http.post).not.toHaveBeenCalled(); expect((await idb.getPendingPayments())[0].status).toBe('failed');
  });
  it('restaurar cobros es idempotente y no sobrescribe otro importe con el mismo ID', async () => {
    const p = payment(); await idb.restorePayments([p]); await idb.restorePayments([p]);
    expect((await idb.getPendingPayments()).length).toBe(1);
    await expectAsync(idb.restorePayments([payment(2), { ...p, amount: 999 }])).toBeRejected();
    const all = await idb.getPendingPayments(); expect(all.length).toBe(1); expect(all[0].amount).toBe(100);
  });
  it('sólo cierra una revisión de duplicado/eliminado con constancia y conserva el registro', async () => {
    const p = payment(); await idb.queuePayment(p);
    await expectAsync(idb.closeReview(p.localId, 'No confirmado')).toBeRejected();
    await idb.updatePaymentStatus(p.localId, 'failed', 'Ya existe', 'DUPLICATE_PAYMENT');
    await service.closeReview(p.localId, 'Verificado en Finanzas como pago existente.');
    expect(service.pendingCount).toBe(0);
    expect((await idb.getPendingPayments())[0].status).toBe('reviewed');
    await service.syncPendingPayments(); expect(http.post).not.toHaveBeenCalled();
  });
  it('un servidor caído activa el modo local aunque el navegador tenga Internet', async () => {
    http.get.and.returnValue(throwError(() => new HttpErrorResponse({ status: 503 })));
    expect(await service.checkConnection()).toBeFalse(); expect(offline.isOnline).toBeFalse();
  });
  it('no cambia a modo offline por una sesión no autorizada', async () => {
    http.get.and.returnValue(throwError(() => new HttpErrorResponse({ status: 401 })));
    expect(await service.checkConnection()).toBeFalse(); expect(offline.isOnline).toBeTrue();
  });
  it('recupera las reglas mensuales del snapshot al iniciar sin conexión', async () => {
    const monthly = new MonthlyInterestService(http as unknown as HttpClient, refresh as unknown as DataRefreshService, idb, offline);
    expect(await monthly.loadCachedRules()).toBeTrue();
    expect(monthly.ruleForMonth(2026, 11)).toEqual({ percentage: 12.5, applyDay: 15, cutoffDay: 14, isDefault: false });
  });
  it('carga el medio preferido correcto en Finanzas', async () => {
    const component = Object.create(FinancesComponent.prototype) as any;
    Object.assign(component, { offlineService: { isOnline: false }, idb, monthlyInterestService: { loadCachedRules: async () => true },
      invalidateFilteredClientsCache: () => {}, openAutoPaymentClientIfReady: () => {} });
    component.loadClients(); await waitFor(() => component.clientsLoaded);
    component.paymentDto = {}; component.getSuggestedPaymentAmount = () => 100;
    component.checkIncreaseLogic = () => {}; component.syncPaymentPreview = () => {}; component.onAmountChange = () => {}; component.joinPaymentPresence = async () => {};
    component.selectClient(component.clients[0]); expect(component.paymentDto.paymentMethodId).toBe(2);
  });
  it('una falla de almacenamiento conserva el formulario y libera el botón de Finanzas', async () => {
    const component = Object.create(FinancesComponent.prototype) as any;
    Object.assign(component, { offlineService: { isOnline: false }, monthlyInterestService: { loadCachedRules: async () => true }, paymentCollision: null,
      paymentDto: { clientId: 1, amount: 100, date: new Date(), paymentMethodId: 2 }, getCalculatedAmounts: () => ({ amountEntered: 100 }),
      getSurchargeScenario: () => 'D', getClientNameById: () => 'Cliente', getNamePaymentMethodById: () => 'Transferencia',
      syncService: { queuePayment: async () => { throw new Error('Cuota agotada'); } }, closeClientModal: jasmine.createSpy('close') });
    await component.executeBackendCall();
    expect(component.isSubmittingPayment).toBeFalse(); expect(component.paymentDto.amount).toBe(100); expect(component.closeClientModal).not.toHaveBeenCalled();
  });
  it('redirige el cobro del Dashboard a Finanzas cuando está offline', () => {
    const component = Object.create(DashboardComponent.prototype) as any;
    component.offlineService = { isOnline: false }; component.router = { navigate: jasmine.createSpy('navigate') };
    component.openPaymentModalWith({ clientId: 1 }); expect(component.router.navigate).toHaveBeenCalledWith(['/finances'], { queryParams: { autoOpenPayment: 1 } });
  });
});

describe('Cobros locales: vista persistente', () => {
  it('muestra cobros fallidos, motivo e importe y deshabilita revisión sin conexión', async () => {
    const p = { ...payment(), status: 'failed', errorMessage: 'El pago necesita una decisión.' };
    await TestBed.configureTestingModule({ imports: [OfflinePaymentsComponent], providers: [provideRouter([]),
      { provide: SyncService, useValue: { payments$: of([p]), storageError$: of(null), syncStatus: 'idle' } },
      { provide: OfflineService, useValue: { isOnline: false } }, { provide: AuthService, useValue: { isObserver: () => false } }
    ] }).compileComponents();
    const fixture = TestBed.createComponent(OfflinePaymentsComponent); fixture.detectChanges();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('Necesita revisión'); expect(host.textContent).toContain('El pago necesita una decisión.');
    expect(host.querySelector<HTMLButtonElement>('.local-payment button')!.disabled).toBeTrue();
    fixture.destroy(); TestBed.resetTestingModule();
  });
});
