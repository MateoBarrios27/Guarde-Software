import { TestBed } from '@angular/core/testing';

import { ClientIncreaseRegimenService } from './client-increase-regimen.service';

describe('ClientIncreaseRegimenService', () => {
  let service: ClientIncreaseRegimenService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(ClientIncreaseRegimenService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
