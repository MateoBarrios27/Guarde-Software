import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { A11yModule } from '@angular/cdk/a11y';
import { Subscription } from 'rxjs';
import { OfflineService } from '../../../core/services/offline-service/offline.service';
import { SyncService, SyncStatus } from '../../../core/services/offline-service/sync.service';
import { OfflinePaymentsComponent } from '../offline-payments/offline-payments.component';

@Component({ selector: 'app-offline-banner', standalone: true, imports: [CommonModule, A11yModule, OfflinePaymentsComponent],
  templateUrl: './offline-banner.component.html', styleUrl: './offline-banner.component.css' })
export class OfflineBannerComponent implements OnInit, OnDestroy {
  isOnline = true;
  pendingCount = 0;
  syncStatus: SyncStatus = 'idle';
  lastSnapshotAt: string | null = null;
  storageError: string | null = null;
  showPayments = false;
  private subs = new Subscription();
  constructor(public offlineService: OfflineService, public syncService: SyncService) {}
  ngOnInit(): void {
    void this.syncService.init().catch(() => { this.storageError = 'No se pudo preparar el modo local. Revisá el almacenamiento del navegador.'; });
    this.subs.add(this.offlineService.isOnline$.subscribe(v => this.isOnline = v));
    this.subs.add(this.syncService.pendingCount$.subscribe(v => this.pendingCount = v));
    this.subs.add(this.syncService.syncStatus$.subscribe(v => this.syncStatus = v));
    this.subs.add(this.syncService.lastSnapshotAt$.subscribe(v => this.lastSnapshotAt = v));
    this.subs.add(this.syncService.storageError$.subscribe(v => this.storageError = v));
  }
  get showBanner(): boolean { return !this.isOnline || this.pendingCount > 0 || this.syncStatus === 'syncing' || this.syncStatus === 'error' || !!this.storageError; }
  get bannerClass(): string { return this.storageError || this.syncStatus === 'error' ? 'error' : this.syncStatus === 'syncing' ? 'syncing' : !this.isOnline ? 'offline' : 'pending'; }
  get bannerMessage(): string {
    if (this.storageError) return this.storageError;
    if (this.syncStatus === 'syncing') return `Conciliando ${this.pendingCount} cobro(s) local(es)…`;
    if (!this.isOnline) return `${this.offlineService.reason} · ${this.pendingCount} cobro(s) local(es)`;
    if (this.pendingCount) return `${this.pendingCount} cobro(s) pendiente(s) de conciliación o revisión`;
    return 'No se pudo completar la conciliación. Abrí Cobros locales para revisar.';
  }
  get formattedSnapshotTime(): string {
    if (!this.lastSnapshotAt || Number.isNaN(Date.parse(this.lastSnapshotAt))) return 'No hay respaldo local: importá uno en Configuración';
    return `Datos al ${new Date(this.lastSnapshotAt).toLocaleString('es-AR', { dateStyle: 'short', timeStyle: 'short' })}`;
  }
  async reconnect(): Promise<void> {
    await this.syncService.checkConnection();
    this.offlineService.setManualMode(false);
    await this.syncService.checkConnection();
  }
  ngOnDestroy(): void { this.subs.unsubscribe(); }
}
