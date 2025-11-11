export interface CreateMonthlyIncreaseDto {
  //Formato "YYYY-MM" (ej: "2025-11")
  effectiveDate: string;
  percentage: number;
}