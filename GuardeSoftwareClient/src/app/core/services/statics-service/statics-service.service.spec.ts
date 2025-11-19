import { TestBed } from '@angular/core/testing';

import { StaticsServiceService } from './statics-service.service';

describe('StaticsServiceService', () => {
  let service: StaticsServiceService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(StaticsServiceService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
