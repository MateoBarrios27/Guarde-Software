import { TableClient } from '../../core/dtos/client/TableClientDto';
import { ClientProjectedNextMonthItem } from '../../core/dtos/client/ClientProjectedNextMonthDto';
import { displayedAmount, orderByProjectedAmount, projectableSortColumn, sumDisplayedAmounts } from './client-projection.util';

const projection = (clientId: number, balance: number, overrides: Partial<ClientProjectedNextMonthItem> = {}): ClientProjectedNextMonthItem =>
  ({ clientId, previousBalance: 0, interestAmount: 0, currentRent: 100, balance, statementTotal: 0, ...overrides });

const client = (id: number, paymentIdentifier: number | null, balance: number, projected?: ClientProjectedNextMonthItem): TableClient =>
  ({ id, paymentIdentifier, previousBalance: 0, interestAmount: 0, currentRent: 100, balance, _projection: projected ?? null } as TableClient);

describe('client projection util', () => {
  it('maps only the four amount columns to a projectable sort', () => {
    expect(projectableSortColumn('Balance')).toBe('balance');
    expect(projectableSortColumn('PreviousBalance')).toBe('previousBalance');
    expect(projectableSortColumn('InterestAmount')).toBe('interestAmount');
    expect(projectableSortColumn('CurrentRent')).toBe('currentRent');
    expect(projectableSortColumn('FullName')).toBeNull();
    expect(projectableSortColumn('NextPaymentDay')).toBeNull();
  });

  it('shows current figures normally and projected ones (or unknown) when the view is on', () => {
    const withProjection = client(1, 1, -100, projection(1, -250, { previousBalance: -100, interestAmount: 12.5, currentRent: 150 }));
    const withoutProjection = client(2, 2, -100);

    expect(displayedAmount(withProjection, 'balance', false)).toBe(-100);
    expect(displayedAmount(withProjection, 'balance', true)).toBe(-250);
    expect(displayedAmount(withProjection, 'previousBalance', true)).toBe(-100);
    expect(displayedAmount(withProjection, 'interestAmount', true)).toBe(12.5);
    expect(displayedAmount(withProjection, 'currentRent', true)).toBe(150);
    expect(displayedAmount(withoutProjection, 'balance', true)).toBeNull();
    expect(displayedAmount(withoutProjection, 'balance', false)).toBe(-100);
  });

  it('keeps a missing current amount as zero, as the table did', () => {
    const row = { id: 1, paymentIdentifier: 1 } as TableClient;
    expect(displayedAmount(row, 'balance', false)).toBe(0);
  });

  it('sums the displayed amounts and counts unknown projections as zero', () => {
    const rows = [
      client(1, 1, -100, projection(1, -250)),
      client(2, 2, 0, projection(2, 40)),
      client(3, 3, -999),
    ];
    expect(sumDisplayedAmounts(rows, 'balance', false)).toBe(-1099);
    expect(sumDisplayedAmounts(rows, 'balance', true)).toBe(-210);
  });

  it('orders by projected amount, ties by payment identifier, unknown last in both directions', () => {
    const rows = [
      client(1, 3, 0, projection(1, -20)),
      client(2, 1, 0, projection(2, -300)),
      client(3, 2, 0),
      client(4, 4, 0, projection(4, -20)),
      client(5, 5, 0, projection(5, 10)),
    ];

    expect(orderByProjectedAmount(rows, 'Balance', 'asc', true).map(c => c.id)).toEqual([2, 1, 4, 5, 3]);
    expect(orderByProjectedAmount(rows, 'Balance', 'desc', true).map(c => c.id)).toEqual([5, 1, 4, 2, 3]);
  });

  it('leaves the server order for other fields or when the view is off, without copying', () => {
    const rows = [client(1, 2, -5, projection(1, -50)), client(2, 1, -1, projection(2, -1))];
    expect(orderByProjectedAmount(rows, 'FullName', 'asc', true)).toBe(rows);
    expect(orderByProjectedAmount(rows, 'Balance', 'asc', false)).toBe(rows);
  });
});
