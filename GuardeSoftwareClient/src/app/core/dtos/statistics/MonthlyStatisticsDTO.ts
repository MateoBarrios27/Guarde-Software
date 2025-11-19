export interface MonthlyStatisticsDTO {
  year: number;
  month: number;
  
  // Las métricas que pediste
  totalAbonado: number;      // "ABONO" o "Cobrado"
  totalSaldo: number;        // "SALDO" (Deuda actual total al cierre del mes)
  totalInteres: number;      // "INTERES" generado ese mes
  totalSaldoAnterior: number; // "SALDO ANTERIOR" (Deuda que venía del mes pasado)
  totalFacturado: number;    // Lo que se generó para cobrar ese mes (Alquileres)
}