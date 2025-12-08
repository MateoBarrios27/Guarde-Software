export interface MonthlyStatisticsDTO {
  year: number;
  month: number;
  
  totalPagado: number;      
  totalAlquileres: number;   
  totalIntereses: number;   
  deudaTotalDelMes: number;
  balanceGlobalActual: number;
  totalEspaciosOcupados: number;
}