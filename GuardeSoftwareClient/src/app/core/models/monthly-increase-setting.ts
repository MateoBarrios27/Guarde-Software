export interface MonthlyIncreaseSetting {
  id: number; // O puedes mapearlo a 'id' en el servicio
  effectiveDate: Date;
  percentage: number;
  createdAt?: Date;
}