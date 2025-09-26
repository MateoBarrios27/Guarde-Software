import { TestBed } from '@angular/core/testing';

import { LockerTypeService } from './locker-type.service';

describe('LockerTypeService', () => {
  let service: LockerTypeService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(LockerTypeService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
