import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CreateMovementModalComponent } from './create-movement-modal.component';

describe('CreateMovementModalComponent', () => {
  let component: CreateMovementModalComponent;
  let fixture: ComponentFixture<CreateMovementModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CreateMovementModalComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CreateMovementModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
