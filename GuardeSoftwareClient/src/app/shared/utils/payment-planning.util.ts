import {
  AppliedPaymentPlanningIncrease,
  PaymentPlanningContext,
  PaymentPlanningMonth
} from '../../core/dtos/accountMovement/payment-planning.dto';

const MONTHS = [
  'enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio',
  'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'
];

export function buildPaymentPlanningBreakdown(
  context: PaymentPlanningContext,
  appliedIncreases: AppliedPaymentPlanningIncrease[],
  chargeHalfSixthMonth: boolean
): PaymentPlanningMonth[] {
  const [year, month] = context.startDate.slice(0, 10).split('-').map(Number);
  const increases = new Map(appliedIncreases.map(item => [item.year * 100 + item.month, item]));
  let currentRent = Number(context.baseRent || 0);

  return Array.from({ length: context.months }, (_, index) => {
    const date = new Date(year, month - 1 + index, 1);
    const itemYear = date.getFullYear();
    const itemMonth = date.getMonth() + 1;
    const increase = increases.get(itemYear * 100 + itemMonth);
    if (!context.isPriceLocked && increase) currentRent = increase.newRentAmount;

    const isHalfPromotion = context.hasSixMonthPromotion && chargeHalfSixthMonth && index === 5;
    const amount = isHalfPromotion ? Math.round((currentRent / 2) * 100) / 100 : currentRent;

    return {
      year: itemYear,
      month: itemMonth,
      label: `${MONTHS[itemMonth - 1]} ${itemYear}`,
      amount,
      isHalfPromotion
    };
  });
}

/** Round the monetary amount once, without using a rounded display percentage as a minimum. */
export function roundRentToPaymentStep(targetAmount: number, preferredPaymentMethod: string): number {
  const step = preferredPaymentMethod.toLocaleLowerCase().includes('efectivo') ? 1000 : 100;
  // Account amounts have cent precision. Normalize before ceiling so floating-point
  // noise at an exact payment step cannot add another hundred/thousand pesos.
  const targetCents = Math.round(targetAmount * 100);
  return Math.ceil(targetCents / (step * 100)) * step;
}

export function roundPlannedRent(
  baseRent: number,
  percentage: number,
  preferredPaymentMethod: string
): number {
  if (percentage <= 0) return baseRent;
  return roundRentToPaymentStep(baseRent * (1 + percentage / 100), preferredPaymentMethod);
}
