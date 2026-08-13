export interface PaymentPlanningIncrease {
  year: number;
  month: number;
  label: string;
}

export interface AppliedPaymentPlanningIncrease {
  year: number;
  month: number;
  percentage: number;
  newRentAmount: number;
}

export interface PaymentPlanningContext {
  clientId: number;
  months: number;
  startDate: string;
  endDate: string;
  baseRent: number;
  increaseFrequencyMonths: number;
  increaseAnchorDate?: string | null;
  hasSixMonthPromotion: boolean;
  isPriceLocked: boolean;
  increases: PaymentPlanningIncrease[];
}

export interface PlanClientPaymentRequest {
  clientId: number;
  months: number;
  chargeHalfSixthMonth: boolean;
  appliedIncreases: AppliedPaymentPlanningIncrease[];
}

export interface PaymentPlanningMonth {
  year: number;
  month: number;
  label: string;
  amount: number;
  isHalfPromotion: boolean;
}

export interface PlannedPaymentResult {
  createdDebits: number;
  totalAmount: number;
  startDate: string;
  endDate: string;
  isPriceLocked: boolean;
  months: PaymentPlanningMonth[];
}
