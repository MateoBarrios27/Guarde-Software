import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { CreateMovementModalComponent } from './create-movement-modal.component';
import { AccountMovementService } from '../../../core/services/accountMovement-service/account-movement.service';
import { AccountMovementDTO } from '../../../core/dtos/accountMovement/account-movement.dto';

describe('CreateMovementModalComponent', () => {
  let component: CreateMovementModalComponent;
  let fixture: ComponentFixture<CreateMovementModalComponent>;
  let accountMovementService: jasmine.SpyObj<AccountMovementService>;

  const debit = (concept: string) => ({ movementType: 'DEBITO', concept }) as AccountMovementDTO;

  beforeEach(async () => {
    accountMovementService = jasmine.createSpyObj<AccountMovementService>(
      'AccountMovementService',
      ['createMovement', 'getMovementsByClientId']
    );
    accountMovementService.getMovementsByClientId.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [CreateMovementModalComponent],
      providers: [
        { provide: AccountMovementService, useValue: accountMovementService }
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

  it('should suggest the two rent months after the debits the client already has', () => {
    accountMovementService.getMovementsByClientId.and.returnValue(of([
      debit('Alquiler Octubre 2026'),
      debit('Alquiler Noviembre 2026 (Planificado)'),
    ]));

    component.ngOnInit();

    expect(accountMovementService.getMovementsByClientId).toHaveBeenCalledWith(1);
    expect(component.conceptSuggestions.slice(1, 3)).toEqual([
      'Alquiler Diciembre 2026',
      'Alquiler Enero 2027',
    ]);
    expect(component.conceptSuggestions[3]).toBe('Proporcional');
  });

  it('should keep the current and next month when the movements cannot be loaded', () => {
    const before = component.conceptSuggestions;
    accountMovementService.getMovementsByClientId.and.returnValue(throwError(() => new Error('offline')));

    component.ngOnInit();

    expect(component.conceptSuggestions).toEqual(before);
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
