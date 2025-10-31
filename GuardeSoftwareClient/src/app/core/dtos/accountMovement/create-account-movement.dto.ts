export interface CreateAccountMovementDTO {
  clientId: number;
  movementType: 'DEBITO' | 'CREDITO';
  amount: number;
  concept: string;
}
