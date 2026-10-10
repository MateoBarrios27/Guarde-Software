import Swal from 'sweetalert2';
import { PaymentDecisionDetails, requestPaymentDecision } from './payment-decision-dialog';

describe('Payment decision dialog', () => {
  const details: PaymentDecisionDetails = {
    code: 'PAYMENT_DECISION_REQUIRED', scenario: 'surplus', message: 'El pago deja un excedente.',
    surplus: 10, remainingDebt: 0, decisionToken: 'token', expectedPaymentStateToken: 'state',
    options: [
      { action: 'credit_next_month', title: 'Dejar el próximo mes pendiente, con saldo a favor', description: 'Dejar saldo anterior a favor',
        debits: [{ month: '2026-11-01T00:00:00', amount: 100 }], resultingBalance: -90, previousBalance: 10, nextPaymentDate: "2026-11-01T00:00:00" },
      { action: 'close_credited_month', title: 'Dar ese mes por pagado y pasar al siguiente', description: 'Conservar deuda anterior',
        debits: [{ month: '2026-11-01T00:00:00', amount: 100 }, { month: '2026-12-01T00:00:00', amount: 100 }], resultingBalance: -190, previousBalance: -90, nextPaymentDate: "2026-12-01T00:00:00" }
    ]
  };
  afterEach(() => Swal.close());
  it('renders proposed month and recommended option as a real radio selector', async () => {
    const result = requestPaymentDecision(details);
    const popup = Swal.getPopup()!;
    expect(popup.textContent).toContain('noviembre de 2026');
    expect(popup.textContent).toContain('Saldo pendiente');
    expect(popup.textContent).toContain('Saldo anterior a favor');
    expect(popup.querySelectorAll('.payment-decision-next-payment')[0].textContent).toContain('11/2026');
    expect(popup.querySelectorAll('.payment-decision-next-payment')[1].textContent).toContain('12/2026');
    expect(popup.querySelectorAll('.payment-decision-outcomes').length).toBe(2);
    expect(popup.querySelector<HTMLInputElement>('input:checked')?.value).toBe('credit_next_month');
    Swal.clickConfirm();
    expect(await result).toBe('credit_next_month');
  });
  it('returns the alternative chosen by the administrative user', async () => {
    const result = requestPaymentDecision(details);
    Swal.getPopup()!.querySelector<HTMLInputElement>('input[value="close_credited_month"]')!.click();
    Swal.clickConfirm();
    expect(await result).toBe('close_credited_month');
  });
  it('shows the partial month as pending and its following month as the alternative', async () => {
    const result = requestPaymentDecision({ ...details, scenario: 'partial_payment', surplus: 0,
      options: [
        { action: 'keep_month_pending', title: 'Este mes todavía se tiene que pagar', description: 'Sin otro débito',
          debits: [], resultingBalance: -50, previousBalance: 0, nextPaymentDate: '2026-10-01T00:00:00' },
        { action: 'close_partial_month', title: 'Tomar este mes como pagado y pasar al siguiente', description: 'Conservar faltante',
          debits: [{ month: '2026-11-01T00:00:00', amount: 100 }], resultingBalance: -150, previousBalance: -50, nextPaymentDate: '2026-11-01T00:00:00' }
      ] });
    const popup = Swal.getPopup()!;
    expect(Swal.getTitle()?.textContent).toContain('¿Este mes sigue pendiente de pago?');
    expect(popup.querySelectorAll('.payment-decision-next-payment')[0].textContent).toContain('10/2026');
    expect(popup.querySelectorAll('.payment-decision-next-payment')[1].textContent).toContain('11/2026');
    expect(popup.querySelector('.payment-decision-summary')).toBeNull();
    popup.querySelector<HTMLInputElement>('input[value="close_partial_month"]')!.click();
    Swal.clickConfirm();
    expect(await result).toBe('close_partial_month');
  });
  it('cancel does not approve a default option', async () => {
    const result = requestPaymentDecision(details);
    Swal.clickCancel();
    expect(await result).toBeNull();
  });
  it('escapes content and blocks confirmation without a valid choice', async () => {
    const result = requestPaymentDecision({ ...details, message: '<img src=x onerror=alert(1)>',
      options: details.options.map(option => ({ ...option, title: '<script>bad()</script>' })) });
    const popup = Swal.getPopup()!;
    expect(popup.querySelector('.payment-decision-body img, .payment-decision-body script')).toBeNull();
    popup.querySelector<HTMLInputElement>('input:checked')!.checked = false;
    Swal.clickConfirm();
    await new Promise<void>(resolve => setTimeout(resolve, 0));
    expect(Swal.getValidationMessage()?.textContent).toContain('Seleccioná una opción');
    Swal.clickCancel();
    expect(await result).toBeNull();
  });
});
