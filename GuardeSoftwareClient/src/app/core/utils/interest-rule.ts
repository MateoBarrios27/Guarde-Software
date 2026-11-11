/**
 * Interés por mora del mes (espejo de GuardeSoftwareAPI/Utilities/InterestRule.cs).
 * Sin configuración rige el 10 % aplicado el día 11 (plazo hasta el 10).
 */
export const DEFAULT_INTEREST_PERCENTAGE = 10;
export const DEFAULT_INTEREST_APPLY_DAY = 11;

export interface MonthlyInterestSettingLike {
  effectiveDate: string;
  percentage: number;
  applyDay: number;
}

export interface MonthlyInterestRule {
  percentage: number;
  applyDay: number;
  cutoffDay: number;
  isDefault: boolean;
}

export function resolveInterestRule(
  settings: readonly MonthlyInterestSettingLike[] | null | undefined,
  year: number,
  month: number,
  defaults = { percentage: DEFAULT_INTEREST_PERCENTAGE, applyDay: DEFAULT_INTEREST_APPLY_DAY }
): MonthlyInterestRule {
  const key = `${year}-${String(month).padStart(2, '0')}`;
  const setting = settings?.find(item => String(item.effectiveDate ?? '').slice(0, 7) === key);
  const percentage = setting ? Number(setting.percentage) : defaults.percentage;
  const applyDay = setting ? Number(setting.applyDay) : defaults.applyDay;
  return { percentage, applyDay, cutoffDay: applyDay - 1, isDefault: !setting };
}

/** Mismo redondeo que el backend: se trunca a la centena. */
export function calculateInterest(taxableBase: number, percentage: number): number {
  if (!(taxableBase > 0) || !(percentage > 0)) return 0;
  const amount = Math.round(taxableBase * percentage) / 100; // centavos exactos antes de truncar
  return Math.floor(amount / 100) * 100;
}
