// Next-month projection for the Clients table. Same sign convention as TableClient
// (negative = debt, positive = credit) and the same figures the projected account
// statement sends.
export interface ClientProjectedNextMonthItem {
  clientId: number;
  previousBalance: number;
  interestAmount: number;
  currentRent: number;
  balance: number;
  // Total the projected statement asks the client to pay (includes the identifier cents).
  statementTotal: number;
  // Increase (%) already included in currentRent; null/absent when none falls in the month.
  increasePercentage?: number | null;
  // An increase is due but the month has no percentage in Configuración: currentRent is the current rent.
  increasePercentageMissing?: boolean;
}

export interface ClientProjectedNextMonthResult {
  targetMonth: string;
  generatedAt: string;
  items: ClientProjectedNextMonthItem[];
  failedClientIds: number[];
}
