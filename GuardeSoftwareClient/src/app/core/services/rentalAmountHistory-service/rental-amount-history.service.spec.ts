import { TestBed } from '@angular/core/testing';

import { RentalAmountHistoryService } from './rental-amount-history.service';

describe('RentalAmountHistoryService', () => {
  let service: RentalAmountHistoryService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(RentalAmountHistoryService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
