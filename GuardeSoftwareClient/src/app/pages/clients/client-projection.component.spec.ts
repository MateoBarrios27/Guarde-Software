import { LOCALE_ID } from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localeEsAr from '@angular/common/locales/es-AR';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BehaviorSubject, Subject, of, throwError } from 'rxjs';
import { ClientsComponent } from './clients.component';
import { ClientService } from '../../core/services/client-service/client.service';
import { StatisticsService } from '../../core/services/statics-service/statics-service.service';
import { WarehouseService } from '../../core/services/warehouse-service/warehouse.service';
import { BillingTypeService } from '../../core/services/billingType-service/billing-type.service';
import { PaymentMethodService } from '../../core/services/paymentMethod-service/payment-method.service';
import { LockerTypeService } from '../../core/services/lockerType-service/locker-type.service';
import { OfflineService } from '../../core/services/offline-service/offline.service';
import { IndexedDbService } from '../../core/services/offline-service/indexed-db.service';
import { DataRefreshEvent, DataRefreshService } from '../../core/services/data-refresh-service/data-refresh.service';
import { AuthService } from '../../core/services/auth-service/auth.service';
import { TableClient } from '../../core/dtos/client/TableClientDto';
import { ClientProjectedNextMonthItem, ClientProjectedNextMonthResult } from '../../core/dtos/client/ClientProjectedNextMonthDto';

// Same locale as the app (app.config.ts): amounts render as `$ -100,00`.
registerLocaleData(localeEsAr, 'es-AR');

const tableClient = (id: number, paymentIdentifier: number, balance: number): TableClient => ({
  id, paymentIdentifier, fullName: `Cliente ${id}`, city: '', status: 'Pendiente', active: true, lockers: [],
  previousBalance: 0, interestAmount: 0, currentRent: 100, balance, nextPaymentDay: null,
} as TableClient);

const projected = (clientId: number, balance: number, overrides: Partial<ClientProjectedNextMonthItem> = {}): ClientProjectedNextMonthItem =>
  ({ clientId, previousBalance: -10, interestAmount: 5, currentRent: 120, balance, statementTotal: 0, ...overrides });

// Current balances: -100, 0, +50. Projected balances reorder them: 2, 1, 3 (client 4 has none).
const serverRows = () => [tableClient(1, 1.01, -100), tableClient(2, 2.02, 0), tableClient(3, 3.03, 50), tableClient(4, 4.04, 10)];
const projection = (): ClientProjectedNextMonthResult => ({
  targetMonth: '2026-11-01T00:00:00', generatedAt: '2026-10-30T12:00:00Z', failedClientIds: [4],
  items: [
    projected(1, -20, { increasePercentage: 6.7 }),
    projected(2, -300, { previousBalance: 0, interestAmount: 0 }),
    projected(3, 10, { previousBalance: 50, statementTotal: 0, increasePercentageMissing: true }),
  ],
});

describe('Clients projected next month', () => {
  let fixture: ComponentFixture<ClientsComponent>;
  let component: ClientsComponent;
  let online$: BehaviorSubject<boolean>;
  let refresh$: Subject<DataRefreshEvent>;
  let getTableClients: jasmine.Spy;
  let getProjectedNextMonth: jasmine.Spy;

  const settle = async () => {
    await fixture.whenStable();
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const host = () => fixture.nativeElement as HTMLElement;
  const cells = (row: number) => Array.from(host().querySelectorAll(`#client-row-${row} td`)).map(td => td.textContent?.replace(/\s+/g, ' ').trim());
  const rowIds = () => Array.from(host().querySelectorAll('tr.client-row')).map(tr => Number(tr.id.replace('client-row-', '')));
  const toggle = () => host().querySelector<HTMLInputElement>('input[role="switch"]')!;

  beforeEach(async () => {
    online$ = new BehaviorSubject<boolean>(true);
    refresh$ = new Subject<DataRefreshEvent>();
    getTableClients = jasmine.createSpy('getTableClients').and.callFake(() => of({ items: serverRows(), totalCount: 4, pageNumber: 1, pageSize: 10000 }));
    getProjectedNextMonth = jasmine.createSpy('getProjectedNextMonth').and.callFake(() => of(projection()));

    await TestBed.configureTestingModule({
      imports: [ClientsComponent],
      providers: [
        provideRouter([]),
        { provide: LOCALE_ID, useValue: 'es-AR' },
        { provide: ClientService, useValue: { getTableClients, getProjectedNextMonth } },
        { provide: StatisticsService, useValue: { getClientStatistics: () => of({ total: 4, alDia: 1, morosos: 1, pendientes: 2, dadosBaja: 0 }) } },
        { provide: WarehouseService, useValue: { getWarehouses: () => of([]) } },
        { provide: BillingTypeService, useValue: { getBillingTypes: () => of([]) } },
        { provide: PaymentMethodService, useValue: { getPaymentMethods: () => of([]) } },
        { provide: LockerTypeService, useValue: { getLockerTypes: () => of([]) } },
        { provide: OfflineService, useValue: { get isOnline() { return online$.value; }, isOnline$: online$.asObservable() } },
        { provide: IndexedDbService, useValue: { getCachedClients: () => Promise.resolve([]) } },
        { provide: DataRefreshService, useValue: { watch: () => refresh$.asObservable() } },
        { provide: AuthService, useValue: { isObserver: () => false } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ClientsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await settle();
  });

  it('shows the current figures and does not calculate anything until the switch is turned on', () => {
    expect(toggle()).toBeTruthy();
    expect(toggle().checked).toBeFalse();
    expect(getProjectedNextMonth).not.toHaveBeenCalled();
    expect(host().querySelector('.client-projection-banner')).toBeNull();
    expect(rowIds()).toEqual([1, 2, 3, 4]);
    expect(component.totals.balance).toBe(-40);
    expect(cells(1)).toContain('$ -100,00');
  });

  it('fills previous balance, interest, rent and balance with the projected figures and totals', async () => {
    toggle().click();
    await settle();

    expect(getProjectedNextMonth).toHaveBeenCalledTimes(1);
    expect(toggle().checked).toBeTrue();
    expect(host().querySelector('.client-projection-banner')?.textContent).toContain('noviembre 2026');
    expect(host().querySelectorAll('.client-projection-pill').length).toBe(4);

    // Client 1: prev -10, interest 5, rent 120, balance -20.
    expect(cells(1)).toEqual(jasmine.arrayContaining(['$ -10,00', '$ 5,00', '$ 120,00', '$ -20,00']));
    expect(component.totals.previousBalance).toBe(-10 + 0 + 50);
    expect(component.totals.interestAmount).toBe(5 + 0 + 5);
    expect(component.totals.currentRent).toBe(360);
    expect(component.totals.balance).toBe(-20 - 300 + 10);
  });

  it('marks clients without projection as unknown instead of showing their current figures', async () => {
    toggle().click();
    await settle();

    expect(cells(4).filter(text => text === '—').length).toBe(4);
    expect(host().querySelector('.client-projection-banner')?.textContent).toContain('1 sin proyección');
  });

  it('explains an increase included in the rent and flags a due increase that has no configured percentage', async () => {
    const hints = (row: number) => Array.from(host().querySelectorAll(`#client-row-${row} td[title]`))
      .map(td => td.getAttribute('title') ?? '').filter(Boolean);

    expect(hints(1).some(text => text.includes('aumento'))).toBeFalse();

    toggle().click();
    await settle();

    expect(hints(1).some(text => text.includes('aumento del 6,7 %') && text.includes('hacia arriba'))).toBeTrue();
    expect(hints(2).some(text => text.includes('aumento'))).toBeFalse();
    expect(hints(3).some(text => text.includes('no hay porcentaje configurado'))).toBeTrue();
    expect(host().querySelector('.client-projection-banner')?.textContent).toContain('1 con aumento sin porcentaje');
  });

  it('sorts the loaded rows by projected balance without asking the server again', async () => {
    toggle().click();
    await settle();
    const tableCalls = getTableClients.calls.count();

    component.handleSort('Balance');
    await settle();

    expect(rowIds()).toEqual([2, 1, 3, 4]);
    component.handleSort('Balance');
    await settle();
    expect(rowIds()).toEqual([3, 1, 2, 4]);
    expect(getTableClients.calls.count()).toBe(tableCalls);
  });

  it('turning the switch off restores the current figures and the server order of the sorted column', async () => {
    toggle().click();
    await settle();
    component.handleSort('Balance');
    await settle();

    toggle().click();
    await settle();

    expect(toggle().checked).toBeFalse();
    expect(host().querySelector('.client-projection-banner')).toBeNull();
    expect(rowIds()).toEqual([1, 2, 3, 4]);
    expect(cells(1)).toContain('$ -100,00');
    expect(component.totals.balance).toBe(-40);
    expect(getTableClients.calls.count()).toBeGreaterThan(1);
  });

  it('reuses the calculation when searching or filtering, but a data change forces a new one', async () => {
    toggle().click();
    await settle();

    await component.loadClients();
    await settle();
    expect(getProjectedNextMonth).toHaveBeenCalledTimes(1);

    refresh$.next({ domains: ['finances'], sourceRoute: null, emittedAt: Date.now() });
    await settle();
    expect(getProjectedNextMonth).toHaveBeenCalledTimes(2);
  });

  it('keeps a failed calculation visible with a retry, never showing current figures as projected', async () => {
    getProjectedNextMonth.and.returnValue(throwError(() => new Error('boom')));
    spyOn(console, 'error');
    toggle().click();
    await settle();

    expect(host().querySelector('.client-projection-banner')?.textContent).toContain('No se pudo calcular');
    expect(cells(1).filter(text => text === '—').length).toBe(4);
    expect(component.totalsUnknown).toBeTrue();

    getProjectedNextMonth.and.returnValue(of(projection()));
    Array.from(host().querySelectorAll<HTMLButtonElement>('.client-projection-banner button')).find(b => b.textContent?.includes('Reintentar'))!.click();
    await settle();

    expect(host().querySelector('.client-projection-banner')?.textContent).not.toContain('No se pudo calcular');
    expect(cells(1)).toContain('$ -20,00');
  });

  it('ignores a calculation that was still running when the switch was turned off', async () => {
    const pending = new Subject<ClientProjectedNextMonthResult>();
    getProjectedNextMonth.and.returnValue(pending);
    toggle().click();
    await settle();
    expect(component.projectionLoading).toBeTrue();
    expect(host().querySelectorAll('.skeleton-pulse').length).toBeGreaterThan(0);

    toggle().click();
    await settle();
    pending.next(projection());
    pending.complete();
    await settle();

    expect(component.projectionLoading).toBeFalse();
    expect(cells(1)).toContain('$ -100,00');
  });

  it('disables the switch offline and shows current figures; reconnecting brings the projection back', async () => {
    toggle().click();
    await settle();

    online$.next(false);
    await settle();
    expect(toggle().disabled).toBeTrue();
    expect(host().querySelector('.client-projection-banner')).toBeNull();
    expect(cells(1)).toContain('$ -100,00');

    getProjectedNextMonth.calls.reset();
    online$.next(true);
    await settle();
    expect(toggle().disabled).toBeFalse();
    expect(cells(1)).toContain('$ -20,00');
  });

  it('hides the switch in the list of deactivated clients and restores it for active ones', async () => {
    component.toggleInactivos();
    await settle();
    expect(host().querySelector('input[role="switch"]')).toBeNull();

    component.toggleInactivos();
    await settle();
    expect(host().querySelector('input[role="switch"]')).toBeTruthy();
  });
});
