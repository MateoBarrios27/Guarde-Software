import { buildRentalAmountTimeline } from './rental-amount-timeline.util';
import type { RentalAmountHistoryItem } from '../../core/services/client-service/client.service';

const step = (id: number, amount: number, startDate: string, status: RentalAmountHistoryItem['status']): RentalAmountHistoryItem => ({ id, amount, startDate, status });

describe('rental amount timeline', () => {
  it('puts every planned step above active, independent of the API order', () => {
    const data = [step(3,150,'2026-10-01','active'),step(4,165,'2026-11-01','planned'),step(1,100,'2026-05-01','past'),step(5,181.5,'2027-01-01','planned')];
    expect(buildRentalAmountTimeline(data).map(e=>e.item.id)).toEqual([5,4,3,1]);
  });
  it('compares chronological steps, ignoring payment method events', () => {
    const data = [step(-7,150,'2026-10-09','event'),step(3,150,'2026-10-01','active'),step(2,125,'2026-09-01','past'),step(1,100,'2026-05-01','past')];
    const entries=buildRentalAmountTimeline(data);
    expect(entries.find(e=>e.item.id===3)?.changePercentage).toBe(20);
    expect(entries.find(e=>e.item.id===2)?.changePercentage).toBe(25);
    expect(entries.find(e=>e.item.id===-7)?.hasPreviousStep).toBeFalse();
  });
  it('uses start date and ID for a deterministic order on the same date', () => {
    const data=[step(4,120,'2026-10-01','active'),step(3,100,'2026-10-01','past')];
    expect(buildRentalAmountTimeline(data)[0].changePercentage).toBe(20);
  });
  it('handles reductions and equal amounts without labeling them as increases', () => {
    const entries=buildRentalAmountTimeline([step(1,100,'2026-01-01','past'),step(2,80,'2026-02-01','past'),step(3,80,'2026-03-01','active')]);
    expect(entries[0].changePercentage).toBe(0);
    expect(entries[1].changePercentage).toBe(-20);
  });
  it('avoids division by zero and leaves the initial step without a comparison', () => {
    const entries=buildRentalAmountTimeline([step(1,0,'2026-01-01','past'),step(2,100,'2026-02-01','active')]);
    expect(entries[0].hasPreviousStep).toBeTrue();
    expect(entries[0].previousAmount).toBe(0);
    expect(entries[0].changePercentage).toBeNull();
    expect(entries[1].hasPreviousStep).toBeFalse();
  });
  it('preserves precision until display formatting', () => {
    const entries=buildRentalAmountTimeline([step(1,90000,'2026-01-01','past'),step(2,100000,'2026-02-01','active')]);
    expect(entries[0].changePercentage).toBeCloseTo(11.111111, 5);
  });
  it('does not mutate the API array or its records', () => {
    const first=Object.freeze(step(1,100,'2026-01-01','past'));
    const second=Object.freeze(step(2,120,'2026-02-01','active'));
    const input=Object.freeze([first, second]);
    const entries=buildRentalAmountTimeline(input);
    expect(input.map(item=>item.id)).toEqual([1,2]);
    expect(entries[0].item).toBe(second);
    expect(Object.keys(second)).toEqual(['id','amount','startDate','status']);
  });
  it('handles empty history and events without rent steps', () => {
    expect(buildRentalAmountTimeline([])).toEqual([]);
    const entry=buildRentalAmountTimeline([step(-1,100,'2026-01-01','event')])[0];
    expect(entry.hasPreviousStep).toBeFalse();
    expect(entry.changePercentage).toBeNull();
  });
});
