import { WarehouseRevenueDTO } from "./WarehouseRevenueDto";

export interface MonthlyStatisticsDTO {
  year: number;
  month: number;
  
  totalPagado: number;      
  totalAlquileres: number;   
  totalIntereses: number;   
  deudaTotalDelMes: number;
  balanceGlobalActual: number;
  totalEspaciosOcupados: number;
  totalAdvancePayments: number;
  totalIvaFacturaA: number;
  totalIvaFacturaB: number;
  warehouseRevenues?: WarehouseRevenueDTO[];
}