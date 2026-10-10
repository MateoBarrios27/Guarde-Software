import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { MonthlyIncreaseService } from './monthly-increase.service';
import { DataRefreshService } from '../data-refresh-service/data-refresh.service';

describe('MonthlyIncreaseService defaults', () => {
  let service: MonthlyIncreaseService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: DataRefreshService, useValue: { notify: jasmine.createSpy('notify') } }] });
    service = TestBed.inject(MonthlyIncreaseService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  const settingsRequest = (request: { url: string }) => request.url.endsWith('/MonthlyIncrease');

  it('uses the exact increase month, including a UTC first-day timestamp', async () => {
    const result = firstValueFrom(service.getPercentageForMonth(2026, 11));
    http.expectOne(settingsRequest).flush([
      { effectiveDate: '2026-10-01T00:00:00', percentage: 12 },
      { effectiveDate: '2026-11-01T00:00:00Z', percentage: 7.3 },
      { effectiveDate: '2027-11-01', percentage: 30 }
    ]);
    expect(await result).toBe(7.3);
  });
  it('does not reuse the latest setting when the target month is missing', async () => {
    const result = firstValueFrom(service.getPercentageForMonth(2026, 11));
    http.expectOne(settingsRequest).flush([{ effectiveDate: '2026-10-01', percentage: 20 }]);
    expect(await result).toBeNull();
  });
  it('reads fresh settings each time a default is requested', async () => {
    const first = firstValueFrom(service.getPercentageForMonth(2026, 11));
    http.expectOne(settingsRequest).flush([{ effectiveDate: '2026-11-01', percentage: 7 }]);
    expect(await first).toBe(7);
    const second = firstValueFrom(service.getPercentageForMonth(2026, 11));
    http.expectOne(settingsRequest).flush([{ effectiveDate: '2026-11-01', percentage: 9 }]);
    expect(await second).toBe(9);
  });
});
