import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription, combineLatest } from 'rxjs';
import { OfflineService } from '../../../core/services/offline-service/offline.service';
import { SyncService, SyncStatus } from '../../../core/services/offline-service/sync.service';

@Component({
  selector: 'app-offline-banner',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './offline-banner.component.html',
  styleUrl: './offline-banner.component.css'
})
export class OfflineBannerComponent implements OnInit, OnDestroy {
  isOnline = true;
  pendingCount = 0;
  syncStatus: SyncStatus = 'idle';
  lastSnapshotAt: string | null = null;

  private subs: Subscription[] = [];

  constructor(
    public offlineService: OfflineService,
    public syncService: SyncService
  ) {}

  ngOnInit(): void {
    void this.syncService.init();
    this.subs.push(
      this.offlineService.isOnline$.subscribe((v: boolean) => this.isOnline = v),
      this.syncService.pendingCount$.subscribe((v: number) => this.pendingCount = v),
      this.syncService.syncStatus$.subscribe((v: SyncStatus) => this.syncStatus = v),
      this.syncService.lastSnapshotAt$.subscribe((v: string | null) => this.lastSnapshotAt = v)
    );
  }

  get showBanner(): boolean {
    return !this.isOnline || this.pendingCount > 0 || this.syncStatus === 'syncing' || this.syncStatus === 'success' || this.syncStatus === 'error';
  }

  get bannerClass(): string {
    if (this.syncStatus === 'syncing') return 'syncing';
    if (this.syncStatus === 'success') return 'success';
    if (this.syncStatus === 'error') return 'error';
    if (!this.isOnline) return 'offline';
    if (this.pendingCount > 0) return 'pending';
    return 'idle';
  }

  get bannerMessage(): string {
    if (this.syncStatus === 'syncing') return `Sincronizando ${this.pendingCount} pago(s)...`;
    if (this.syncStatus === 'success') return 'Pagos sincronizados correctamente';
    if (this.syncStatus === 'error') return 'Algunos pagos no pudieron sincronizarse. Revise la conexión.';
    if (!this.isOnline && this.pendingCount > 0) return `Sin conexión — ${this.pendingCount} pago(s) guardado(s) localmente`;
    if (!this.isOnline) return 'Sin conexión — Modo offline activo';
    if (this.pendingCount > 0) return `${this.pendingCount} pago(s) pendiente(s) de sincronizar`;
    return '';
  }

  get formattedSnapshotTime(): string {
    if (!this.lastSnapshotAt) return 'Sin datos en caché';
    const d = new Date(this.lastSnapshotAt);
    return `Datos al ${d.toLocaleDateString('es-AR')} ${d.toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit' })}`;
  }

  async manualSync(): Promise<void> {
    await this.syncService.syncPendingPayments();
  }

  ngOnDestroy(): void {
    this.subs.forEach(s => s.unsubscribe());
  }
}
