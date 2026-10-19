export interface MovementStatisticsDTO {
  movementId: number;
  rentalId: number;
  movementDate: string;
  previousBalance: number;
  interestAmount: number;
  currentRent: number | null;
  balance: number;
}

export interface AccountMovementDTO {
  id: number;
  rentalId: number;
  movementDate: Date;
  movementType: 'DEBITO' | 'CREDITO';
  concept: string;
  amount: number;
  paymentId?: number; // Es nullable en la BD
}
