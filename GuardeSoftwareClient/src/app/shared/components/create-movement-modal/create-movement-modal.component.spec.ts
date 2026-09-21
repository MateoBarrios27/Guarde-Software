import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CreateMovementModalComponent } from './create-movement-modal.component';
import { AccountMovementService } from '../../../core/services/accountMovement-service/account-movement.service';

describe('CreateMovementModalComponent', () => {
  let component: CreateMovementModalComponent;
  let fixture: ComponentFixture<CreateMovementModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CreateMovementModalComponent],
      providers: [
        {
          provide: AccountMovementService,
          useValue: jasmine.createSpyObj<AccountMovementService>('AccountMovementService', ['createMovement'])
        }
      ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CreateMovementModalComponent);
    component = fixture.componentInstance;
    component.clientId = 1;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should suggest the current and next month concepts', () => {
    const now = new Date();
    const months = [
      'enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio',
      'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'
    ];
    const month = months[now.getMonth()];
    const formattedMonth = month.charAt(0).toUpperCase() + month.slice(1);
    const nextMonthDate = new Date(now.getFullYear(), now.getMonth() + 1, 1);
    const nextMonth = months[nextMonthDate.getMonth()];
    const formattedNextMonth = nextMonth.charAt(0).toUpperCase() + nextMonth.slice(1);

    expect(component.conceptSuggestions).toEqual([
      `Interés por mora de ${formattedMonth} ${now.getFullYear()}`,
      `Alquiler ${formattedMonth} ${now.getFullYear()}`,
      `Alquiler ${formattedNextMonth} ${nextMonthDate.getFullYear()}`,
      'Proporcional'
    ]);
  });

  it('should put the selected suggestion in the concept control', () => {
    component.showConceptSuggestions = true;

    component.selectConceptSuggestion('Proporcional');

    expect(component.newMovementForm.get('concept')?.value).toBe('Proporcional');
    expect(component.showConceptSuggestions).toBeFalse();
  });

  it('should close concept suggestions on the first pointer down outside the autocomplete', () => {
    component.openConceptSuggestions();
    fixture.detectChanges();

    const outsideElement = fixture.nativeElement.querySelector('h3') as HTMLElement;
    outsideElement.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true }));
    fixture.detectChanges();

    expect(component.showConceptSuggestions).toBeFalse();
  });

  it('should keep concept suggestions open when the pointer down occurs inside the autocomplete', () => {
    component.openConceptSuggestions();
    fixture.detectChanges();

    const conceptTextarea = fixture.nativeElement.querySelector('#movement-concept') as HTMLTextAreaElement;
    conceptTextarea.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true }));
    fixture.detectChanges();

    expect(component.showConceptSuggestions).toBeTrue();
  });
});
