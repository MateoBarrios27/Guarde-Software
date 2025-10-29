import {
  Component,
  OnInit,
  Input,
  Output,
  EventEmitter,
  OnChanges,
  SimpleChanges,
} from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import {
  FormBuilder,
  FormGroup,
  FormArray,
  Validators,
  ReactiveFormsModule,
} from '@angular/forms';
import { IconComponent } from '../icon/icon.component';

// --- Modelos y Servicios que este componente necesita ---
import { Locker } from '../../../core/models/locker';
import { Warehouse } from '../../../core/models/warehouse';
import { PaymentMethod } from '../../../core/models/payment-method';
import { IncreaseRegimen } from '../../../core/models/increase-regimen';
import { LockerType } from '../../../core/models/locker-type';
import { ClientDetailDTO } from '../../../core/dtos/client/ClientDetailDTO';
import { CreateClientDTO } from '../../../core/dtos/client/CreateClientDTO';

import { LockerService } from '../../../core/services/locker-service/locker.service';
import { WarehouseService } from '../../../core/services/warehouse-service/warehouse.service';
import { PaymentMethodService } from '../../../core/services/paymentMethod-service/payment-method.service';
import { IncreaseRegimenService } from '../../../core/services/increaseRegimen-service/increase-regimen.service';
import { LockerTypeService } from '../../../core/services/lockerType-service/locker-type.service';
import { ClientService } from '../../../core/services/client-service/client.service';

@Component({
  selector: 'app-create-client-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, IconComponent],
  templateUrl: './create-client-modal.component.html',
})
export class CreateClientModalComponent implements OnInit {
  // --- ENTRADAS Y SALIDAS ---
  private _clientData: ClientDetailDTO | null = null;

  @Input()
  set clientData(data: ClientDetailDTO | null) {
    this._clientData = data;
    // Solo poblamos el formulario si YA está listo Y los selects están cargados
    if (this.newClientForm && data && this.paymentMethods.length > 0) {
      console.log('Datos de cliente y selects listos. Poblando formulario...');
      this.populateForm(data);
    }
  }

  get clientData(): ClientDetailDTO | null {
    return this._clientData;
  }

  @Output() closeModal = new EventEmitter<void>();
  @Output() saveSuccess = new EventEmitter<void>();

  public newClientForm!: FormGroup;

  // --- Datos para los Selects y cálculos ---
  public warehouses: Warehouse[] = [];
  public availableLockers: Locker[] = [];
  public lockerTypes: LockerType[] = [];
  public paymentMethods: PaymentMethod[] = [];
  public increaseRegimens: IncreaseRegimen[] = [];
  isLoading: boolean = false;

  constructor(
    private fb: FormBuilder,
    private lockerService: LockerService,
    private warehouseService: WarehouseService,
    private paymentMethodService: PaymentMethodService,
    private increaseRegimenService: IncreaseRegimenService,
    private lockerTypeService: LockerTypeService,
    private clientService: ClientService
  ) {}

  ngOnInit(): void {
    this.initNewClientForm();
    this.loadFormData();
  }

  private loadFormData(): void {
    // ... (cargas de warehouseService, lockerService, lockerTypeService, increaseRegimenService... van aquí)
    this.warehouseService
      .getWarehouses()
      .subscribe((data) => (this.warehouses = data));
    this.lockerService
      .getLockers()
      .subscribe(
        (data) =>
          (this.availableLockers = data.filter(
            (l) => l.status.toLowerCase() === 'disponible'
          ))
      );
    this.lockerTypeService
      .getLockerTypes()
      .subscribe((data) => (this.lockerTypes = data));
    this.increaseRegimenService
      .getIncreaseRegimens()
      .subscribe((data) => (this.increaseRegimens = data));

    // --- LÓGICA DE SINCRONIZACIÓN ---
    // Esta es la llamada clave que debe ir al final
    this.paymentMethodService
      .getPaymentMethods()
      .subscribe((data) => {
        this.paymentMethods = data;
        
        // AHORA que tenemos los 'paymentMethods', revisamos si
        // los datos del cliente ya habían llegado.
        if (this.newClientForm && this.clientData) {
          console.log('Selects cargados. Datos del cliente ya estaban. Poblando...');
          this.populateForm(this.clientData);
        }
      });
  }

  private initNewClientForm(): void {
    this.newClientForm = this.fb.group({
      numeroIdentificacion: [''],
      nombre: ['', Validators.required],
      apellido: ['', Validators.required],
      tipoDocumento: ['DNI'],
      numeroDocumento: ['', Validators.required],
      // cuit: [''],
      emails: this.fb.array([
        this.fb.control('', [Validators.required, Validators.email]),
      ]),
      telefonos: this.fb.array([this.fb.control('')]),
      direccion: ['', Validators.required],
      ciudad: ['', Validators.required],
      provincia: ['', Validators.required],
      condicionIVA: [null, Validators.required],
      metodoPago: [null, Validators.required],
      documento: [null, Validators.required],
      observaciones: [''],
      lockersAsignados: this.fb.array([], Validators.required),
      montoManual: [0, [Validators.required, Validators.min(0)]],
      periodicidadAumento: ['4'],
      porcentajeAumento: [''],
      lockerSearch: [''],
      selectedWarehouse: ['all'],
      selectedLockerType: ['all'],
    });

    const lockersAsignados = this.newClientForm.get(
      'lockersAsignados'
    ) as FormArray;
    lockersAsignados.valueChanges.subscribe((ids: number[]) => {
      this.newClientForm
        .get('montoManual')
        ?.setValue(this.calculateTotalAmount(ids));
    });
  }

    private populateForm(data: ClientDetailDTO): void {
    console.log('Poblando formulario con datos:', data);

    // 1. Limpiamos los FormArrays
    this.emails.clear();
    this.telefonos.clear();
    (this.newClientForm.get('lockersAsignados') as FormArray).clear();

    // 2. Rellenamos FormArrays (¡Versión limpia!)
    // 'data.email' ahora es un array gracias al fix de C#
    if (data.email && data.email.length > 0) {
      data.email.forEach(e => this.emails.push(this.fb.control(e, [Validators.required, Validators.email])));
    } else {
      this.addEmail(); // Agrega uno vacío si no hay ninguno
    }

    // 'data.phone' ahora es un array gracias al fix de C#
    if (data.phone && data.phone.length > 0) {
      data.phone.forEach(p => this.telefonos.push(this.fb.control(p)));
    } else {
      this.addTelefono(); // Agrega uno vacío si no hay ninguno
    }

    if (data.lockersList) {
      data.lockersList.forEach(locker => (this.newClientForm.get('lockersAsignados') as FormArray).push(this.fb.control(locker.id)));
    }

    // 3. Rellenamos los campos simples

    // Buscamos el objeto PaymentMethod que coincida con el *nombre*
    const matchingPaymentMethod = this.paymentMethods.find(
      method => method.name === data.preferredPaymentMethod
    );

    this.newClientForm.patchValue({
      numeroIdentificacion: data.paymentIdentifier,
      nombre: data.name,
      apellido: data.lastName,
      numeroDocumento: data.dni,
      // cuit: data.cuit, // Tu form no tiene 'cuit', pero el DTO sí.
      condicionIVA: data.ivaCondition,
      
      metodoPago: matchingPaymentMethod || null, 
      
      direccion: data.address,
      ciudad: data.city,
      provincia: data.province, // Tu DTO de TS usa 'state', asegúrate que el de C# use 'Province' o 'State' y coincidan.
      
      observaciones: data.notes, // Ahora ambos son 'string'
      
      montoManual: data.rentAmount,
      periodicidadAumento: data.increaseFrequency,
      porcentajeAumento: data.increasePercentage, // Asegúrate que el DTO de C# y TS usen 'IncreasePercentage'
    });
  }

  get emails(): FormArray {
    return this.newClientForm.get('emails') as FormArray;
  }
  addEmail(): void {
    this.emails.push(
      this.fb.control('', [Validators.required, Validators.email])
    );
  }
  removeEmail(index: number): void {
    if (this.emails.length > 1) this.emails.removeAt(index);
  }

  get telefonos(): FormArray {
    return this.newClientForm.get('telefonos') as FormArray;
  }
  addTelefono(): void {
    this.telefonos.push(this.fb.control(''));
  }
  removeTelefono(index: number): void {
    if (this.telefonos.length > 1) this.telefonos.removeAt(index);
  }

  get filteredLockers(): Locker[] {
    const search = this.newClientForm.value.lockerSearch?.toLowerCase() || '';

    const warehouseId = this.newClientForm.value.selectedWarehouse;

    const typeId = this.newClientForm.value.selectedLockerType;

    return this.availableLockers.filter((locker) => {
      const searchMatch =
        search === '' ||
        locker.identifier.toLowerCase().includes(search) ||
        (locker.features && locker.features.toLowerCase().includes(search));

      const warehouseMatch =
        warehouseId === 'all' || locker.warehouseId === Number(warehouseId);

      const typeMatch =
        typeId === 'all' || locker.lockerTypeId === Number(typeId);

      return searchMatch && warehouseMatch && typeMatch;
    });
  }

  get costSummary() {
    const assignedIds = this.newClientForm.value.lockersAsignados;

    if (!assignedIds || assignedIds.length === 0) {
      return { totalM3: 0 };
    }

    let totalM3 = 0;

    assignedIds.forEach((id: number) => {
      const locker = this.availableLockers.find((l) => l.id === id);

      const type = this.lockerTypes.find(
        (lt) => lt.id === locker?.lockerTypeId
      );

      if (type) {
        totalM3 += type.m3;
      }
    });

    return { totalM3 };
  }

  getLockerDetails(lockerId: number) {
    const locker = this.availableLockers.find((l) => l.id === lockerId);

    return {
      locker: locker,

      warehouse: this.warehouses.find((w) => w.id === locker?.warehouseId),

      lockerType: this.lockerTypes.find((lt) => lt.id === locker?.lockerTypeId),
    };
  }

  handleLockerToggle(lockerId: number): void {
    const assigned = this.newClientForm.get('lockersAsignados') as FormArray;

    const index = assigned.controls.findIndex(
      (ctrl) => ctrl.value === lockerId
    );

    if (index > -1) {
      assigned.removeAt(index);
    } else {
      assigned.push(this.fb.control(lockerId));
    }
  }

  // openNewClientModal(): void { this.showNewClientModal = true; }
  // closeNewClientModal(): void { this.showNewClientModal = false; this.newClientForm.reset(); /* Considerar reset a valores por defecto */ }

  onSubmit(): void {
    if (this.newClientForm.invalid) {
      this.newClientForm.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    const formValue = this.newClientForm.value;

    // Mapea los datos del formulario al DTO
    const dto: CreateClientDTO = {
      paymentIdentifier: Number(formValue.numeroIdentificacion) || 0,
      firstName: formValue.nombre,
      lastName: formValue.apellido,
      registrationDate: new Date(),
      notes: formValue.observaciones,
      dni: formValue.numeroDocumento,
      emails: formValue.emails,
      phones: formValue.telefonos,

      addressDto: {
        street: formValue.direccion,

        province: formValue.provincia,

        city: formValue.ciudad,
      },
      increaseFrequency: formValue.periodicidadAumento,

      increasePercentage: formValue.porcentajeAumento
        ? Number(formValue.porcentajeAumento)
        : 0,
      cuit: formValue.cuit,
      preferredPaymentMethodId: formValue.metodoPago.id,
      ivaCondition: formValue.condicionIVA,
      startDate: new Date(),
      contractedM3: this.costSummary.totalM3,
      amount: formValue.montoManual,
      lockerIds: formValue.lockersAsignados,
      userID: 1, // Placeholder, debería venir del usuario logueado
    };

    // Llama al servicio directamente desde el modal
    this.clientService.CreateClient(dto).subscribe({
      next: () => {
        this.isLoading = false;
        this.saveSuccess.emit(); // Emite la señal de éxito al padre
      },
      error: (err) => {
        this.isLoading = false;
        console.error('Error al crear el cliente:', err);
        // Opcional: podrías emitir un evento de error para que el padre muestre un toast de error.
        // this.saveError.emit(err);
      },
    });
  }

  private calculateTotalAmount(ids: number[]): number {
    let totalAmount = 0;
    ids.forEach((id) => {
      const locker = this.availableLockers.find((l) => l.id === id);
      const type = this.lockerTypes.find(
        (lt) => lt.id === locker?.lockerTypeId
      );
      if (type) {
        totalAmount += type.amount;
      }
    });
    return totalAmount;
  }
}
