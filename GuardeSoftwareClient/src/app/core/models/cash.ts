export interface CashFlowItem {
  id?: number; // Opcional para nuevos
  date: string; // YYYY-MM-DD
  description: string;
  depo: number;
  casa: number;
  pagado: number;
  retiros: number;
  extras: number;
  isConfirmed?: boolean;
}

export interface MonthlySummary {
  totalSystemIncome: number;    // Recaudación automática del sistema
  totalAdvancePayments: number; // Pagos adelantados
  totalManualExpenses: number;  // Suma de gastos manuales (calculado en back o front)
  netBalance: number;           // Ganancia Neta
  pendingCollection: number;    // Deuda de clientes
}

export interface FinancialAccount {
  id: number;
  name: string;
  type: string; // 'Banco', 'Fisico', etc.
  currency: string;
  balance: number;
}