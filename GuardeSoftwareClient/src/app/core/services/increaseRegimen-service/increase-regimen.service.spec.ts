import { TestBed } from '@angular/core/testing';

import { IncreaseRegimenService } from './increase-regimen.service';

describe('IncreaseRegimenService', () => {
  let service: IncreaseRegimenService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(IncreaseRegimenService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
