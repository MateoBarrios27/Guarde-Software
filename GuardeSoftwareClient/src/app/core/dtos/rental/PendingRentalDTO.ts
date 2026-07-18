export interface PendingRentalDTO{
    id: number;
    clientId: number;
    clientName: string;
    paymentIdentifier: number;
    monthsUnpaid: number;
    balance: number;
    previousBalance?: number;
    currentRent: number;
    pendingAmount: number;
    isPending: boolean;
    lockerIdentifiers: string;
    preferredPayment: number;
    increaseAnchorDate: number;
    pendingSurcharge: number;
    interestAmount?: number;
    nextPaymentDay?: Date | string | null;
}
