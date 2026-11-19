import { Component, NgZone } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { OutsideEventsDirective } from '../directives/outside-events.directive';
import { SuspendViewDirective } from '../directives/suspend-view.directive';
import { DataRefreshService } from '../../core/services/data-refresh-service/data-refresh.service';
import { WarehouseService } from '../../core/services/warehouse-service/warehouse.service';
import { PaymentMethodService } from '../../core/services/paymentMethod-service/payment-method.service';
import { BillingTypeService } from '../../core/services/billingType-service/billing-type.service';
import { LockerTypeService } from '../../core/services/lockerType-service/locker-type.service';
import { LockersComponent } from '../../pages/lockers/lockers.component';
import { Locker } from '../../core/models/locker';

describe('Catalog requests', () => {
  let http: HttpTestingController;
  let refresh: DataRefreshService;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: Router, useValue: { url: '/settings' } }] });
    http = TestBed.inject(HttpTestingController);
    refresh = TestBed.inject(DataRefreshService);
  });
  afterEach(() => http.verify());
  const catalogs = [
    [WarehouseService, 'getWarehouses', 'Warehouse'],
    [PaymentMethodService, 'getPaymentMethods', 'PaymentMethod'],
    [BillingTypeService, 'getBillingTypes', 'BillingType'],
    [LockerTypeService, 'getLockerTypes', 'LockerType'],
  ] as const;
  for (const [token, method, endpoint] of catalogs) {
    it(`${endpoint} shares pending/completed requests and reloads after catalog/all`, () => {
      const service = TestBed.inject(token as typeof WarehouseService) as any;
      const values: unknown[] = [];
      service[method]().subscribe((value: unknown) => values.push(value));
      service[method]().subscribe((value: unknown) => values.push(value));
      const request = http.expectOne(req => req.url.endsWith('/' + endpoint));
      request.flush([{ id: 1, name: 'Inicial' }]);
      service[method]().subscribe((value: unknown) => values.push(value));
      http.expectNone(req => req.url.endsWith('/' + endpoint));
      expect(values.length).toBe(3);
      refresh.notify('clients');
      service[method]().subscribe();
      http.expectNone(req => req.url.endsWith('/' + endpoint));
      for (const domain of ['catalog', 'all'] as const) {
        refresh.notify(domain);
        service[method]().subscribe();
        http.expectOne(req => req.url.endsWith('/' + endpoint)).flush([]);
      }
    });
  }

  it('retries a failed catalog request', () => {
    const service = TestBed.inject(WarehouseService);
    const error = jasmine.createSpy('catalogError');
    service.getWarehouses().subscribe({ error });
    http.expectOne(req => req.url.endsWith('/Warehouse')).flush('error', { status: 500, statusText: 'Error' });
    service.getWarehouses().subscribe();
    http.expectOne(req => req.url.endsWith('/Warehouse')).flush([]);
    expect(error).toHaveBeenCalledTimes(1);
  });

  it('an older in-flight response cannot replace the invalidated catalog', () => {
    const service = TestBed.inject(WarehouseService);
    service.getWarehouses().subscribe();
    const old = http.expectOne(req => req.url.endsWith('/Warehouse'));
    refresh.notify('catalog');
    service.getWarehouses().subscribe();
    const fresh = http.expectOne(req => req.url.endsWith('/Warehouse'));
    fresh.flush([{ id: 2, name: 'Nuevo' }]);
    old.flush([{ id: 1, name: 'Viejo' }]);
    service.getWarehouses().subscribe(value => expect(value[0].name).toBe('Nuevo'));
    http.expectNone(req => req.url.endsWith('/Warehouse'));
  });
});

@Component({ standalone: true, imports: [OutsideEventsDirective], template: `
  <div appOutsideEvents (quietScroll)="record()" (quietMouseenter)="record()" (quietMouseleave)="record()"></div>` })
class NativeEventHost {
  zones: boolean[] = [];
  record(): void { this.zones.push(NgZone.isInAngularZone()); }
}

describe('High frequency DOM events', () => {
  it('scroll/hover execute outside Angular and remove all listeners on destruction', () => {
    const fixture = TestBed.createComponent(NativeEventHost);
    fixture.detectChanges();
    const element = fixture.nativeElement.querySelector('div') as HTMLElement;
    const zone = TestBed.inject(NgZone);
    zone.run(() => { for (const name of ['scroll', 'mouseenter', 'mouseleave']) element.dispatchEvent(new Event(name)); });
    expect(fixture.componentInstance.zones).toEqual([false, false, false]);
    fixture.destroy();
    element.dispatchEvent(new Event('scroll'));
    expect(fixture.componentInstance.zones.length).toBe(3);
  });
});

@Component({ standalone: true, imports: [SuspendViewDirective], template: `
  <span *appSuspendView="paused">{{ background }}</span><b>{{ foreground }}</b>` })
class BackgroundViewHost {
  paused = false;
  background = 'initial';
  foreground = 'initial';
}

describe('Background table during client editing', () => {
  it('retains the DOM, updates the foreground, and catches up with pending data on close', () => {
    const fixture = TestBed.createComponent(BackgroundViewHost);
    fixture.detectChanges();
    const node = fixture.nativeElement.querySelector('span');
    fixture.componentInstance.paused = true;
    fixture.detectChanges();
    fixture.componentInstance.background = 'updated';
    fixture.componentInstance.foreground = 'editing';
    fixture.detectChanges();
    expect(node.textContent).toBe('initial');
    expect(fixture.nativeElement.querySelector('b').textContent).toBe('editing');
    fixture.componentInstance.paused = false;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('span')).toBe(node);
    expect(node.textContent).toBe('updated');
  });
});

describe('Bauleras: cached filtering and counts', () => {
  let component: LockersComponent;
  beforeEach(() => {
    const zone = { run: (callback: () => unknown) => callback(), runOutsideAngular: (callback: () => unknown) => callback() };
    component = new LockersComponent({} as never, {} as never, {} as never, {} as never, {} as never,
      {} as never, { events: new Subject() } as never, {} as never,
      { scrollStrategies: { close: () => ({}) } } as never, zone as never, { markForCheck: () => undefined } as never);
    component.lockers = [
      { id: 1, identifier: 'B-10', warehouseId: 1, lockerTypeId: 1, status: 'DISPONIBLE', features: '' },
      { id: 2, identifier: 'B-2', warehouseId: 2, lockerTypeId: 1, status: 'OCUPADO', features: '', clients: [{ id: 1, fullName: 'Ana', paymentIdentifier: 1 }] },
      { id: 3, identifier: 'B-3', warehouseId: 1, lockerTypeId: 2, status: 'POR LIBERARSE', features: '' },
      { id: 4, identifier: 'B-4', warehouseId: 1, lockerTypeId: 2, status: 'MANTENIMIENTO', features: '' },
    ];
  });
  afterEach(() => component.ngOnDestroy());

  it('retains one list across reads and calculates every status over all filtered rows', () => {
    const list = component.filteredLockers;
    expect(list.map(locker => locker.id)).toEqual([2, 3, 4, 1]);
    expect([component.ocupados, component.libres, component.porLiberarse, component.mantenimiento]).toEqual([1, 1, 1, 1]);
    expect(component.filteredLockers).toBe(list);
    component.itemsPerPage = 1;
    expect(component.libres).toBe(1);
    component.toggleWarehouseId(1);
    expect(component.filteredLockers.length).toBe(3);
    expect(component.ocupados).toBe(0);
    component.toggleWarehouseId(1); // exclude
    expect(component.filteredLockers.map(locker => locker.id)).toEqual([2]);
    expect(component.ocupados).toBe(1);
  });

  it('invalidates on data, search and sort changes and keeps natural numeric ordering', () => {
    component.searchTerm = 'Ana';
    expect(component.filteredLockers.map(locker => locker.id)).toEqual([2]);
    component.searchTerm = '';
    component.handleSort('identifier');
    expect(component.filteredLockers.map(locker => locker.id)).toEqual([1, 4, 3, 2]);
    component.lockers = [...component.lockers, { ...component.lockers[0], id: 5, identifier: 'B-11' }];
    expect(component.filteredLockers[0].id).toBe(5);
    component.searchTerm = 'no existe';
    expect([component.filteredLockers.length, component.ocupados, component.libres]).toEqual([0, 0, 0]);
  });
});
