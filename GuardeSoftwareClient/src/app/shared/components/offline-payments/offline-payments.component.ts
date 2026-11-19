import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { SyncService } from '../../../core/services/offline-service/sync.service';
import { OfflineService } from '../../../core/services/offline-service/offline.service';
import { AuthService } from '../../../core/services/auth-service/auth.service';
import Swal from '../../services/ui-alert.service';

let nextFileId = 0;
@Component({
  selector: 'app-offline-payments', standalone: true, imports: [CommonModule, RouterLink],
  template: `
    <section class="local-payments" aria-label="Cobros locales">
      <p class="local-help">Estos cobros se conservan en este navegador hasta que el servidor confirme su registro. Los saldos del respaldo no incluyen los cobros locales.</p>
      <p *ngIf="sync.storageError$ | async as storageError" class="local-error" role="alert">{{ storageError }}</p>
      <p *ngIf="error" class="local-error" role="alert">{{ error }}</p>
      <div class="local-toolbar">
        <button type="button" class="g-btn g-btn--secondary" (click)="exportPayments()" [disabled]="!!busy">Exportar cobros locales</button>
        <button *ngIf="!auth.isObserver()" type="button" class="g-btn g-btn--secondary" (click)="paymentFile.click()" [disabled]="!!busy">Restaurar archivo de cobros</button>
        <input #paymentFile class="local-file" [id]="fileId" type="file" accept=".json,application/json" (change)="importPayments($event)" [disabled]="!!busy">
        <a routerLink="/finances">Ir a Finanzas</a>
      </div>
      <ng-container *ngIf="sync.payments$ | async as payments">
        <p *ngIf="!payments.length" class="local-empty">No hay cobros locales pendientes de conciliación.</p>
        <article *ngFor="let payment of payments; trackBy: trackId" class="local-payment">
          <div class="local-payment-top"><strong>{{ payment.clientName }}</strong><strong class="local-amount">{{ payment.amount | currency:'ARS':'symbol':'1.2-2':'es-AR' }}</strong></div>
          <p>{{ payment.date | date:'dd/MM/yyyy HH:mm' }} · {{ payment.paymentMethodName }}</p>
          <p class="local-state">{{ payment.status === 'failed' ? 'Necesita revisión' : payment.status === 'syncing' ? 'Enviado, pendiente de confirmación' : 'Pendiente de conciliación' }}</p>
          <p *ngIf="payment.errorMessage" class="local-error">{{ payment.errorMessage }}</p>
          <p *ngIf="payment.errorCode === 'DUPLICATE_PAYMENT'">Revisá el pago existente en Finanzas. Este registro se conserva para evitar otro cobro.</p>
          <details><summary>Identificador del cobro</summary><code>{{ payment.localId }}</code></details>
          <button *ngIf="!auth.isObserver()" type="button" class="g-btn g-btn--primary" (click)="review(payment.localId)"
            [disabled]="!offline.isOnline || !!busy || sync.syncStatus === 'syncing'">
            {{ busy === payment.localId ? 'Revisando…' : payment.status === 'failed' ? 'Revisar con conexión' : 'Conciliar ahora' }}
          </button>
          <button *ngIf="!auth.isObserver() && (payment.errorCode === 'DUPLICATE_PAYMENT' || payment.errorCode === 'OFFLINE_PAYMENT_DELETED')" type="button" class="g-btn g-btn--secondary"
            (click)="closeReview(payment.localId)" [disabled]="!offline.isOnline || !!busy">Cerrar revisión con constancia</button>
        </article>
      </ng-container>
      <details *ngIf="sync.reviewedPayments$ | async as reviewed"><summary>Revisiones cerradas ({{ reviewed.length }})</summary>
        <article *ngFor="let payment of reviewed" class="local-payment"><strong>{{ payment.clientName }} · {{ payment.amount | currency:'ARS' }}</strong>
          <p>{{ payment.resolutionNote }}</p><p>{{ payment.reviewedAt | date:'dd/MM/yyyy HH:mm' }}</p><code>{{ payment.localId }}</code></article>
      </details>
      <p class="local-help">Exportá los cobros antes de cambiar de equipo o borrar datos del navegador. El respaldo de clientes se descarga por separado. No hay cobros confirmados hasta recibir la respuesta del servidor.</p>
    </section>`,
  styles: [`
    :host{display:block;min-width:0}.local-payments{color:#334155;font-size:14px;line-height:1.5}.local-help{margin:0 0 16px;color:#64748b}.local-toolbar{display:flex;flex-wrap:wrap;align-items:center;gap:10px;margin-bottom:18px}
    a{color:#2563eb;font-weight:700}.local-file{display:none}
    a:focus-visible{outline:3px solid #93c5fd;outline-offset:3px}.local-payment{border:1px solid #e2e8f0;border-radius:12px;padding:16px;margin-bottom:12px;background:#fff;overflow-wrap:anywhere}
    .local-payment-top{display:flex;gap:12px;justify-content:space-between}.local-amount{color:#123a74;font-size:17px;white-space:nowrap}.local-payment p{margin:6px 0}.local-state{font-weight:800;color:#c2410c}.local-error{color:#be123c}.local-payment button{margin-top:10px}.local-empty{background:#f1f5f9;border-radius:8px;padding:18px}
    details{font-size:12px;color:#64748b;margin:8px 0}summary{cursor:pointer}code{display:block;margin-top:6px;white-space:normal}@media(max-width:480px){.local-payment-top{flex-direction:column;gap:2px}.local-toolbar>*{width:100%;justify-content:center}.local-file{width:1px}.local-payment{padding:12px}}
  `]
})
export class OfflinePaymentsComponent {
  readonly fileId = `local-payments-file-${++nextFileId}`;
  busy = '';
  error = '';
  constructor(public sync: SyncService, public offline: OfflineService, public auth: AuthService) {}
  trackId(_index: number, payment: { localId: string }): string { return payment.localId; }
  async review(localId: string): Promise<void> {
    this.busy = localId; this.error = '';
    try { await this.sync.reviewPayment(localId); }
    catch (error) { this.error = error instanceof Error ? error.message : 'No se pudo revisar el cobro. Se conserva localmente.'; }
    finally { this.busy = ''; }
  }
  async exportPayments(): Promise<void> {
    this.busy = 'export'; this.error = '';
    try { await this.sync.exportPayments(); }
    catch { this.error = 'No se pudo exportar la cola. No borres los datos del navegador.'; }
    finally { this.busy = ''; }
  }
  async closeReview(localId: string): Promise<void> {
    this.busy = localId; this.error = '';
    try {
      const result = await Swal.fire({ title: 'Cerrar revisión local', icon: 'question', input: 'textarea', inputLabel: 'Cómo se resolvió este cobro',
        text: 'Verificá el pago existente o su eliminación en Finanzas. Esta constancia conserva el registro local y lo retira de los pendientes; no registra ni elimina un pago del servidor.',
        inputValidator: value => value.trim().length >= 6 ? undefined : 'Explicá cómo se resolvió el cobro.',
        showCancelButton: true, confirmButtonText: 'Cerrar revisión', cancelButtonText: 'Volver' });
      if (result.isConfirmed) await this.sync.closeReview(localId, String(result.value ?? ''));
    } catch (error) { this.error = error instanceof Error ? error.message : 'No se pudo cerrar la revisión.'; }
    finally { this.busy = ''; }
  }
  async importPayments(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    if (!input.files?.length) return;
    this.busy = 'import'; this.error = '';
    try { await this.sync.importPayments(input.files[0]); }
    catch (error) { this.error = error instanceof Error ? error.message : 'No se pudo restaurar el archivo de cobros.'; }
    finally { this.busy = ''; input.value = ''; }
  }
}
