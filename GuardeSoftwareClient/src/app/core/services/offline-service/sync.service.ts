import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, interval, Subscription } from 'rxjs';
import { filter, switchMap } from 'rxjs/operators';
import { environment } from '../../../../environments/environments';
import { OfflineService } from './offline.service';
import { IndexedDbService, PendingPayment, SyncSnapshot } from './indexed-db.service';

export type SyncStatus = 'idle' | 'syncing' | 'success' | 'error';

export interface SyncResult {
  successCount: number;
  failureCount: number;
  errors: { localId: string; message: string }[];
}

@Injectable({ providedIn: 'root' })
export class SyncService {
  private url = environment.apiUrl;

  private _pendingCount = new BehaviorSubject<number>(0);
  readonly pendingCount$ = this._pendingCount.asObservable();

  private _syncStatus = new BehaviorSubject<SyncStatus>('idle');
  readonly syncStatus$ = this._syncStatus.asObservable();

  private _lastSnapshotAt = new BehaviorSubject<string | null>(null);
  readonly lastSnapshotAt$ = this._lastSnapshotAt.asObservable();

  private autoRefreshSub?: Subscription;
  private wasOffline = false;

  constructor(
    private http: HttpClient,
    private offlineService: OfflineService,
    private idb: IndexedDbService
  ) {}

  /** Call once from AppComponent.ngOnInit */
  async init(): Promise<void> {
    // Load pending count from storage
    await this.refreshPendingCount();

    // Load last snapshot timestamp
    const ts = await this.idb.getSnapshotTimestamp();
    this._lastSnapshotAt.next(ts);

    // Auto-refresh snapshot every 5 minutes when online
    this.autoRefreshSub = interval(5 * 60 * 1000).pipe(
      filter(() => this.offlineService.isOnline)
    ).subscribe(() => this.refreshSnapshot());

    // On reconnect: refresh snapshot AND sync pending payments
    this.offlineService.isOnline$.subscribe(async (isOnline) => {
      if (isOnline && this.wasOffline) {
        await this.refreshSnapshot();
        const pending = await this.idb.getPendingPayments();
        if (pending.filter(p => p.status === 'pending').length > 0) {
          await this.syncPendingPayments();
        }
      }
      this.wasOffline = !isOnline;
    });

    // Do initial snapshot if online
    if (this.offlineService.isOnline) {
      this.refreshSnapshot(); // fire and forget
    }
  }

  async refreshSnapshot(): Promise<boolean> {
    try {
      const snapshot = await this.http.get<SyncSnapshot>(`${this.url}/Sync/snapshot`).toPromise();
      if (snapshot) {
        await this.idb.saveSnapshot(snapshot);
        this._lastSnapshotAt.next(snapshot.generatedAt);
        return true;
      }
      return false;
    } catch {
      return false;
    }
  }

  async refreshPendingCount(): Promise<void> {
    const count = await this.idb.getPendingPaymentsCount();
    this._pendingCount.next(count);
  }

  async queuePayment(payment: Omit<PendingPayment, 'createdAt' | 'status'>): Promise<void> {
    const full: PendingPayment = {
      ...payment,
      createdAt: new Date().toISOString(),
      status: 'pending'
    };
    await this.idb.queuePayment(full);
    await this.refreshPendingCount();
  }

  async syncPendingPayments(): Promise<SyncResult> {
    const result: SyncResult = { successCount: 0, failureCount: 0, errors: [] };
    const pending = (await this.idb.getPendingPayments()).filter(p => p.status === 'pending');

    if (pending.length === 0) return result;

    this._syncStatus.next('syncing');

    try {
      // Mark all as syncing
      for (const p of pending) {
        await this.idb.updatePaymentStatus(p.localId, 'syncing');
      }

      const requestBody = {
        payments: pending.map(p => ({
          localId: p.localId,
          clientId: p.clientId,
          paymentMethodId: p.paymentMethodId,
          movementType: p.movementType,
          concept: p.concept,
          amount: p.amount,
          date: p.date,
          isAdvancePayment: p.isAdvancePayment,
          advanceMonths: p.advanceMonths,
          commissionAmount: p.commissionAmount,
          commissionConcept: p.commissionConcept,
          skipFutureProjection: p.skipFutureProjection,
          surchargeAction: p.surchargeAction,
          surchargeAmount: p.surchargeAmount
        }))
      };

      const response = await this.http
        .post<{ results: { localId: string; success: boolean; errorMessage?: string }[] }>(
          `${this.url}/Sync/payments`,
          requestBody
        )
        .toPromise();

      if (response?.results) {
        for (const r of response.results) {
          if (r.success) {
            await this.idb.removePendingPayment(r.localId);
            result.successCount++;
          } else {
            await this.idb.updatePaymentStatus(r.localId, 'failed', r.errorMessage);
            result.failureCount++;
            result.errors.push({ localId: r.localId, message: r.errorMessage ?? 'Error desconocido' });
          }
        }
      }

      await this.refreshPendingCount();
      this._syncStatus.next(result.failureCount > 0 ? 'error' : 'success');

      // Refresh snapshot after sync so balances are up to date
      await this.refreshSnapshot();

      // Reset status to idle after 5 seconds
      setTimeout(() => this._syncStatus.next('idle'), 5000);

    } catch (err) {
      // Mark all syncing back to pending on network error
      for (const p of pending) {
        await this.idb.updatePaymentStatus(p.localId, 'pending');
      }
      this._syncStatus.next('error');
      setTimeout(() => this._syncStatus.next('idle'), 5000);
    }

    return result;
  }

  /** Download the current snapshot as a .json file */
  async downloadSnapshot(): Promise<void> {
    const snapshot = await this.http.get<SyncSnapshot>(`${this.url}/Sync/snapshot`).toPromise();
    if (!snapshot) return;

    const json = JSON.stringify(snapshot, null, 2);
    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    const date = new Date().toISOString().split('T')[0];
    a.href = url;
    a.download = `guarde-snapshot-${date}.json`;
    a.click();
    URL.revokeObjectURL(url);
  }

  /** Import a snapshot from a .json file and save it to IndexedDB */
  async importSnapshot(file: File): Promise<boolean> {
    try {
      const text = await file.text();
      const snapshot: SyncSnapshot = JSON.parse(text);

      // Basic validation
      if (!snapshot.clients || !snapshot.paymentMethods || !snapshot.generatedAt) {
        throw new Error('Archivo de snapshot inválido o corrupto.');
      }

      await this.idb.saveSnapshot(snapshot);
      this._lastSnapshotAt.next(snapshot.generatedAt);
      return true;
    } catch {
      return false;
    }
  }

  get pendingCount(): number {
    return this._pendingCount.getValue();
  }

  get syncStatus(): SyncStatus {
    return this._syncStatus.getValue();
  }

  ngOnDestroy(): void {
    this.autoRefreshSub?.unsubscribe();
  }
}
