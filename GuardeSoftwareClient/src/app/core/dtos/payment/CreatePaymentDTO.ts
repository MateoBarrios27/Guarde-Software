
export interface CreatePaymentDTO{
    clientId: number;
    movementType: string;
    concept: string;
    amount: number;
    paymentMethodId: number;
    date: Date;
    isAdvancePayment: boolean;
    advanceMonths?: number;
    commissionAmount?: number;
    commissionConcept?: string;
    increasePercentage?: number | null;
}
