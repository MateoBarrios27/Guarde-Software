export interface LatePaymentSurchargeProjection {
  priorRentBeforePayment: number;
  unpaidInterestsBeforePayment: number;
  unpaidInterestsAfterPayment: number;
  lateRentBase: number;
  taxableBase: number;
  surchargeAmount: number;
}

/**
 * Mirrors the part of PaymentAllocationEngine that matters for a late fee:
 * prior rent is paid first, then existing interests, then the current rent.
 * The late rent base remains taxable even when this payment cancels it; only
 * interests that remain unpaid after applying the payment are compounded.
 */
export function projectLatePaymentSurcharge(
  priorRentBeforePayment: number,
  unpaidInterestsBeforePayment: number,
  lateRentBase: number,
  paymentAvailableForDebt: number
): LatePaymentSurchargeProjection {
  const priorRent = positive(priorRentBeforePayment);
  const interestsBefore = positive(unpaidInterestsBeforePayment);
  const rentBase = positive(lateRentBase);
  const payment = positive(paymentAvailableForDebt);

  const paymentAvailableForInterests = Math.max(0, payment - priorRent);
  const interestsPaid = Math.min(interestsBefore, paymentAvailableForInterests);
  const interestsAfter = Math.max(0, interestsBefore - interestsPaid);
  const taxableBase = rentBase + interestsAfter;

  return {
    priorRentBeforePayment: priorRent,
    unpaidInterestsBeforePayment: interestsBefore,
    unpaidInterestsAfterPayment: interestsAfter,
    lateRentBase: rentBase,
    taxableBase,
    surchargeAmount: Math.floor((taxableBase * 0.10) / 100) * 100
  };
}

function positive(value: number): number {
  const normalized = Number(value);
  return Number.isFinite(normalized) ? Math.max(0, normalized) : 0;
}
