import type { PendingPayment, SyncSnapshot } from './indexed-db.service';

const record = (value: unknown): value is Record<string, any> => !!value && typeof value === 'object' && !Array.isArray(value);
const id = (value: unknown) => typeof value === 'number' && Number.isSafeInteger(value) && value > 0;
const finite = (value: unknown) => typeof value === 'number' && Number.isFinite(value);
const date = (value: unknown) => typeof value === 'string' && /^\d{4}-\d{2}-\d{2}(T.*)?$/.test(value)
  && Number.isFinite(Date.parse(value)) && new Date(value.slice(0, 10) + 'T00:00:00Z').toISOString().slice(0, 10) === value.slice(0, 10);
function requireValue(condition: unknown): asserts condition {
  if (!condition) throw new Error('El archivo está incompleto o contiene datos inválidos. Se conservaron los datos anteriores.');
}
function uniqueRows(value: unknown): asserts value is Record<string, any>[] {
  requireValue(Array.isArray(value));
  const seen = new Set<number>();
  for (const row of value) {
    requireValue(record(row) && id(row['id']) && !seen.has(row['id']));
    seen.add(row['id']);
  }
}
export function validateSnapshot(value: unknown): asserts value is SyncSnapshot {
  requireValue(record(value) && (value['schemaVersion'] == null || value['schemaVersion'] === 1) && date(value['generatedAt']));
  uniqueRows(value['clients']); uniqueRows(value['paymentMethods']); uniqueRows(value['rentals']);
  const numeric = ['paymentIdentifier', 'balance', 'currentRent', 'previousBalance', 'pendingSurcharge', 'interestAmount', 'plannedPaymentAmount'];
  const ids = ['preferredPaymentMethodId', 'billingTypeId', 'rentalId', 'increaseFrequencyMonths'];
  for (const client of value['clients']) {
    requireValue(typeof client['fullName'] === 'string');
    for (const key of numeric) requireValue(client[key] == null || finite(client[key]));
    for (const key of ids) requireValue(client[key] == null || id(client[key]));
    for (const key of ['nextPaymentDay', 'increaseAnchorDate']) requireValue(client[key] == null || date(client[key]));
    for (const key of ['active', 'needsNextRentPlanning', 'isSixMonthPromotion', 'hasPlannedPayment']) requireValue(client[key] == null || typeof client[key] === 'boolean');
    for (const key of ['dni', 'cuit', 'color', 'status', 'departureStatus', 'ivaCondition', 'lastGeneratedMonthYear']) requireValue(client[key] == null || typeof client[key] === 'string');
    for (const key of ['emails', 'lockerIdentifiers']) requireValue(client[key] == null || (Array.isArray(client[key]) && client[key].every((s: unknown) => typeof s === 'string')));
    for (const key of ['warehouseIds', 'lockerTypeIds']) requireValue(client[key] == null || (Array.isArray(client[key]) && client[key].every(id)));
    requireValue(client['monthsUnpaid'] == null || (Number.isInteger(client['monthsUnpaid']) && client['monthsUnpaid'] >= 0));
    requireValue(client['paymentDaysThisMonth'] == null || (Array.isArray(client['paymentDaysThisMonth']) && client['paymentDaysThisMonth'].every((d: number) => Number.isInteger(d) && d >= 1 && d <= 31)));
  }
  for (const method of value['paymentMethods']) requireValue(typeof method['name'] === 'string' && finite(method['commission']));
  for (const rental of value['rentals']) requireValue(id(rental['clientId']) && typeof rental['active'] === 'boolean');
  if (value['monthlyInterest'] != null) {
    const rule = value['monthlyInterest'];
    requireValue(record(rule) && finite(rule['defaultPercentage']) && rule['defaultPercentage'] >= 0 && rule['defaultPercentage'] <= 100
      && Number.isInteger(rule['defaultApplyDay']) && rule['defaultApplyDay'] >= 2 && rule['defaultApplyDay'] <= 28
      && date(rule['today']) && Array.isArray(rule['settings']) && Array.isArray(rule['runs']));
    const months = new Set<string>();
    for (const setting of rule['settings']) {
      requireValue(record(setting) && date(setting['effectiveDate']) && setting['effectiveDate'].slice(8, 10) === '01'
        && !months.has(setting['effectiveDate'].slice(0, 7)) && finite(setting['percentage']) && setting['percentage'] >= 0 && setting['percentage'] <= 100
        && Number.isInteger(setting['applyDay']) && setting['applyDay'] >= 2 && setting['applyDay'] <= 28);
      months.add(setting['effectiveDate'].slice(0, 7));
    }
  }
}

export function validatePayment(value: unknown): asserts value is PendingPayment {
  requireValue(record(value) && typeof value['localId'] === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value['localId'])
    && id(value['clientId']) && id(value['paymentMethodId']) && finite(value['amount']) && value['amount'] > 0
    && date(value['date']) && date(value['createdAt']) && value['movementType'] === 'CREDITO'
    && typeof value['clientName'] === 'string' && typeof value['paymentMethodName'] === 'string'
    && typeof value['isAdvancePayment'] === 'boolean' && ['pending', 'syncing', 'failed', 'reviewed'].includes(value['status']));
  if (value['status'] === 'reviewed') requireValue(typeof value['resolutionNote'] === 'string' && value['resolutionNote'].trim().length >= 6 && date(value['reviewedAt']));
  requireValue(!value['isAdvancePayment'] || id(value['advanceMonths']));
  for (const key of ['commissionAmount', 'surchargeAmount']) requireValue(value[key] == null || finite(value[key]));
  for (const key of ['concept', 'commissionConcept', 'expectedPaymentStateToken', 'futureDebitAction', 'paymentDecisionToken', 'errorMessage', 'errorCode']) requireValue(value[key] == null || typeof value[key] === 'string');
  requireValue(value['surchargeAction'] == null || ['forgive', 'immediate', 'next_payment'].includes(value['surchargeAction']));
  requireValue(value['surchargeAmountWasOverridden'] == null || typeof value['surchargeAmountWasOverridden'] === 'boolean');
}

/** Only financial intent; statuses, display labels and refreshed decision tokens may change. */
export function paymentIntent(payment: PendingPayment): string {
  return JSON.stringify([payment.clientId, payment.paymentMethodId, payment.movementType, payment.concept ?? '', payment.amount,
    new Date(payment.date).toISOString(), payment.isAdvancePayment, payment.advanceMonths ?? null, payment.commissionAmount ?? null,
    payment.commissionConcept ?? '', payment.surchargeAction ?? null, payment.surchargeAmount ?? null, payment.surchargeAmountWasOverridden ?? false]);
}
