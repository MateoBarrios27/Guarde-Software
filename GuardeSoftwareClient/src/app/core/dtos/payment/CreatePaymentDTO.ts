
export interface CreatePaymentDTO{
    clientId: number;
    rentalId: number;
    movementType: string;
    concept: string;
    amount: number;
}