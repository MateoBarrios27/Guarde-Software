import { Injectable } from '@angular/core';
import type { MonthlyInterestOverview } from '../../models/monthly-interest';
import type { PaymentDecisionDetails } from '../payment-service/payment-decision-dialog';
import { paymentIntent, validatePayment, validateSnapshot } from './snapshot-validation';

export interface CachedClient {
  id: number;
  fullName: string;
  dni?: string;
  cuit?: string;
  emails?: string[];
  paymentIdentifier?: number;
  balance?: number;
  currentRent?: number;
  previousBalance?: number;
  pendingSurcharge?: number;
  interestAmount?: number;
  lastGeneratedMonthYear?: string;
  color?: string;
  active?: boolean;
  preferredPaymentMethodId?: number;
  billingTypeId?: number;
  ivaCondition?: string;
  warehouseIds?: number[];
  lockerTypeIds?: number[];
  paymentDaysThisMonth?: number[];
  // Enriched fields
  nextPaymentDay?: string;
  status?: string;
  departureStatus?: string;
  rentalId?: number;
  lockerIdentifiers?: string[];
  monthsUnpaid?: number;
  increaseAnchorDate?: string;
  needsNextRentPlanning?: boolean;
  increaseFrequencyMonths?: number;
  isSixMonthPromotion?: boolean;
  plannedPaymentAmount?: number;
  hasPlannedPayment?: boolean;
}

export interface CachedPaymentMethod {
  id: number;
  name: string;
  commission: number;
}

export interface CachedRental {
  id: number;
  clientId: number;
  active: boolean;
}

export interface PendingPayment {
  localId: string;           // UUID for tracking
  clientId: number;
  clientName: string;        // Denormalized for display
  paymentMethodId: number;
  paymentMethodName: string; // Denormalized for display
  movementType: string;
  concept?: string;
  amount: number;
  date: string;              // ISO string
  isAdvancePayment: boolean;
  advanceMonths?: number;
  commissionAmount?: number;
  commissionConcept?: string;
  skipFutureProjection: boolean;
  surchargeAction?: string;
  surchargeAmount?: number;
  surchargeAmountWasOverridden?: boolean;
  expectedPaymentStateToken?: string | null;
  futureDebitAction?: string;
  paymentDecisionToken?: string;
  createdAt: string;         // ISO string - when queued
  status: 'pending' | 'syncing' | 'failed' | 'reviewed';
  errorMessage?: string;
  errorCode?: string;
  decision?: PaymentDecisionDetails;
  resolutionNote?: string;
  reviewedAt?: string;
}

export interface SyncSnapshot {
  schemaVersion?: number;
  generatedAt: string;
  clients: CachedClient[];
  paymentMethods: CachedPaymentMethod[];
  rentals: CachedRental[];
  monthlyInterest?: MonthlyInterestOverview | null;
}

const DB_NAME = 'GuardeOfflineDB';
const DB_VERSION = 2; // Bumped to force schema update with new client fields

@Injectable({ providedIn: 'root' })
export class IndexedDbService {
  private db: IDBDatabase | null = null;
  private opening: Promise<IDBDatabase> | null = null;

  private openDB(): Promise<IDBDatabase> {
    if (this.db) return Promise.resolve(this.db);

    if (this.opening) return this.opening;
    this.opening = new Promise((resolve, reject) => {
      const request = indexedDB.open(DB_NAME, DB_VERSION);

      request.onupgradeneeded = (event: IDBVersionChangeEvent) => {
        const db = (event.target as IDBOpenDBRequest).result;
        if (!db.objectStoreNames.contains('clients_cache')) {
          db.createObjectStore('clients_cache', { keyPath: 'id' });
        }
        if (!db.objectStoreNames.contains('payment_methods_cache')) {
          db.createObjectStore('payment_methods_cache', { keyPath: 'id' });
        }
        if (!db.objectStoreNames.contains('rentals_cache')) {
          db.createObjectStore('rentals_cache', { keyPath: 'id' });
        }
        if (!db.objectStoreNames.contains('pending_payments')) {
          const store = db.createObjectStore('pending_payments', { keyPath: 'localId' });
          store.createIndex('by_status', 'status');
          store.createIndex('by_date', 'date');
        }
        if (!db.objectStoreNames.contains('meta')) {
          db.createObjectStore('meta', { keyPath: 'key' });
        }
      };

      request.onsuccess = () => {
        this.db = request.result;
        this.db.onversionchange = () => { this.db?.close(); this.db = null; this.opening = null; };
        resolve(this.db);
      };

      request.onerror = () => { this.opening = null; reject(request.error); };
      request.onblocked = () => { this.opening = null; reject(new Error('Cerrá las otras pestañas para habilitar el almacenamiento local.')); };
    });
    return this.opening;
  }

  private async transaction<T>(
    stores: string[],
    mode: IDBTransactionMode,
    fn: (tx: IDBTransaction) => Promise<T>
  ): Promise<T> {
    const db = await this.openDB();
    return new Promise((resolve, reject) => {
      const tx = db.transaction(stores, mode);
      let value: T;
      tx.oncomplete = () => resolve(value);
      tx.onerror = () => reject(tx.error);
      tx.onabort = () => reject(tx.error ?? new Error('No se pudo guardar en este navegador.'));
      try {
        fn(tx).then(result => { value = result; }).catch(error => { try { tx.abort(); } catch {} reject(error); });
      } catch (error) { try { tx.abort(); } catch {} reject(error); }
    });
  }

  private idbRequest<T>(request: IDBRequest<T>): Promise<T> {
    return new Promise((resolve, reject) => {
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
    });
  }

  // ── Snapshot cache ─────────────────────────────────────────────────────────

  async saveSnapshot(snapshot: SyncSnapshot): Promise<void> {
    validateSnapshot(snapshot);
    const db = await this.openDB();
    return new Promise((resolve, reject) => {
      const tx = db.transaction(['clients_cache', 'payment_methods_cache', 'rentals_cache', 'meta'], 'readwrite');
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
      tx.onabort = () => reject(tx.error ?? new Error('Se canceló la importación. Los datos anteriores se conservaron.'));
      try {
      const clientsStore = tx.objectStore('clients_cache');
      const pmStore = tx.objectStore('payment_methods_cache');
      const rentalsStore = tx.objectStore('rentals_cache');
      const metaStore = tx.objectStore('meta');

      // Clear and repopulate
      clientsStore.clear();
      pmStore.clear();
      rentalsStore.clear();

      snapshot.clients.forEach(c => clientsStore.put(c));
      snapshot.paymentMethods.forEach(pm => pmStore.put(pm));
      snapshot.rentals.forEach(r => rentalsStore.put(r));
      metaStore.put({ key: 'snapshot_generated_at', value: snapshot.generatedAt });
      metaStore.put({ key: 'monthly_interest', value: snapshot.monthlyInterest ?? null });
      } catch (error) { tx.abort(); reject(error); }
    });
  }

  async getCachedClients(): Promise<CachedClient[]> {
    return this.transaction(['clients_cache'], 'readonly', tx => {
      const store = tx.objectStore('clients_cache');
      return this.idbRequest<CachedClient[]>(store.getAll());
    });
  }

  async getCachedPaymentMethods(): Promise<CachedPaymentMethod[]> {
    return this.transaction(['payment_methods_cache'], 'readonly', tx => {
      const store = tx.objectStore('payment_methods_cache');
      return this.idbRequest<CachedPaymentMethod[]>(store.getAll());
    });
  }

  async getSnapshotTimestamp(): Promise<string | null> {
    const db = await this.openDB();
    return new Promise((resolve) => {
      const tx = db.transaction(['meta'], 'readonly');
      const req = tx.objectStore('meta').get('snapshot_generated_at');
      req.onsuccess = () => resolve(req.result?.value ?? null);
      req.onerror = () => resolve(null);
    });
  }

  async hasCache(): Promise<boolean> {
    const ts = await this.getSnapshotTimestamp();
    return ts !== null;
  }

  // ── Pending payments queue ──────────────────────────────────────────────────

  async queuePayment(payment: PendingPayment): Promise<void> {
    validatePayment(payment);
    return this.transaction(['pending_payments'], 'readwrite', async tx => {
      const store = tx.objectStore('pending_payments');
      const existing = await this.idbRequest<PendingPayment[]>(store.getAll());
      if (existing.some(p => p.clientId === payment.clientId && p.status !== 'reviewed')) throw new Error('Este cliente ya tiene un cobro local sin conciliar. Revisalo en Cobros locales antes de cargar otro.');
      await this.idbRequest(store.add(payment));
    });
  }

  async getPendingPayments(): Promise<PendingPayment[]> {
    return this.transaction(['pending_payments'], 'readonly', tx => {
      const store = tx.objectStore('pending_payments');
      return this.idbRequest<PendingPayment[]>(store.getAll());
    });
  }

  async updatePaymentStatus(localId: string, status: PendingPayment['status'], errorMessage?: string, errorCode?: string, decision?: PaymentDecisionDetails): Promise<void> {
    const db = await this.openDB();
    return new Promise((resolve, reject) => {
      const tx = db.transaction(['pending_payments'], 'readwrite');
      const store = tx.objectStore('pending_payments');
      const req = store.get(localId);
      req.onsuccess = () => {
        const payment = req.result as PendingPayment;
        if (payment) {
          payment.status = status;
          payment.errorMessage = errorMessage;
          payment.errorCode = errorCode;
          payment.decision = decision;
          store.put(payment);
        }
      };
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
      tx.onabort = () => reject(tx.error ?? new Error('No se pudo actualizar el cobro local.'));
    });
  }

  async removePendingPayment(localId: string): Promise<void> {
    return this.transaction(['pending_payments'], 'readwrite', tx => {
      const store = tx.objectStore('pending_payments');
      return this.idbRequest<undefined>(store.delete(localId));
    });
  }

  async clearFailedPayments(): Promise<void> {
    const all = await this.getPendingPayments();
    const failed = all.filter(p => p.status === 'failed');
    const db = await this.openDB();
    return new Promise((resolve, reject) => {
      const tx = db.transaction(['pending_payments'], 'readwrite');
      const store = tx.objectStore('pending_payments');
      failed.forEach(p => store.delete(p.localId));
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
    });
  }

  async getPendingPaymentsCount(): Promise<number> {
    const payments = await this.getPendingPayments();
    return payments.filter(p => p.status !== 'reviewed').length;
  }

  async getMonthlyInterest(): Promise<MonthlyInterestOverview | null> {
    return this.transaction(['meta'], 'readonly', async tx => (await this.idbRequest<any>(tx.objectStore('meta').get('monthly_interest')))?.value ?? null);
  }
  async saveMonthlyInterest(overview: MonthlyInterestOverview): Promise<void> {
    await this.transaction(['meta'], 'readwrite', async tx => { await this.idbRequest(tx.objectStore('meta').put({ key: 'monthly_interest', value: overview })); });
  }

  async restorePayments(payments: PendingPayment[]): Promise<void> {
    payments.forEach(validatePayment);
    await this.transaction(['pending_payments'], 'readwrite', async tx => {
      const store = tx.objectStore('pending_payments');
      for (const payment of payments) {
        const previous = await this.idbRequest<PendingPayment | undefined>(store.get(payment.localId));
        if (previous && paymentIntent(previous) !== paymentIntent(payment)) throw new Error('El mismo identificador tiene importes distintos. No se importó ningún cobro.');
        if (!previous) await this.idbRequest(store.add({ ...payment, status: payment.status === 'reviewed' ? 'reviewed' : 'pending', decision: undefined }));
      }
    });
  }

  async prepareRetry(localId: string, changes: Partial<Pick<PendingPayment, 'futureDebitAction' | 'paymentDecisionToken' | 'expectedPaymentStateToken'>> = {}): Promise<void> {
    await this.transaction(['pending_payments'], 'readwrite', async tx => {
      const store = tx.objectStore('pending_payments');
      const payment = await this.idbRequest<PendingPayment | undefined>(store.get(localId));
      if (!payment) throw new Error('El cobro ya fue conciliado.');
      if (payment.status === 'reviewed') throw new Error('Esta revisión ya se cerró. No se reenviará el cobro.');
      await this.idbRequest(store.put({ ...payment, ...changes, status: 'pending', errorMessage: undefined, errorCode: undefined, decision: undefined }));
    });
  }

  async closeReview(localId: string, note: string): Promise<void> {
    if (note.trim().length < 6) throw new Error('Dejá una constancia de cómo se resolvió el cobro.');
    await this.transaction(['pending_payments'], 'readwrite', async tx => {
      const store = tx.objectStore('pending_payments');
      const payment = await this.idbRequest<PendingPayment | undefined>(store.get(localId));
      if (!payment || payment.status !== 'failed' || !['DUPLICATE_PAYMENT', 'OFFLINE_PAYMENT_DELETED'].includes(payment.errorCode ?? '')) throw new Error('Este cobro todavía necesita conciliación. No se puede cerrar su revisión.');
      await this.idbRequest(store.put({ ...payment, status: 'reviewed', resolutionNote: note.trim(), reviewedAt: new Date().toISOString() }));
    });
  }
}
