/** Configuración de intereses de un mes (Configuración → Intereses por mora). */
export interface MonthlyInterestSetting {
  id: number;
  /** Primer día del mes, `YYYY-MM-DD…` tal como lo envía la API. */
  effectiveDate: string;
  percentage: number;
  /** Día del mes en que corre el job de intereses. */
  applyDay: number;
  /** Último día para pagar sin recargo (applyDay - 1). */
  cutoffDay: number;
  /** El mes ya se aplicó o ya llegó su día: no se puede editar ni borrar. */
  isLocked: boolean;
  createdAt?: string;
  updatedAt?: string | null;
}

/** Mes en que el job ya aplicó intereses. */
export interface MonthlyInterestRun {
  period: string;
  appliedAt: string;
  percentage: number;
  applyDay: number;
  usedDefault: boolean;
  affectedRentals: number;
  totalAmount: number;
}

export interface MonthlyInterestOverview {
  defaultPercentage: number;
  defaultApplyDay: number;
  /** Fecha del servidor con la que se calcularon los bloqueos (yyyy-MM-dd). */
  today: string;
  settings: MonthlyInterestSetting[];
  runs: MonthlyInterestRun[];
}

export interface CreateMonthlyInterestDto {
  /** `YYYY-MM` */
  effectiveDate: string;
  percentage: number;
  applyDay: number;
}

export interface UpdateMonthlyInterestDto {
  percentage: number;
  applyDay: number;
}
