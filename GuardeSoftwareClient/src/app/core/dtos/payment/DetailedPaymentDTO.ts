
export interface DetailedPaymentDTO{
    movementId: number;
    paymentId: number;
    clientName: string;
    paymentIdentifier: string;
    amount: number;
    paymentDate: Date;
    paymentMethodName: string;
    concept: string;
    movementType: string;
    preferredPayment: number;
}
