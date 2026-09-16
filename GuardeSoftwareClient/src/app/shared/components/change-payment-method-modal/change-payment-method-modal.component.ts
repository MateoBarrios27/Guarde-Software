import { ChangeDetectorRef, Component, DestroyRef, EventEmitter, inject, Input, OnInit, Output } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { A11yModule } from '@angular/cdk/a11y';
import { forkJoin, finalize } from 'rxjs';
import { ClientService, PaymentMethodChangeContext } from '../../../core/services/client-service/client.service';
import { PaymentMethodService } from '../../../core/services/paymentMethod-service/payment-method.service';
import { PaymentMethod } from '../../../core/models/payment-method';
import { CurrencyFormatDirective } from '../../directives/currency-format.directive';

@Component({
  selector: 'app-change-payment-method-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, A11yModule, CurrencyFormatDirective],
  templateUrl: './change-payment-method-modal.component.html'
})
export class ChangePaymentMethodModalComponent implements OnInit {
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  @Input({ required: true }) clientId!: number;
  @Output() closed = new EventEmitter<void>();
  @Output() saved = new EventEmitter<{ method: PaymentMethod; amount: number; message: string }>();
  context?: PaymentMethodChangeContext;
  methods: PaymentMethod[] = [];
  methodId = 0;
  amount: number | null = null;
  suggestedAmount = 0;
  loading = true;
  saving = false;
  error = '';

  ngOnInit(): void {
    forkJoin({ context: this.clients.getPaymentMethodChangeContext(this.clientId), methods: this.paymentMethods.getPaymentMethods() })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => { this.loading = false; this.cdr.markForCheck(); })
      ).subscribe({
        next: ({ context, methods }) => { this.context = context; this.methods = methods; this.amount = context.amount; },
        error: err => this.error = err.error?.message || 'No se pudieron cargar los datos. Cerrá el modal e intentá nuevamente.'
      });
  }

  constructor(private clients: ClientService, private paymentMethods: PaymentMethodService) {}

  private isCashPaymentMethod(name: string): boolean {
    return name.toLocaleLowerCase('es-AR').includes('efectivo');
  }

  private roundAmountUpForPaymentMethod(amount: number, methodName: string): number {
    if (!Number.isFinite(amount) || amount <= 0) return 0;

    const step = this.isCashPaymentMethod(methodName) ? 1000 : 100;
    const ratio = amount / step;
    // Avoid a binary floating-point artifact turning an exact multiple into
    // the next unit, while preserving the upward rounding used by increases.
    const nearestInteger = Math.round(ratio);
    const units = Math.abs(ratio - nearestInteger) < 1e-9
      ? nearestInteger
      : Math.ceil(ratio);
    return units * step;
  }

  recalculate(): void {
    const method = this.methods.find(m => m.id === Number(this.methodId));
    if (!method || !this.context) return;
    this.error = '';
    if (this.context.commission <= -100 || method.commission <= -100) {
      this.error = 'El porcentaje configurado no permite calcular el abono.';
      this.amount = null;
      return;
    }
    const amountBeforeRounding = this.context.amount /
      (1 + this.context.commission / 100) *
      (1 + method.commission / 100);
    this.suggestedAmount = this.roundAmountUpForPaymentMethod(amountBeforeRounding, method.name);
    this.amount = this.suggestedAmount;
  }

  get valid(): boolean {
    return !!this.context && this.methodId > 0 && this.methodId !== this.context.paymentMethodId &&
      this.amount !== null && Number.isFinite(Number(this.amount)) && Number(this.amount) >= 0 && Number(this.amount) <= 99999999.99;
  }

  save(): void {
    if (!this.valid || this.saving || !this.context) return;
    this.saving = true;
    this.error = '';
    this.clients.changePaymentMethod(this.clientId, this.context, Number(this.methodId), Number(this.amount))
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => { this.saving = false; this.cdr.markForCheck(); })
      ).subscribe({
        next: result => this.saved.emit({ method: this.methods.find(m => m.id === Number(this.methodId))!, amount: Number(this.amount), message: result.message }),
        error: err => this.error = err.error?.message || 'No se pudo confirmar el cambio. Revisá la conexión y volvé a abrir el modal para comprobar el estado.'
      });
  }
}
