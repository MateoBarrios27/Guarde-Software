export interface CashFlowItem {
  id?: number;
  date: string;
  description: string;
  comment?: string;
  depo: number;
  casa: number;
  isPaid: boolean;
  retiros: number;
  extras: number;
  isConfirmed?: boolean;
}

export interface MonthlySummary {
  totalSystemIncome: number;    
  totalAdvancePayments: number; 
  totalManualExpenses: number;  
  netBalance: number;         
  pendingCollection: number;  
}

export interface FinancialAccount {
  id: number;
  name: string;
  type: string; 
  currency: string;
  balance: number;
}