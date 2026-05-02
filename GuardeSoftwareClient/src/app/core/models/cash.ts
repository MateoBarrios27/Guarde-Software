export interface CashFlowItem {
  id?: number;
  date?: string;
  description: string;
  comment?: string;
  depo: number;
  casa: number;
  isPaid: boolean;
  retiros: number;
  extras: number;
  isConfirmed?: boolean;
  displayOrder?: number;
  replicationState: number;
}

export interface MonthlySummary {
  totalSystemIncome: number;    
  totalAdvancePayments: number; 
  totalManualExpenses: number;  
  netBalance: number;         
  pendingCollection: number; 
  abono: number; 
  ivaFacturaA: number;
  ivaFacturaB: number;
}

export interface FinancialAccount {
  id: number;
  name: string;
  type: string; 
  currency: string;
  balance: number;
  displayOrder: number;
  color?: string;
}

