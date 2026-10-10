import type { RentalAmountHistoryItem } from '../../core/services/client-service/client.service';

export interface RentalAmountTimelineEntry {
  item: RentalAmountHistoryItem;
  hasPreviousStep: boolean;
  previousAmount: number | null;
  changePercentage: number | null;
}

/** Presentation only: compare chronological amount steps, ignoring payment-method events. */
export function buildRentalAmountTimeline(items: readonly RentalAmountHistoryItem[]): RentalAmountTimelineEntry[] {
  const dateValue = (item: RentalAmountHistoryItem): number => {
    const value = Date.parse(item.startDate.length === 10 ? `${item.startDate}T00:00:00` : item.startDate);
    return Number.isFinite(value) ? value : 0;
  };
  const chronological = (a: RentalAmountHistoryItem, b: RentalAmountHistoryItem): number =>
    dateValue(b) - dateValue(a) || b.id - a.id;
  const steps = items.filter(item => item.status !== 'event').sort(chronological);
  const previous = new Map<RentalAmountHistoryItem, RentalAmountHistoryItem>();
  steps.forEach((item, index) => {
    if (steps[index + 1]) previous.set(item, steps[index + 1]);
  });
  const priority = (item: RentalAmountHistoryItem): number =>
    item.status === 'planned' ? 0 : item.status === 'active' ? 1 : 2;

  return [...items].sort((a, b) => priority(a) - priority(b) || chronological(a, b))
    .map(item => {
      const older = previous.get(item);
      const percentage = older && older.amount > 0
        ? (item.amount - older.amount) / older.amount * 100 : null;
      return {
        item,
        hasPreviousStep: !!older,
        previousAmount: older?.amount ?? null,
        changePercentage: percentage !== null && Number.isFinite(percentage) ? percentage : null
      };
    });
}
