import { Component, EventEmitter, HostListener, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { A11yModule } from '@angular/cdk/a11y';
import { firstValueFrom } from 'rxjs';
import { CashReceivablesService, Receivable, ReceivableInput, ReceivablePayment } from '../../../core/services/cash-service/cash-receivables.service';
import { CurrencyFormatDirective } from '../../../shared/directives/currency-format.directive';
import { DeleteConfirmationService } from '../../../shared/services/delete-confirmation.service';

@Component({
  selector: 'app-cash-receivables', standalone: true,
  imports: [CommonModule, FormsModule, CurrencyFormatDirective, A11yModule],
  templateUrl: './cash-receivables.component.html',
  styleUrls: ['./cash-receivables.component.css']
})
export class CashReceivablesComponent implements OnInit {
  @Output() closed = new EventEmitter<void>();
  accounts: Receivable[] = [];
  selectedId: number | null = null;
  filter = 'pending';
  search = '';
  loading = false;
  busy = false;
  error = '';
  notice = '';
  loadFailed = false;
  editing = false;
  editingId: number | null = null;
  form = this.emptyAccount();
  payment = this.emptyPayment();
  private paymentAttempt: { key: string; requestId: string } | null = null;

  constructor(private service: CashReceivablesService, public confirmation: DeleteConfirmationService) {}
  ngOnInit(): void { void this.load(); }
  private today(): string {
    const now = new Date();
    return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
  }
  private emptyAccount() { return { description: '', date: this.today(), totalAmount: null as number | null, notes: '' }; }
  private emptyPayment() { return { date: this.today(), amount: null as number | null, comment: '' }; }
  get selected(): Receivable | undefined { return this.accounts.find(a => a.id === this.selectedId); }
  get visible(): Receivable[] {
    const query = this.search.trim().toLocaleLowerCase('es');
    return this.accounts.filter(a =>
      (this.filter === 'all' || (this.filter === 'pending' ? a.remainingAmount > 0 : a.remainingAmount === 0)) &&
      a.description.toLocaleLowerCase('es').includes(query));
  }
  get totalPending(): number { return this.accounts.reduce((sum, a) => sum + a.remainingAmount, 0); }
  get totalCollected(): number { return this.accounts.reduce((sum, a) => sum + a.paidAmount, 0); }
  progress(account: Receivable): number { return Math.min(100, Math.max(0, account.paidAmount / account.totalAmount * 100)); }
  async load(): Promise<void> {
    if (this.loading) return;
    this.loading = true; this.error = '';
    try {
      this.accounts = await firstValueFrom(this.service.getAll()); this.loadFailed = false;
      if (!this.selected) this.selectedId = null;
    } catch (error) {
      this.loadFailed = true;
      this.showError(error, 'No se pudieron cargar las cuentas a cobrar. Reintentá para ver los saldos actualizados.');
    } finally { this.loading = false; }
  }
  select(account: Receivable): void {
    if (this.busy || this.loading) return;
    this.selectedId = account.id; this.editing = false;
    this.payment = this.emptyPayment(); this.paymentAttempt = null; this.error = ''; this.notice = '';
  }
  startEdit(account?: Receivable): void {
    this.editing = true; this.editingId = account?.id ?? null;
    this.form = account ? { description: account.description, date: account.date.slice(0, 10), totalAmount: account.totalAmount, notes: account.notes } : this.emptyAccount();
    this.error = ''; this.notice = '';
  }
  close(): void { if (!this.busy && !this.confirmation.confirmation()) this.closed.emit(); }
  @HostListener('document:keydown.escape') escape(): void { this.close(); }
  private validMoney(value: number | null): value is number {
    return value !== null && Number.isFinite(value) && value > 0 && value <= 999999999999.99 && Math.abs(value * 100 - Math.round(value * 100)) < 0.01;
  }
  private showError(error: any, fallback: string): void {
    const validation = error?.error?.errors;
    this.error = error?.error?.message || (validation ? Object.values(validation).flat().join(' ') : fallback);
  }
  async saveAccount(): Promise<void> {
    if (this.busy || this.loading || this.loadFailed) return;
    if (!this.form.description.trim() || !this.form.date || !this.validMoney(this.form.totalAmount)) {
      this.error = 'Completá persona y concepto, fecha y un total mayor a cero (hasta dos decimales).'; return;
    }
    const input: ReceivableInput = { ...this.form, totalAmount: this.form.totalAmount, description: this.form.description.trim() };
    this.busy = true; this.error = ''; this.notice = '';
    try {
      const id = this.editingId;
      const result = await firstValueFrom(id === null ? this.service.create(input) : this.service.update(id, input));
      this.selectedId = id ?? result; this.editing = false; this.filter = 'all'; this.search = '';
      this.payment = this.emptyPayment(); this.paymentAttempt = null; this.notice = 'Cuenta guardada.';
      await this.load();
    } catch (error) { this.showError(error, 'No se pudo guardar la cuenta.'); }
    finally { this.busy = false; }
  }
  async savePayment(): Promise<void> {
    const account = this.selected;
    if (!account || this.busy || this.loading || this.loadFailed) return;
    if (!this.validMoney(this.payment.amount) || !this.payment.date) { this.error = 'Ingresá una fecha y un importe mayor a cero (hasta dos decimales).'; return; }
    if (this.payment.amount > account.remainingAmount) { this.error = 'El pago supera el saldo pendiente.'; return; }
    if (this.payment.date < account.date.slice(0, 10)) { this.error = 'El pago no puede ser anterior a la fecha de la cuenta.'; return; }
    const input = { date: this.payment.date, amount: this.payment.amount, comment: this.payment.comment.trim() };
    const key = JSON.stringify({ id: account.id, ...input });
    if (this.paymentAttempt?.key !== key) this.paymentAttempt = { key, requestId: this.newRequestId() };
    this.busy = true; this.error = ''; this.notice = '';
    try {
      await firstValueFrom(this.service.addPayment(account.id, { ...input, requestId: this.paymentAttempt.requestId }));
      this.payment = this.emptyPayment(); this.paymentAttempt = null; this.notice = 'Pago registrado.'; await this.load();
    } catch (error) { this.showError(error, 'No se pudo confirmar el pago. Podés reintentar el mismo pago sin duplicarlo.'); }
    finally { this.busy = false; }
  }
  // Works on HTTP LAN deployments where crypto.randomUUID is unavailable.
  private newRequestId(): string {
    const bytes = crypto.getRandomValues(new Uint8Array(16));
    bytes[6] = (bytes[6] & 15) | 64; bytes[8] = (bytes[8] & 63) | 128;
    const hex = Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('');
    return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
  }
  async deleteAccount(): Promise<void> {
    const account = this.selected;
    if (!account || this.busy || this.loading || this.loadFailed || account.payments.length) return;
    try {
      if (!await this.confirmation.confirm({ message: 'Esta acción eliminará la cuenta a cobrar', highlightedText: account.description, confirmText: 'Eliminar cuenta' })) return;
      this.busy = true;
      await firstValueFrom(this.service.delete(account.id)); this.selectedId = null;
      this.notice = 'Cuenta eliminada.'; await this.load();
    } catch (error) { this.showError(error, 'No se pudo eliminar la cuenta.'); }
    finally { this.busy = false; }
  }
  async deletePayment(payment: ReceivablePayment): Promise<void> {
    const account = this.selected;
    if (!account || this.busy || this.loading || this.loadFailed) return;
    try {
      if (!await this.confirmation.confirm({ message: 'Esta acción eliminará el pago y volverá a sumarlo al saldo pendiente', highlightedText: `$ ${payment.amount.toLocaleString('es-AR', { minimumFractionDigits: 2 })}`, confirmText: 'Eliminar pago' })) return;
      this.busy = true;
      await firstValueFrom(this.service.deletePayment(account.id, payment.id));
      this.notice = 'Pago eliminado. Saldo actualizado.'; await this.load();
    } catch (error) { this.showError(error, 'No se pudo eliminar el pago.'); }
    finally { this.busy = false; }
  }
}
