import { TableClient } from '../../core/dtos/client/TableClientDto';

export type ClientAmountColumn = 'previousBalance' | 'interestAmount' | 'currentRent' | 'balance';

/** Sort fields of the table that map to a column the projection can replace. */
const PROJECTABLE_SORT_FIELDS: Record<string, ClientAmountColumn> = {
  PreviousBalance: 'previousBalance',
  InterestAmount: 'interestAmount',
  CurrentRent: 'currentRent',
  Balance: 'balance',
};

export function projectableSortColumn(sortField: string): ClientAmountColumn | null {
  return PROJECTABLE_SORT_FIELDS[sortField] ?? null;
}

/** Amount shown in the table; null when the projection is on but unknown for that client. */
export function displayedAmount(client: TableClient, column: ClientAmountColumn, projected: boolean): number | null {
  if (!projected) {
    return client[column] ?? 0;
  }
  return client._projection ? client._projection[column] : null;
}

export function sumDisplayedAmounts(clients: TableClient[], column: ClientAmountColumn, projected: boolean): number {
  return clients.reduce((total, client) => total + (Number(displayedAmount(client, column, projected)) || 0), 0);
}

/**
 * Orders rows by the projected amount of the sorted column, as the server does with the
 * current one: ties by payment identifier, clients without projection last. Returns the same
 * array when the order is the server's.
 */
export function orderByProjectedAmount(
  clients: TableClient[],
  sortField: string,
  direction: 'asc' | 'desc',
  projected: boolean,
): TableClient[] {
  const column = projectableSortColumn(sortField);
  if (!projected || !column) {
    return clients;
  }

  const sign = direction === 'asc' ? 1 : -1;
  return [...clients].sort((a, b) => {
    const left = displayedAmount(a, column, true);
    const right = displayedAmount(b, column, true);
    if (left === null || right === null) {
      return left === right ? 0 : left === null ? 1 : -1;
    }
    if (left !== right) {
      return (left - right) * sign;
    }
    const byIdentifier = (a.paymentIdentifier ?? Number.MAX_VALUE) - (b.paymentIdentifier ?? Number.MAX_VALUE);
    return byIdentifier !== 0 ? byIdentifier : a.id - b.id;
  });
}
