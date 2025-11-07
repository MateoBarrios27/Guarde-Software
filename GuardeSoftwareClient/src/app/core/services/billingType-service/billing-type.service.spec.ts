import { TestBed } from '@angular/core/testing';

import { BillingTypeService } from './billing-type.service';

describe('BillingTypeService', () => {
  let service: BillingTypeService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(BillingTypeService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
