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

  it('should suggest the current month concepts', () => {
    const now = new Date();
    const months = [
      'enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio',
      'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'
    ];
    const month = months[now.getMonth()];
    const formattedMonth = month.charAt(0).toUpperCase() + month.slice(1);

    expect(component.conceptSuggestions).toEqual([
      `Interés por mora de ${formattedMonth} ${now.getFullYear()}`,
      `Alquiler ${formattedMonth} ${now.getFullYear()}`,
      'Proporcional'
    ]);
  });

  it('should put the selected suggestion in the concept control', () => {
    component.showConceptSuggestions = true;

    component.selectConceptSuggestion('Proporcional');

    expect(component.newMovementForm.get('concept')?.value).toBe('Proporcional');
    expect(component.showConceptSuggestions).toBeFalse();
  });
});
