import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PaymentIncreaseModalComponent } from './payment-increase-modal.component';
import { DataRefreshService } from '../../../core/services/data-refresh-service/data-refresh.service';

describe('Payment increase modal configured default', () => {
  let fixture: ComponentFixture<PaymentIncreaseModalComponent>;
  let http: HttpTestingController;
  let resolved: jasmine.Spy;
  const settingsRequest = (request: { url: string }) => request.url.endsWith('/MonthlyIncrease');
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [PaymentIncreaseModalComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(),
        { provide: DataRefreshService, useValue: { notify: jasmine.createSpy('notify') } }] }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(PaymentIncreaseModalComponent);
    fixture.componentRef.setInput('increaseYear', 2026);
    fixture.componentRef.setInput('increaseMonth', 11);
    fixture.componentRef.setInput('currentRent', 100000);
    fixture.componentRef.setInput('projectedRent', 100000);
    resolved = jasmine.createSpy('resolved');
    fixture.componentInstance.configuredPercentageResolved.subscribe(resolved);
  });
  afterEach(() => { fixture.destroy(); http.verify(); });
  const flushSetting = (percentage = 7.3) => http.expectOne(settingsRequest)
    .flush([{ effectiveDate: '2026-11-01T00:00:00', percentage }]);

  it('waits for settings before confirmation and then initializes the requested rate', fakeAsync(() => {
    fixture.detectChanges();
    tick();
    expect(fixture.componentInstance.confirmIsDisabled).toBeTrue();
    expect(fixture.nativeElement.querySelector('#increase-percentage').disabled).toBeTrue();
    flushSetting();
    fixture.detectChanges();
    expect(resolved).toHaveBeenCalledOnceWith(7.3);
    expect(fixture.componentInstance.confirmIsDisabled).toBeFalse();
    expect(fixture.nativeElement.textContent).toContain('Configuración de aumentos: 7.3%');
    expect(fixture.nativeElement.querySelector('header').textContent).toContain('Ajustar abono');
    expect(fixture.nativeElement.querySelector('header').textContent).not.toContain('Etapa');
    tick();
  }));
  it('keeps a confirmed stage instead of fetching and replacing its manual value', fakeAsync(() => {
    fixture.componentRef.setInput('useConfiguredPercentage', false);
    fixture.componentRef.setInput('percentage', 12);
    fixture.componentRef.setInput('projectedRent', 112000);
    fixture.detectChanges();
    http.expectNone(settingsRequest);
    expect(resolved).not.toHaveBeenCalled();
    tick();
    expect(Number(fixture.nativeElement.querySelector('#increase-percentage').value)).toBe(12);
  }));
  it('cancels an old stage request so it cannot replace the next stage percentage', () => {
    fixture.detectChanges();
    const old = http.expectOne(settingsRequest);
    fixture.componentRef.setInput('increaseMonth', 12);
    fixture.detectChanges();
    expect(old.cancelled).toBeTrue();
    http.expectOne(settingsRequest).flush([{ effectiveDate: '2026-12-01', percentage: 9 }]);
    expect(resolved).toHaveBeenCalledOnceWith(9);
  });
  it('indicates the missing month and allows a manually entered percentage', () => {
    fixture.detectChanges();
    http.expectOne(settingsRequest).flush([{ effectiveDate: '2026-10-01', percentage: 15 }]);
    fixture.detectChanges();
    expect(resolved).toHaveBeenCalledOnceWith(0);
    expect(fixture.nativeElement.textContent).toContain('Este mes no tiene un aumento configurado');
    const manual = jasmine.createSpy('manual');
    fixture.componentInstance.percentageChange.subscribe(manual);
    const input = fixture.nativeElement.querySelector('#increase-percentage') as HTMLInputElement;
    input.value = '8.5'; input.dispatchEvent(new Event('input'));
    expect(manual).toHaveBeenCalledWith(8.5);
    http.expectNone(settingsRequest);
  });
  it('shows a loading failure, preserves edits and can retry', () => {
    fixture.componentRef.setInput('percentage', 12);
    fixture.componentRef.setInput('projectedRent', 112000);
    fixture.detectChanges();
    http.expectOne(settingsRequest).flush({}, { status: 500, statusText: 'Error' });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('Podés ingresarlo manualmente');
    expect(resolved).not.toHaveBeenCalled();
    expect(fixture.componentInstance.percentage).toBe(12);
    expect(fixture.componentInstance.confirmIsDisabled).toBeFalse();
    fixture.nativeElement.querySelector('.increase-settings-error button').click();
    flushSetting(9);
    expect(resolved).toHaveBeenCalledOnceWith(9);
  });
  it('cancels the pending request when the modal is closed', () => {
    fixture.detectChanges();
    const pending = http.expectOne(settingsRequest);
    fixture.destroy();
    expect(pending.cancelled).toBeTrue();
    expect(resolved).not.toHaveBeenCalled();
  });
});
