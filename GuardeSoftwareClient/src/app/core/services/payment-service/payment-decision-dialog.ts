import Swal from 'sweetalert2';
import { UiAlertService } from '../../../shared/services/ui-alert.service';

export interface PaymentDecisionOption {
  action: string;
  title: string;
  description: string;
  debits: { month: string; amount: number }[];
  resultingBalance: number;
  previousBalance: number;
  nextPaymentDate: string;
}
export interface PaymentDecisionDetails {
  code: string;
  scenario: string;
  message: string;
  surplus: number;
  remainingDebt: number;
  decisionToken: string;
  expectedPaymentStateToken: string;
  options: PaymentDecisionOption[];
}
const escapeHtml = (value: string): string => value.replace(/[&<>"']/g, character =>
  ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[character]!));
const money = (value: number): string => new Intl.NumberFormat('es-AR',
  { style: 'currency', currency: 'ARS' }).format(value);

export async function requestPaymentDecision(details: PaymentDecisionDetails): Promise<string | null> {
  const title = details.scenario === 'partial_payment' ? '¿Este mes sigue pendiente de pago?'
    : details.scenario === 'advance_shortfall' ? 'Revisá los meses del adelanto'
    : details.scenario === 'missing_rent' ? 'Revisá el débito del alquiler'
    : details.scenario === 'surplus' ? '¿El próximo mes con saldo a favor se da por pagado?'
    : 'Revisá el excedente del adelanto';
  const cards = details.options.map((option, index) => {
    const debitRows = option.debits.map(debit => `<li><span>${escapeHtml(new Intl.DateTimeFormat('es-AR',
      { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(new Date(debit.month)))}</span><strong>${money(debit.amount)}</strong></li>`).join('');
    const nextPayment = new Intl.DateTimeFormat('es-AR', { month: '2-digit', year: 'numeric', timeZone: 'UTC' })
      .format(new Date(option.nextPaymentDate));
    const balanceLabel = option.resultingBalance < 0 ? 'Saldo pendiente' : 'Saldo a favor';
    return `<label class="payment-decision-card"><input type="radio" name="payment-decision" value="${escapeHtml(option.action)}" ${index === 0 ? 'checked' : ''}>
      <div class="payment-decision-card-content"><div class="payment-decision-card-heading">
      <div class="payment-decision-card-title">${escapeHtml(option.title)}</div>
      <div class="payment-decision-next-payment"><span>Próx. pago</span><strong>${escapeHtml(nextPayment)}</strong></div></div>
      <p>${escapeHtml(option.description)}</p>
      <ul class="payment-decision-debits">${debitRows || '<li>No se crean nuevos alquileres.</li>'}</ul>
      <div class="payment-decision-outcomes"><div class="payment-decision-outcome"><span>Saldo anterior${option.previousBalance > 0 ? ' a favor' : ''}</span><strong>${money(option.previousBalance)}</strong></div>
      <div class="payment-decision-outcome payment-decision-total"><span>${balanceLabel}</span><strong>${money(option.resultingBalance)}</strong></div></div></div></label>`;
  }).join('');
  const result = await UiAlertService.fire({
    title,
    html: `<div class="payment-decision-body"><p>${escapeHtml(details.message)}</p>
      ${details.surplus > 0 ? `<div class="payment-decision-summary"><span>Excedente disponible</span><strong>${money(details.surplus)}</strong></div>` : ''}
      <fieldset class="payment-decision-options"><legend>Elegí qué hacer antes de registrar el pago</legend>${cards}</fieldset>
      <p class="payment-decision-note">Los movimientos existentes se conservan. Todavía no se registró este pago.</p></div>`,
    width: 760,
    showCancelButton: true,
    confirmButtonText: 'Confirmar y registrar pago',
    cancelButtonText: 'Volver al pago',
    confirmButtonColor: '#2563eb',
    cancelButtonColor: '#64748b',
    allowOutsideClick: false,
    customClass: { popup: 'payment-decision-popup' },
    preConfirm: () => {
      const action = Swal.getPopup()?.querySelector<HTMLInputElement>('input[name="payment-decision"]:checked')?.value;
      if (!details.options.some(option => option.action === action)) {
        Swal.showValidationMessage('Seleccioná una opción para continuar.');
        return false;
      }
      return action;
    }
  });
  return result.isConfirmed ? result.value ?? null : null;
}
