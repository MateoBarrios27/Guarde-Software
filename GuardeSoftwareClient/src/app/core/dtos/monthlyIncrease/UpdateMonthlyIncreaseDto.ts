export interface UpdateMonthlyIncreaseDto {
  percentage: number;
  userId?: number; // El backend debería tomarlo del token
}