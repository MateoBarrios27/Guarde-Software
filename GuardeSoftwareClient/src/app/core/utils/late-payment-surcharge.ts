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
  lateRentBase: number
): LatePaymentSurchargeProjection {
  const interestsAtCutoff = positive(unpaidInterestsAtCutoff);
  const rentBase = positive(lateRentBase);
  const taxableBase = rentBase + interestsAtCutoff;

  return {
    unpaidInterestsAtCutoff: interestsAtCutoff,
    lateRentBase: rentBase,
    taxableBase,
    surchargeAmount: Math.floor((taxableBase * 0.10) / 100) * 100
  };
}

function positive(value: number): number {
  const normalized = Number(value);
  return Number.isFinite(normalized) ? Math.max(0, normalized) : 0;
}
