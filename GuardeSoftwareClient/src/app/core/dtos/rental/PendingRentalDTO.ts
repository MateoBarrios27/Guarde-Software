export interface PendingRentalDTO{
    id: number;
    clientId: number;
    clientName: string;
    paymentIdentifier: number;
    monthsUnpaid: number;
    balance: number;
    currentRent: number;
    pendingAmount: number;
    isPending: boolean;
    lockerIdentifiers: string;
}
