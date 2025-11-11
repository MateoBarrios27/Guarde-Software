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
import { forkJoin, Observable } from 'rxjs';
import { BillingType } from '../../../core/models/billing-type.model';
import { BillingTypeService } from '../../../core/services/billingType-service/billing-type.service';
import Swal from 'sweetalert2';

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
    this.tryPopulateForm();
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
  // public increaseRegimens: IncreaseRegimen[] = [];
  public billingTypes: BillingType[] = [];
  isLoading: boolean = false;
  private areBasicDataLoaded = false;

  constructor(
    private fb: FormBuilder,
    private lockerService: LockerService,
    private warehouseService: WarehouseService,
    private paymentMethodService: PaymentMethodService,
    private increaseRegimenService: IncreaseRegimenService,
    private lockerTypeService: LockerTypeService,
    private billingTypeService: BillingTypeService,
    private clientService: ClientService
  ) {}

  ngOnInit(): void {
    this.initNewClientForm();
    this.loadFormData();
  }

  private loadFormData(): void {
    this.isLoading = true; 
    this.areBasicDataLoaded = false; 

    forkJoin({
      warehouses: this.warehouseService.getWarehouses(),
      lockers: this.lockerService.getLockers(),
      lockerTypes: this.lockerTypeService.getLockerTypes(),
      paymentMethods: this.paymentMethodService.getPaymentMethods(),
      billingTypes: this.billingTypeService.getBillingTypes()
    }).subscribe({
      next: (results) => {
        this.warehouses = results.warehouses;
        this.lockerTypes = results.lockerTypes;
        this.paymentMethods = results.paymentMethods;

        // Filtramos lockers disponibles + los del cliente actual
        this.availableLockers = results.lockers.filter(
          (l) => l.status.toLowerCase() === 'disponible' ||
                 (this.clientData?.lockersList?.some(assignedLocker => assignedLocker.id === l.id) ?? false)
        );
        this.billingTypes = results.billingTypes;
        console.log('Datos básicos cargados');
        this.areBasicDataLoaded = true;
        this.isLoading = false;
        this.tryPopulateForm(); // Intentar poblar ahora
      },
      error: (err) => {
        console.error('Error cargando datos para el modal:', err);
        this.isLoading = false;
        // --- MANEJO DE ERROR CON MODAL ---
        Swal.fire({
          icon: 'error',
          title: 'Error de Carga',
          text: 'No se pudieron cargar los datos necesarios (depósitos, tipos, etc.). Por favor, intente reabrir el modal.',
          confirmButtonColor: '#2563eb'
        });
      }
    });
  }

  private tryPopulateForm(): void {
    // Solo poblar si tenemos el formulario, los datos del cliente (si es edición) Y los datos básicos cargados
    if (this.newClientForm && this.areBasicDataLoaded && this._clientData) {
      console.log('Intentando poblar formulario ahora...');
      this.populateForm(this._clientData);
    } else if (this.newClientForm && this.areBasicDataLoaded && !this._clientData) {
        console.log("Datos básicos cargados, modo creación.");
    } else {
        console.log("Esperando datos básicos o datos del cliente...");
    }
  }

  private initNewClientForm(): void {
    this.newClientForm = this.fb.group({
      // --- Sección Personal ---
      numeroIdentificacion: [''],
      nombre: ['', Validators.required],
      apellido: ['', Validators.required],
      tipoDocumento: ['DNI'], 
      numeroDocumento: ['', Validators.required],
      cuit: [''],
      
      // --- Sección Contacto ---
      emails: this.fb.array([ this.fb.control('', [Validators.required, Validators.email]), ]),
      telefonos: this.fb.array([this.fb.control('')]),
      direccion: ['', Validators.required],
      ciudad: ['', Validators.required],
      codigoPostal: [''],
      provincia: ['', Validators.required],
      
      // --- Sección Pago ---
      condicionIVA: [null, Validators.required],
      metodoPago: [null, Validators.required],
      billingTypeId: [null, Validators.required],
      
      // --- Sección Financiera (Legacy) ---
      // Los campos inician deshabilitados. 
      // El HTML los habilitará/deshabilitará con el toggle.
      // El HTML también deshabilitará TODO si es modo EDICIÓN.
      isLegacyClient: [false], 
      legacyStartDate: [{ value: null, disabled: true }],
      legacyInitialAmount: [{ value: null, disabled: true }],
      legacyNextIncreaseDate: [{ value: null, disabled: true }],
      isLegacy6MonthPromo: [{ value: false, disabled: true }],
      prepaidMonths: [{ value: 0, disabled: true }], // También se deshabilita
      
      // --- Sección Observaciones ---
      observaciones: [''],
      
      // --- Sección Lockers ---
      lockersAsignados: this.fb.array([]),
      montoManual: [0, [Validators.required, Validators.min(0)]],
      lockerSearch: [''],
      selectedWarehouse: ['all'],
      selectedLockerType: ['all'],
      contractedM3: [0],
    });

    // Lógica para habilitar/deshabilitar los campos legacy dinámicamente
    this.newClientForm.get('isLegacyClient')?.valueChanges.subscribe(isLegacy => {
      const fields = ['legacyStartDate', 'legacyInitialAmount', 'legacyNextIncreaseDate', 'isLegacy6MonthPromo', 'prepaidMonths'];
      if (isLegacy) {
        fields.forEach(field => this.newClientForm.get(field)?.enable());
      } else {
        fields.forEach(field => this.newClientForm.get(field)?.disable());
      }
    });

     // Listener para recalcular monto
     const lockersAsignados = this.newClientForm.get('lockersAsignados') as FormArray;
     lockersAsignados.valueChanges.subscribe((ids: (number | null)[]) => {
       const validIds = ids.filter((id): id is number => typeof id === 'number' && id > 0);
       this.newClientForm.get('montoManual')?.setValue(this.calculateTotalAmount(validIds), { emitEvent: false });
       this.newClientForm.get('contractedM3')?.setValue(this.calculateTotalM3(validIds), { emitEvent: false });
     });

     // Añadir control para m3
     // this.newClientForm.addControl('contractedM3', this.fb.control(0)); // Ya está en el group
  }

    private populateForm(data: ClientDetailDTO): void {
      console.log('Poblando formulario con datos:', data);

      this.emails.clear();
      this.telefonos.clear();
      (this.newClientForm.get('lockersAsignados') as FormArray).clear(); 

      // Poblar emails
      if (data.email && data.email.length > 0) {
        data.email.forEach(e => this.emails.push(this.fb.control(e, [Validators.required, Validators.email])));
      } else { this.addEmail(); }

      // Poblar teléfonos
      if (data.phone && data.phone.length > 0) {
        data.phone.forEach(p => this.telefonos.push(this.fb.control(p)));
      } else { this.addTelefono(); }

      // Poblar lockers
       if (data.lockersList && data.lockersList.length > 0) {
           data.lockersList.forEach(locker => {
               if (locker && typeof locker.id === 'number' && locker.id > 0) {
                   (this.newClientForm.get('lockersAsignados') as FormArray).push(this.fb.control(locker.id));
               }
           });
           (this.newClientForm.get('lockersAsignados') as FormArray).updateValueAndValidity();
       }

      const matchingPaymentMethod = this.paymentMethods.find(
        method => method.name === data.preferredPaymentMethod 
      );

      // Poblar el formulario
      this.newClientForm.patchValue({
        numeroIdentificacion: data.paymentIdentifier,
        nombre: data.name,
        apellido: data.lastName,
        numeroDocumento: data.dni,
        cuit: data.cuit,
        condicionIVA: data.ivaCondition,
        metodoPago: matchingPaymentMethod || null,
        direccion: data.address,
        ciudad: data.city,
        provincia: data.province,
        observaciones: data.notes,
        montoManual: data.rentAmount,
        billingTypeId: data.billingTypeId || null,
        
        // --- POBLAR CAMPOS LEGACY (asumiendo que el DTO los trae) ---
        // Asignamos isLegacyClient basado en si tiene fecha de prox. aumento (una heurística)
        // O si el backend envía un flag 'isLegacyClient'
        // isLegacyClient: data.isLegacyClient, // <-- Ideal si el backend lo manda
        isLegacy6MonthPromo: data.increaseFrequencyMonths === 6,
        
        // (Estos campos se deshabilitarán en el HTML por estar en modo edición)
        legacyStartDate: data.registrationDate, // O data.startDate
        legacyInitialAmount: data.initialAmount, // Asume que el DTO trae esto
        legacyNextIncreaseDate: data.nextIncreaseDay, // Asume que el DTO trae esto
        // prepaidMonths: data.prepaidMonths, // Asume que el DTO trae esto
      });

      // Disparar cálculo de m3 inicial
      const initialLockerIds = this.newClientForm.get('lockersAsignados')?.value || [];
      const validInitialIds = initialLockerIds.filter((id: any): id is number => typeof id === 'number' && id > 0);
      this.newClientForm.get('contractedM3')?.setValue(this.calculateTotalM3(validInitialIds));
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
      // Usar el FormArray directamente
      const assignedIds = (this.newClientForm?.get('lockersAsignados')?.value as (number | null)[]) || [];
      // Filtrar IDs inválidos (null, undefined, 0, etc.)
      const validIds = assignedIds.filter((id): id is number => typeof id === 'number' && id > 0);

      // Calcular m3 usando la función dedicada
      const totalM3 = this.calculateTotalM3(validIds);

      return { totalM3 }; // Ya no necesitamos calcular amount aquí
  }

  getLockerDetails(lockerId: number | null) {
      // Si el ID es inválido, devolver algo vacío
      if (typeof lockerId !== 'number' || lockerId <= 0) {
           console.warn(`getLockerDetails llamado con ID inválido: ${lockerId}`);
          return { locker: null, warehouse: null, lockerType: null };
      }

      // Buscar locker en la lista de DISPONIBLES + los asignados originalmente
      const locker = this.availableLockers.find((l) => l.id === lockerId);
      if (!locker) {
           console.warn(`No se encontró locker con ID ${lockerId} en availableLockers`);
          return { locker: null, warehouse: null, lockerType: null };
      }

      const warehouse = this.warehouses.find((w) => w.id === locker.warehouseId);
      const lockerType = this.lockerTypes.find((lt) => lt.id === locker.lockerTypeId);

      if (!warehouse) console.warn(`No se encontró warehouse para locker ID ${lockerId} (warehouseId: ${locker.warehouseId})`);
      if (!lockerType) console.warn(`No se encontró lockerType para locker ID ${lockerId} (lockerTypeId: ${locker.lockerTypeId})`);

      return {
          locker: locker,
          warehouse: warehouse || null, // Devolver null si no se encuentra
          lockerType: lockerType || null, // Devolver null si no se encuentra
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

  // Reemplaza este método en create-client-modal.component.ts

  onSubmit(): void {
    // getRawValue() obtiene valores de campos deshabilitados (como los de legacy en modo edición)
    const formValue = this.newClientForm.getRawValue();
    if (this.newClientForm.invalid) {
      Object.values(this.newClientForm.controls).forEach(control => {
        if (control.invalid) {
          control.markAsTouched();
          control.updateValueAndValidity({ onlySelf: true });
        }
        if (control instanceof FormArray) {
           (control as FormArray).controls.forEach(arrayControl => {
             arrayControl.markAsTouched();
             arrayControl.updateValueAndValidity({ onlySelf: true });
           });
        }
      });
      console.warn('Formulario inválido:', formValue);
      Swal.fire('Formulario Inválido', 'Por favor, completa todos los campos requeridos (*).', 'warning');
      return;
    }

    if (!formValue.metodoPago || !formValue.metodoPago.id) {
        Swal.fire('Error de Validación', 'Método de pago no seleccionado o inválido.', 'error');
        return;
    }
    
    // --- 5. CAMBIO: Mapeo de DTO actualizado ---
    const isEditing = !!this.clientData;
    const isLegacy = formValue.isLegacyClient;
  
    if (isLegacy && !isEditing) { // Solo validamos requeridos al CREAR
        if (!formValue.legacyStartDate) {
            Swal.fire('Datos Incompletos', 'Para un cliente antiguo, la "Fecha de Ingreso Manual" es requerida.', 'warning');
            return;
        }
         if (!formValue.legacyInitialAmount || formValue.legacyInitialAmount <= 0) {
            Swal.fire('Datos Incompletos', 'Para un cliente antiguo, el "Monto Inicial" es requerido y debe ser mayor a 0.', 'warning');
            return;
        }
        if (!formValue.legacyNextIncreaseDate) {
            Swal.fire('Datos Incompletos', 'Para un cliente antiguo, la "Próxima Fecha de Aumento" es requerida.', 'warning');
            return;
        }
    }

    this.isLoading = true;

    const dto: CreateClientDTO = {
      id: isEditing ? this.clientData?.id : undefined,
      paymentIdentifier: Number(formValue.numeroIdentificacion) || 0,
      firstName: formValue.nombre,
      lastName: formValue.apellido,
      notes: formValue.observaciones || null,
      dni: formValue.numeroDocumento || null,
      cuit: formValue.cuit || null,
      billingTypeId: formValue.billingTypeId,
      emails: formValue.emails.filter((e: string | null): e is string => !!e && !!e.trim()),
      phones: formValue.telefonos.filter((p: string | null): p is string => !!p && !!p.trim()),
      addressDto: {
        street: formValue.direccion,
        city: formValue.ciudad,
        province: formValue.provincia,
      },
      preferredPaymentMethodId: formValue.metodoPago.id,
      ivaCondition: formValue.condicionIVA || null,
      contractedM3: this.costSummary.totalM3,
      amount: formValue.montoManual,
      lockerIds: formValue.lockersAsignados.filter((id: any): id is number => typeof id === 'number' && id > 0),
      userID: 1, // Placeholder

      // Lógica de fechas y prepago
      registrationDate: isLegacy ? formValue.legacyStartDate : (isEditing ? this.clientData?.registrationDate : new Date()),
      startDate: isLegacy ? formValue.legacyStartDate : (isEditing ? this.clientData?.registrationDate : new Date()),
      prepaidMonths: isLegacy ? Number(formValue.prepaidMonths) : 0,
      isLegacyClient: isLegacy,
      isLegacy6MonthPromo: formValue.isLegacy6MonthPromo,
      
      legacyInitialAmount: isLegacy ? formValue.legacyInitialAmount : null,
      legacyNextIncreaseDate: isLegacy ? formValue.legacyNextIncreaseDate : null,
    };

    console.log('Enviando DTO:', dto);

    // --- Llamada al Servicio (sin cambios) ---
    let apiCall: Observable<any>;

    if (isEditing) {
      apiCall = this.clientService.updateClient(this.clientData!.id, dto);
    } else {
      apiCall = this.clientService.CreateClient(dto);
    }

    apiCall.subscribe({
      next: (response) => {
        this.isLoading = false;
        console.log(isEditing ? 'Cliente actualizado:' : 'Cliente creado:', response);
        this.saveSuccess.emit();
      },
      error: (err) => {
        this.isLoading = false;
        console.error(isEditing ? 'Error al actualizar el cliente:' : 'Error al crear el cliente:', err);
        
        // --- MANEJO DE ERROR DETALLADO CON MODAL ---
        let errorDetails = '';
        if (err.error && err.error.errors) {
            // Error de validación de ASP.NET Core
            errorDetails = Object.entries(err.error.errors)
                                 .map(([key, value]) => `<b>${key}:</b> ${(value as string[]).join(', ')}`)
                                 .join('<br>');
        }
        
        const errorMsg = errorDetails || err.error?.message || err.statusText || 'Ocurrió un error desconocido.';
        
        Swal.fire({
            icon: 'error',
            title: `Error al ${isEditing ? 'actualizar' : 'crear'}`,
            html: `No se pudo guardar el cliente.<br><br><small class="text-left">${errorMsg}</small>`,
            confirmButtonColor: '#2563eb'
        });
      },
    });
  }


  private calculateTotalM3(ids: number[]): number {
     let totalM3 = 0;
     ids.forEach((id) => {
       const details = this.getLockerDetails(id);
       if (details.lockerType && details.lockerType.m3) {
         totalM3 += details.lockerType.m3;
       }
     });
     return Math.round(totalM3 * 100) / 100;
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
