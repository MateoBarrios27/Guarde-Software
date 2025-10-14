
export interface DetailedPaymentDTO{
    paymentId: number;
    clientName: string;
    paymentIdentifier: string;
    lockerIdentifier: string;
    amount: number;
    paymentDate: Date;
    paymentMethodName: string;
    concept: string;
    movementType: string;
}
