import { calculateInterest, DEFAULT_INTEREST_PERCENTAGE } from './interest-rule';

export interface LatePaymentSurchargeProjection {
  unpaidInterestsAtCutoff: number;
  lateRentBase: number;
  taxableBase: number;
  surchargeAmount: number;
}

/**
 * The day-10 cutoff freezes both the current overdue rent and every interest
 * that was still unpaid. A later payment can cancel those components in the
 * ledger, but it does not reduce the penalty already caused by paying them late.
 */
export function projectLatePaymentSurcharge(
  unpaidInterestsAtCutoff: number,
  lateRentBase: number,
  percentage: number = DEFAULT_INTEREST_PERCENTAGE
): LatePaymentSurchargeProjection {
  const interestsAtCutoff = positive(unpaidInterestsAtCutoff);
  const rentBase = positive(lateRentBase);
  const taxableBase = rentBase + interestsAtCutoff;

  return {
    unpaidInterestsAtCutoff: interestsAtCutoff,
    lateRentBase: rentBase,
    taxableBase,
    surchargeAmount: calculateInterest(taxableBase, percentage)
  };
}

function positive(value: number): number {
  const normalized = Number(value);
  return Number.isFinite(normalized) ? Math.max(0, normalized) : 0;
}
