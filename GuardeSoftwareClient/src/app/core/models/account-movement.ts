
export interface AccountMovement {
    rentalId: number;
    movementDate: Date;
    movementType: string;
    concept?: string;
    amount: number;
    paymentId?: number;
}
