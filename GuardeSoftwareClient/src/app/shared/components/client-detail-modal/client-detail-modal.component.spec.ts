import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ClientDetailModalComponent } from './client-detail-modal.component';

describe('ClientDetailModalComponent', () => {
  let component: ClientDetailModalComponent;
  let fixture: ComponentFixture<ClientDetailModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ClientDetailModalComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ClientDetailModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
