import { SimpleChange } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { of } from 'rxjs';
import { CreateClientModalComponent } from './create-client-modal.component';
import { Locker } from '../../../core/models/locker';

describe('CreateClientModalComponent', () => {
  let component: CreateClientModalComponent;

  const createComponentForDate = (today: Date): CreateClientModalComponent => {
    type ConstructorArgs = ConstructorParameters<typeof CreateClientModalComponent>;
    const serviceStub = {} as unknown;
    const clientServiceStub = {
      getNextPaymentIdentifier: () => of({ nextIdentifier: 1 }),
    } as unknown;
    const constructorArgs: ConstructorArgs = [
      new FormBuilder(),
      serviceStub as ConstructorArgs[1],
      serviceStub as ConstructorArgs[2],
      serviceStub as ConstructorArgs[3],
      serviceStub as ConstructorArgs[4],
      serviceStub as ConstructorArgs[5],
      clientServiceStub as ConstructorArgs[6],
      serviceStub as ConstructorArgs[7],
    ];
    const instance = new CreateClientModalComponent(...constructorArgs);
    Object.defineProperty(instance, 'getArgentinaToday', { value: () => today });
    instance.ngOnChanges({ clientData: new SimpleChange(undefined, null, true) });
    return instance;
  };

  beforeEach(() => {
    component = createComponentForDate(new Date(2026, 8, 20));
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('uses the existing automatic proportional calculation by default', () => {
    component.newClientForm.get('montoManual')?.setValue(300000);

    expect(component.shouldGenerateProportional).toBeTrue();
    expect(component.isManualProportional).toBeFalse();
    expect(component.automaticProportionalDays).toBe(10);
    expect(component.automaticProportionalAmount).toBe(100000);
  });

  it('recalculates the manual amount from days and still allows an amount override', () => {
    component.newClientForm.get('montoManual')?.setValue(300000);
    component.setProportionalMode('manual');

    component.newClientForm.get('proportionalDays')?.setValue(5);
    expect(component.newClientForm.get('proportionalAmount')?.value).toBe(50000);

    component.newClientForm.get('proportionalAmount')?.setValue(47500);
    expect(component.newClientForm.get('proportionalAmount')?.value).toBe(47500);

    component.newClientForm.get('proportionalDays')?.setValue(6);
    expect(component.newClientForm.get('proportionalAmount')?.value).toBe(60000);
  });

  it('validates manual days and hides the option when no proportional is generated', () => {
    component.setProportionalMode('manual');
    const daysControl = component.newClientForm.get('proportionalDays');

    daysControl?.setValue(31);
    expect(daysControl?.hasError('max')).toBeTrue();
    daysControl?.setValue(1.5);
    expect(daysControl?.hasError('pattern')).toBeTrue();

    const earlyMonthComponent = createComponentForDate(new Date(2026, 8, 9));
    expect(earlyMonthComponent.shouldGenerateProportional).toBeFalse();
    expect(earlyMonthComponent.newClientForm.get('proportionalMode')?.value).toBe('automatic');
  });

  it('shows an exact occupied locker as disabled with all of its assigned clients in edit mode', () => {
    const occupiedLocker: Locker = {
      id: 50,
      warehouseId: 1,
      lockerTypeId: 1,
      identifier: 'B-050',
      features: '',
      status: 'OCUPADO',
      clients: [
        { id: 10, fullName: 'Cliente Uno', paymentIdentifier: 101 },
        { id: 11, fullName: 'Cliente Dos', paymentIdentifier: 102 },
      ],
      isFreeSpace: true,
    };
    component.isEditMode = true;
    component.allLockers = [occupiedLocker];
    component.availableLockers = [];
    component.newClientForm.patchValue({
      lockerSearch: '  b-050  ',
      selectedWarehouse: 'all',
      selectedLockerType: 'all',
    });

    expect(component.filteredLockers).toEqual([occupiedLocker]);
    expect(component.isLockerDisabled(occupiedLocker)).toBeTrue();
    expect(component.getLockerOccupantNames(occupiedLocker)).toBe('Cliente Uno, Cliente Dos');

    component.handleLockerToggle(occupiedLocker.id);
    expect(component.newClientForm.get('lockersAsignados')?.value).toEqual([]);
  });

  it('keeps occupied lockers hidden from the direct assignment flow when creating a client', () => {
    const occupiedLocker: Locker = {
      id: 51,
      warehouseId: 1,
      lockerTypeId: 1,
      identifier: 'B-051',
      features: '',
      status: 'OCUPADO',
      clientName: 'Cliente Existente',
    };
    component.isEditMode = false;
    component.assignmentMode = 'direct';
    component.allLockers = [occupiedLocker];
    component.availableLockers = [];
    component.newClientForm.patchValue({
      lockerSearch: 'B-051',
      selectedWarehouse: 'all',
      selectedLockerType: 'all',
    });

    expect(component.filteredLockers).toEqual([]);
  });
});
