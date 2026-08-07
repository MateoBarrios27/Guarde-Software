import { Injectable } from '@angular/core';

export interface CachedClient {
  id: number;
  fullName: string;
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
  // Enriched fields
  nextPaymentDay?: string;
  status?: string;
  rentalId?: number;
  monthsUnpaid?: number;
  increaseAnchorDate?: string;
  increaseFrequencyMonths?: number;
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
  expectedPaymentStateToken?: string | null;
  createdAt: string;         // ISO string - when queued
  status: 'pending' | 'syncing' | 'failed';
  errorMessage?: string;
}

export interface SyncSnapshot {
  generatedAt: string;
  clients: CachedClient[];
  paymentMethods: CachedPaymentMethod[];
  rentals: CachedRental[];
}

const DB_NAME = 'GuardeOfflineDB';
const DB_VERSION = 2; // Bumped to force schema update with new client fields

@Injectable({ providedIn: 'root' })
export class IndexedDbService {
  private db: IDBDatabase | null = null;

  private openDB(): Promise<IDBDatabase> {
    if (this.db) return Promise.resolve(this.db);

    return new Promise((resolve, reject) => {
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
        resolve(this.db);
      };

      request.onerror = () => reject(request.error);
    });
  }

  private async transaction<T>(
    stores: string[],
    mode: IDBTransactionMode,
    fn: (tx: IDBTransaction) => Promise<T>
  ): Promise<T> {
    const db = await this.openDB();
    return new Promise((resolve, reject) => {
      const tx = db.transaction(stores, mode);
      fn(tx).then(resolve).catch(reject);
      tx.onerror = () => reject(tx.error);
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
    const db = await this.openDB();
    return new Promise((resolve, reject) => {
      const tx = db.transaction(['clients_cache', 'payment_methods_cache', 'rentals_cache', 'meta'], 'readwrite');
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

      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
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
    return this.transaction(['pending_payments'], 'readwrite', tx => {
      const store = tx.objectStore('pending_payments');
      return this.idbRequest<IDBValidKey>(store.put(payment)).then(() => undefined);
    });
  }

  async getPendingPayments(): Promise<PendingPayment[]> {
    return this.transaction(['pending_payments'], 'readonly', tx => {
      const store = tx.objectStore('pending_payments');
      return this.idbRequest<PendingPayment[]>(store.getAll());
    });
  }

  async updatePaymentStatus(localId: string, status: PendingPayment['status'], errorMessage?: string): Promise<void> {
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
          store.put(payment);
        }
      };
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
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
    return payments.filter(p => p.status === 'pending').length;
  }
}
