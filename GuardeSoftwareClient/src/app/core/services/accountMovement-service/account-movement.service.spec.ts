import { TestBed } from '@angular/core/testing';

import { AccountMovementService } from './account-movement.service';

describe('AccountMovementService', () => {
  let service: AccountMovementService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(AccountMovementService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
