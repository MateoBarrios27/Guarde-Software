import { TestBed } from '@angular/core/testing';

import { MonthlyIncreaseService } from './monthly-increase.service';

describe('MonthlyIncreaseService', () => {
  let service: MonthlyIncreaseService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(MonthlyIncreaseService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
