import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { BehaviorSubject, firstValueFrom, interval, Subscription, timeout } from 'rxjs';
import { environment } from '../../../../environments/environments';
import { OfflineService } from './offline.service';
import { IndexedDbService, PendingPayment, SyncSnapshot } from './indexed-db.service';
import { validatePayment, validateSnapshot } from './snapshot-validation';
import { DataRefreshService } from '../data-refresh-service/data-refresh.service';
import { PaymentDecisionDetails, requestPaymentDecision } from '../payment-service/payment-decision-dialog';
import Swal from '../../../shared/services/ui-alert.service';

export type SyncStatus = 'idle' | 'syncing' | 'success' | 'error';
export interface SyncResult {
  successCount: number;
  failureCount: number;
  errors: { localId: string; message: string }[];
}
interface PaymentResult {
  localId: string;
  success: boolean;
  errorMessage?: string;
  errorCode?: string;
  decision?: PaymentDecisionDetails;
  paymentId?: number;
}

@Injectable({ providedIn: 'root' })
export class SyncService {
  private readonly url = environment.apiUrl;
  private readonly pendingSubject = new BehaviorSubject<PendingPayment[]>([]);
  readonly payments$ = this.pendingSubject.asObservable();
  private readonly reviewedSubject = new BehaviorSubject<PendingPayment[]>([]);
  readonly reviewedPayments$ = this.reviewedSubject.asObservable();
  private readonly countSubject = new BehaviorSubject(0);
  readonly pendingCount$ = this.countSubject.asObservable();
  private readonly statusSubject = new BehaviorSubject<SyncStatus>('idle');
  readonly syncStatus$ = this.statusSubject.asObservable();
  private readonly timestampSubject = new BehaviorSubject<string | null>(null);
  readonly lastSnapshotAt$ = this.timestampSubject.asObservable();
  private readonly storageSubject = new BehaviorSubject<string | null>(null);
  readonly storageError$ = this.storageSubject.asObservable();
  private subscriptions = new Subscription();
  private initializing?: Promise<void>;
  private syncing?: Promise<SyncResult>;
  private probing?: Promise<boolean>;
  private refreshing?: Promise<boolean>;
  private lastRefresh = 0;
  importError = '';

  constructor(private http: HttpClient, public offlineService: OfflineService, private idb: IndexedDbService,
    private dataRefresh: DataRefreshService) {}

  get pendingCount(): number { return this.countSubject.value; }
  get syncStatus(): SyncStatus { return this.statusSubject.value; }
  get payments(): readonly PendingPayment[] { return this.pendingSubject.value; }

  init(): Promise<void> {
    return this.initializing ??= this.initialize();
  }
  private async initialize(): Promise<void> {
    try {
      await this.refreshPendingCount();
      const timestamp = await this.idb.getSnapshotTimestamp();
      this.timestampSubject.next(timestamp);
      this.lastRefresh = timestamp ? Date.parse(timestamp) : 0;
    } catch { this.storageSubject.next('El almacenamiento local no está disponible. No se pueden guardar cobros sin conexión.'); }
    let previous = this.offlineService.isOnline;
    this.subscriptions.add(this.offlineService.isOnline$.subscribe(online => {
      const reconnect = online && !previous;
      previous = online;
      if (reconnect) void this.workOnline().catch(() => this.statusSubject.next('error'));
    }));
    // A browser can retain network access while the API or SQL Server is down.
    this.subscriptions.add(interval(15000).subscribe(() => {
      void this.checkConnection().then(online => online ? this.workOnline() : undefined).catch(() => this.statusSubject.next('error'));
    }));
    if (await this.checkConnection()) await this.workOnline();
  }
  async checkConnection(): Promise<boolean> {
    if (!this.offlineService.networkOnline) return false;
    if (this.probing) return this.probing;
    this.probing = (async () => {
      try {
        const status = await firstValueFrom(this.http.get<{ healthy: boolean }>(`${this.url}/Sync/status`).pipe(timeout(6000)));
        this.offlineService.reportApiAvailability(status?.healthy === true);
      } catch (error) {
        if (!(error instanceof HttpErrorResponse) || ![401, 403].includes(error.status)) this.offlineService.reportApiAvailability(false);
        return false;
      }
      return this.offlineService.isOnline;
    })();
    try { return await this.probing; } finally { this.probing = undefined; }
  }
  private async workOnline(): Promise<void> {
    if (!this.offlineService.isOnline) return;
    await this.refreshPendingCount();
    if (this.payments.some(p => p.status !== 'failed')) await this.syncPendingPayments();
    if (!this.lastRefresh || Date.now() - this.lastRefresh >= 5 * 60 * 1000) await this.refreshSnapshot();
  }
  refreshSnapshot(): Promise<boolean> {
    if (this.refreshing) return this.refreshing;
    this.refreshing = (async () => {
      try {
        const snapshot = await firstValueFrom(this.http.get<SyncSnapshot>(`${this.url}/Sync/snapshot`).pipe(timeout(30000)));
        validateSnapshot(snapshot);
        await this.saveSnapshot(snapshot);
        return true;
      } catch (error) {
        if (error instanceof HttpErrorResponse && (error.status === 0 || error.status >= 500)) this.offlineService.reportApiAvailability(false);
        return false;
      }
    })();
    return this.refreshing.finally(() => { this.refreshing = undefined; });
  }
  private async saveSnapshot(snapshot: SyncSnapshot): Promise<void> {
    try { await this.idb.saveSnapshot(snapshot); }
    catch (error) { this.storageSubject.next('No se pudo actualizar el respaldo local. Revisá el espacio disponible y los permisos del navegador.'); throw error; }
    this.storageSubject.next(null);
    this.timestampSubject.next(snapshot.generatedAt);
    this.lastRefresh = Date.parse(snapshot.generatedAt);
    this.dataRefresh.notify(['clients', 'finances', 'catalog'], null);
  }
  async refreshPendingCount(): Promise<void> {
    const payments = (await this.idb.getPendingPayments()).sort((a, b) => a.date.localeCompare(b.date) || a.createdAt.localeCompare(b.createdAt));
    const unresolved = payments.filter(p => p.status !== 'reviewed');
    this.pendingSubject.next(unresolved);
    this.reviewedSubject.next(payments.filter(p => p.status === 'reviewed'));
    this.countSubject.next(unresolved.length); // Includes failed and interrupted sends.
    this.storageSubject.next(null);
  }
  async queuePayment(payment: Omit<PendingPayment, 'createdAt' | 'status'>): Promise<void> {
    if (!await this.idb.hasCache()) throw new Error('Primero importá o descargá un respaldo en este navegador.');
    const full: PendingPayment = { ...payment, createdAt: new Date().toISOString(), status: 'pending' };
    validatePayment(full);
    await this.idb.queuePayment(full);
    await this.refreshPendingCount();
  }

  syncPendingPayments(localIds?: readonly string[]): Promise<SyncResult> {
    if (this.syncing) return this.syncing;
    const run = async () => {
      // Other tabs share IndexedDB. Read the queue only after acquiring the lock.
      if (navigator.locks) return navigator.locks.request('guarde-offline-payments', () => this.sendPayments(localIds));
      return this.sendPayments(localIds);
    };
    this.syncing = run().finally(() => { this.syncing = undefined; });
    return this.syncing;
  }
  private async sendPayments(localIds?: readonly string[]): Promise<SyncResult> {
    const result: SyncResult = { successCount: 0, failureCount: 0, errors: [] };
    if (!this.offlineService.isOnline) return result;
    const pending = (await this.idb.getPendingPayments()).filter(p => ['pending', 'syncing'].includes(p.status) && (!localIds || localIds.includes(p.localId)))
      .sort((a, b) => a.date.localeCompare(b.date) || a.createdAt.localeCompare(b.createdAt)).slice(0, 100);
    if (!pending.length) { await this.refreshPendingCount(); return result; }
    this.statusSubject.next('syncing');
    try {
      for (const payment of pending) await this.idb.updatePaymentStatus(payment.localId, 'syncing');
      await this.refreshPendingCount();
      const response = await firstValueFrom(this.http.post<{ results: PaymentResult[] }>(`${this.url}/Sync/payments`, {
        payments: pending.map(p => ({
          localId: p.localId, clientId: p.clientId, paymentMethodId: p.paymentMethodId, movementType: p.movementType,
          concept: p.concept, amount: p.amount, date: p.date, isAdvancePayment: p.isAdvancePayment, advanceMonths: p.advanceMonths,
          commissionAmount: p.commissionAmount, commissionConcept: p.commissionConcept, skipFutureProjection: true,
          surchargeAction: p.surchargeAction, surchargeAmount: p.surchargeAmount, surchargeAmountWasOverridden: p.surchargeAmountWasOverridden,
          expectedPaymentStateToken: p.expectedPaymentStateToken, futureDebitAction: p.futureDebitAction, paymentDecisionToken: p.paymentDecisionToken
        }))
      }).pipe(timeout(90000)));
      if (!response || !Array.isArray(response.results)) throw new Error('No se recibió la confirmación de los cobros.');
      const replies = new Map<string, PaymentResult>();
      for (const reply of response.results) {
        if (!pending.some(p => p.localId === reply.localId) || replies.has(reply.localId) || typeof reply.success !== 'boolean') throw new Error('La respuesta de sincronización está incompleta.');
        replies.set(reply.localId, reply);
      }
      for (const payment of pending) {
        const reply = replies.get(payment.localId);
        if (reply?.success) { await this.idb.removePendingPayment(payment.localId); result.successCount++; }
        else {
          const message = reply?.errorMessage ?? 'El servidor no confirmó este cobro. Se conserva para conciliarlo.';
          await this.idb.updatePaymentStatus(payment.localId, reply ? 'failed' : 'pending', message, reply?.errorCode ?? 'SYNC_UNCERTAIN', reply?.decision);
          result.failureCount++; result.errors.push({ localId: payment.localId, message });
        }
      }
    } catch (error) {
      // The server may have committed. Keep the same ID and immutable payload for reconciliation.
      for (const payment of pending) {
        const message = 'No se pudo confirmar el resultado. El cobro se conserva y se conciliará con el mismo identificador.';
        try { await this.idb.updatePaymentStatus(payment.localId, 'pending', message, 'SYNC_UNCERTAIN'); }
        catch { this.storageSubject.next('No se pudo actualizar el estado local. Conservá los datos del navegador y volvé a conciliar el cobro.'); }
        result.failureCount++; result.errors.push({ localId: payment.localId, message });
      }
    } finally {
      try { await this.refreshPendingCount(); }
      catch { this.storageSubject.next('No se pudo leer la cola local. No borres los datos del navegador.'); }
      this.statusSubject.next(result.failureCount || this.storageSubject.value ? 'error' : 'success');
    }
    if (result.successCount) {
      await this.refreshSnapshot();
      this.dataRefresh.notify(['clients', 'finances'], null);
    }
    return result;
  }

  async reviewPayment(localId: string): Promise<void> {
    if (!await this.checkConnection()) throw new Error('Restablecé la conexión con el servidor para revisar el cobro.');
    if (this.syncing) await this.syncing;
    const payment = (await this.idb.getPendingPayments()).find(p => p.localId === localId);
    if (!payment) { await this.refreshPendingCount(); return; }
    const changes: Partial<Pick<PendingPayment, 'expectedPaymentStateToken' | 'futureDebitAction' | 'paymentDecisionToken'>> = {};
    if (payment.errorCode === 'PAYMENT_DECISION_REQUIRED' && payment.decision) {
      const action = await requestPaymentDecision(payment.decision);
      if (!action) return;
      changes.futureDebitAction = action;
      changes.paymentDecisionToken = payment.decision.decisionToken;
      changes.expectedPaymentStateToken = payment.decision.expectedPaymentStateToken;
    } else if (payment.errorCode === 'PAYMENT_STATE_CHANGED') {
      const current = await firstValueFrom(this.http.get<{ stateToken: string; client: { balance: number; currentRent: number } }>(`${this.url}/Sync/payment-state/${payment.clientId}`).pipe(timeout(30000)));
      const money = (amount: number) => new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' }).format(amount);
      const confirmed = await Swal.fire({ title: 'Revisá la cuenta actualizada',
        text: `El cobro local de ${money(payment.amount)} conserva su fecha e importe. El saldo actual del cliente es ${money(current.client.balance)} y su abono es ${money(current.client.currentRent)}. ¿Confirmás aplicar este cobro a la cuenta actual?`,
        icon: 'warning', showCancelButton: true, confirmButtonText: 'Confirmar cobro local', cancelButtonText: 'Volver' });
      if (!confirmed.isConfirmed) return;
      changes.expectedPaymentStateToken = current.stateToken;
      changes.futureDebitAction = undefined; changes.paymentDecisionToken = undefined;
    } else if (['DUPLICATE_PAYMENT', 'OFFLINE_PAYMENT_CHANGED', 'OFFLINE_PAYMENT_DELETED'].includes(payment.errorCode ?? '')) {
      throw new Error(payment.errorMessage ?? 'Revisá el pago registrado en Finanzas antes de continuar. El cobro local se conserva.');
    }
    await this.idb.prepareRetry(localId, changes);
    await this.syncPendingPayments([localId]);
  }
  async readSnapshot(file: File): Promise<SyncSnapshot> {
    if (file.size > 50 * 1024 * 1024) throw new Error('El archivo supera los 50 MB.');
    const snapshot: unknown = JSON.parse(await file.text());
    validateSnapshot(snapshot);
    return snapshot;
  }
  async closeReview(localId: string, note: string): Promise<void> {
    await this.idb.closeReview(localId, note);
    await this.refreshPendingCount();
  }
  async importSnapshot(file: File, allowOlder = false): Promise<boolean> {
    this.importError = '';
    try {
      const snapshot = await this.readSnapshot(file);
      const previous = await this.idb.getSnapshotTimestamp();
      if (!allowOlder && previous && Date.parse(snapshot.generatedAt) < Date.parse(previous)) throw new Error('El respaldo es anterior al que está cargado. Confirmá su reemplazo.');
      await this.saveSnapshot(snapshot);
      return true;
    } catch (error) { this.importError = error instanceof Error ? error.message : 'No se pudo importar el respaldo.'; return false; }
  }
  async downloadSnapshot(): Promise<void> {
    const snapshot = await firstValueFrom(this.http.get<SyncSnapshot>(`${this.url}/Sync/snapshot`).pipe(timeout(30000)));
    validateSnapshot(snapshot);
    await this.saveSnapshot(snapshot);
    this.downloadJson(snapshot, 'guarde-snapshot');
  }
  async exportPayments(): Promise<void> {
    this.downloadJson({ kind: 'guarde-offline-payments', schemaVersion: 1, origin: location.origin, generatedAt: new Date().toISOString(), payments: await this.idb.getPendingPayments() }, 'guarde-cobros-locales');
  }
  async importPayments(file: File): Promise<void> {
    if (file.size > 50 * 1024 * 1024) throw new Error('El archivo supera los 50 MB.');
    const backup = JSON.parse(await file.text());
    if (backup?.kind !== 'guarde-offline-payments' || backup.schemaVersion !== 1 || backup.origin !== location.origin || !Array.isArray(backup.payments)) throw new Error('El archivo de cobros no corresponde a este sistema.');
    backup.payments.forEach(validatePayment);
    await this.idb.restorePayments(backup.payments);
    await this.refreshPendingCount();
  }
  private downloadJson(value: unknown, name: string): void {
    const url = URL.createObjectURL(new Blob([JSON.stringify(value, null, 2)], { type: 'application/json' }));
    const anchor = document.createElement('a');
    anchor.href = url; anchor.download = `${name}-${new Date().toISOString().slice(0, 10)}.json`;
    document.body.appendChild(anchor); anchor.click(); anchor.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
  ngOnDestroy(): void { this.subscriptions.unsubscribe(); }
}
