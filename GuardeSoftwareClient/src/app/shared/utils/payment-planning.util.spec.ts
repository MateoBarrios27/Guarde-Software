import {
  buildPaymentPlanningBreakdown,
  roundPlannedRent,
  roundRentToPaymentStep
} from './payment-planning.util';
import { PaymentPlanningContext } from '../../core/dtos/accountMovement/payment-planning.dto';

function context(overrides: Partial<PaymentPlanningContext> = {}): PaymentPlanningContext {
  return {
    clientId: 1,
    months: 6,
    startDate: '2026-08-01T00:00:00',
    endDate: '2027-01-01T00:00:00',
    baseRent: 100000,
    increaseFrequencyMonths: 4,
    increaseAnchorDate: '2026-09-01T00:00:00',
    hasSixMonthPromotion: false,
    isPriceLocked: false,
    increases: [],
    ...overrides
  };
}

describe('payment planning utilities', () => {
  it('applies two staged increases to their month and every following month', () => {
    const result = buildPaymentPlanningBreakdown(context({ months: 5 }), [
      { year: 2026, month: 9, percentage: 10, newRentAmount: 110000 },
      { year: 2026, month: 12, percentage: 9.0909, newRentAmount: 120000 }
    ], false);

    expect(result.map(item => item.amount)).toEqual([100000, 110000, 110000, 110000, 120000]);
  });

  it('charges half of only the sixth month for a promotional client', () => {
    const result = buildPaymentPlanningBreakdown(context({ hasSixMonthPromotion: true }), [], true);

    expect(result.map(item => item.amount)).toEqual([100000, 100000, 100000, 100000, 100000, 50000]);
    expect(result[5].isHalfPromotion).toBeTrue();
  });

  it('allows the sixth-month promotion to be disabled manually', () => {
    const result = buildPaymentPlanningBreakdown(context({ hasSixMonthPromotion: true }), [], false);

    expect(result.map(item => item.amount)).toEqual([100000, 100000, 100000, 100000, 100000, 100000]);
    expect(result.some(item => item.isHalfPromotion)).toBeFalse();
  });

  it('keeps the base rent throughout a locked six-month period', () => {
    const result = buildPaymentPlanningBreakdown(context({ isPriceLocked: true }), [
      { year: 2026, month: 9, percentage: 10, newRentAmount: 110000 }
    ], false);

    expect(result.every(item => item.amount === 100000)).toBeTrue();
  });

  it('uses the same cash and electronic rounding steps as finances', () => {
    expect(roundPlannedRent(100000, 10.01, 'Efectivo')).toBe(111000);
    expect(roundPlannedRent(100000, 10.01, 'Transferencia')).toBe(110100);
    expect(roundPlannedRent(100000, 0, 'Efectivo')).toBe(100000);
  });
  it('does not jump to the next step because of binary noise at an exact amount', () => {
    expect(roundPlannedRent(100000, 10, 'Efectivo')).toBe(110000);
    expect(roundPlannedRent(100000, 10, 'Transferencia')).toBe(110000);
    expect(roundRentToPaymentStep(110000.00000000001, 'Efectivo')).toBe(110000);
    expect(roundRentToPaymentStep(110000.01, 'Efectivo')).toBe(111000);
    expect(roundRentToPaymentStep(110000.01, 'Transferencia')).toBe(110100);
  });
  it('rounds an edited amount once and remains stable when repeated', () => {
    for (const method of ['Efectivo', 'Transferencia']) {
      const rent = roundRentToPaymentStep(248001, method);
      for (let repeat = 0; repeat < 10; repeat++) {
        expect(roundRentToPaymentStep(rent, method)).toBe(rent);
      }
    }
  });

});
