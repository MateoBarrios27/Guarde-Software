import {
  Component,
  OnInit,
  Input,
  Output,
  EventEmitter,
} from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common'; // Importar DatePipe
import {
  FormBuilder,
  FormGroup,
  FormArray,
  Validators,
  ReactiveFormsModule,
  AbstractControl 
} from '@angular/forms';
import { IconComponent } from '../icon/icon.component';
import Swal from 'sweetalert2';

// --- Modelos y Servicios ---
import { Locker } from '../../../core/models/locker';
import { Warehouse } from '../../../core/models/warehouse';
import { PaymentMethod } from '../../../core/models/payment-method';
import { LockerType } from '../../../core/models/locker-type';
import { ClientDetailDTO } from '../../../core/dtos/client/ClientDetailDTO';
import { CreateClientDTO } from '../../../core/dtos/client/CreateClientDTO';
import { LockerService } from '../../../core/services/locker-service/locker.service';
import { WarehouseService } from '../../../core/services/warehouse-service/warehouse.service';
import { PaymentMethodService } from '../../../core/services/paymentMethod-service/payment-method.service';
import { LockerTypeService } from '../../../core/services/lockerType-service/locker-type.service';
import { ClientService } from '../../../core/services/client-service/client.service';
import { forkJoin, Observable } from 'rxjs';
import { BillingType } from '../../../core/models/billing-type.model';
import { BillingTypeService } from '../../../core/services/billingType-service/billing-type.service';

@Component({
  selector: 'app-create-client-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, IconComponent], 
  templateUrl: './create-client-modal.component.html',
})
export class CreateClientModalComponent implements OnInit {
  // --- ENTRADAS Y SALIDAS ---
  private _clientData: ClientDetailDTO | null = null;
  public isEditMode = false; 

  @Input()
  set clientData(data: ClientDetailDTO | null) {
    this._clientData = data;
    this.isEditMode = !!data; 
    this.tryPopulateForm();
  }

  get clientData(): ClientDetailDTO | null {
    return this._clientData;
  }

  @Output() closeModal = new EventEmitter<void>();
  @Output() saveSuccess = new EventEmitter<void>();

  public newClientForm!: FormGroup;

  // --- Datos para Selects ---
  public warehouses: Warehouse[] = [];
  public availableLockers: Locker[] = [];
  public lockerTypes: LockerType[] = [];
  public paymentMethods: PaymentMethod[] = [];
  public billingTypes: BillingType[] = [];
  isLoading: boolean = false;
  private areBasicDataLoaded = false;

  constructor(
    private fb: FormBuilder,
    private lockerService: LockerService,
    private warehouseService: WarehouseService,
    private paymentMethodService: PaymentMethodService,
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
        this.billingTypes = results.billingTypes;

        this.availableLockers = results.lockers.filter(
          (l) => l.status.toLowerCase() === 'disponible' ||
                 (this.isEditMode && this.clientData?.lockersList?.some(assignedLocker => assignedLocker.id === l.id) === true)
        );
        
        console.log('Datos básicos cargados');
        this.areBasicDataLoaded = true;
        this.isLoading = false;
        this.tryPopulateForm();
      },
      error: (err) => {
        this.isLoading = false;
        console.error('Error cargando datos para el modal:', err);
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
    if (this.newClientForm && this.areBasicDataLoaded && this.isEditMode && this._clientData) {
      console.log('Intentando poblar formulario para edición...');
      this.populateForm(this._clientData);
    } else if (this.newClientForm && this.areBasicDataLoaded && !this.isEditMode) {
        console.log("Datos básicos cargados, modo creación.");
        this.newClientForm.get('isLegacyClient')?.enable(); // Habilitar el toggle
    }
  }

  private initNewClientForm(): void {
    this.newClientForm = this.fb.group({
      // Personal
      numeroIdentificacion: [''],
      nombre: ['', Validators.required],
      apellido: ['', Validators.required],
      tipoDocumento: ['DNI'], 
      numeroDocumento: ['', Validators.required],
      cuit: [''],
      
      // Contacto
      emails: this.fb.array([ this.fb.control('', [Validators.required, Validators.email]), ]),
      telefonos: this.fb.array([this.fb.control('')]),
      direccion: ['', Validators.required],
      ciudad: ['', Validators.required],
      codigoPostal: [''],
      provincia: ['', Validators.required],
      
      // Pago y Fiscal
      condicionIVA: [null, Validators.required],
      metodoPago: [null, Validators.required],
      billingTypeId: [null, Validators.required],
      
      // Legacy (Inician deshabilitados)
      isLegacyClient: [{ value: false, disabled: true }], 
      legacyStartDate: [{ value: null, disabled: true }],
      legacyInitialAmount: [{ value: null, disabled: true }],
      legacyNextIncreaseDate: [{ value: null, disabled: true }], // Este campo es YYYY-MM
      isLegacy6MonthPromo: [{ value: false, disabled: true }],
      prepaidMonths: [{ value: 0, disabled: true }], 
      
      observaciones: [''],
      
      // Lockers
      lockersAsignados: this.fb.array([]),
      montoManual: [0, [Validators.required, Validators.min(0)]],
      lockerSearch: [''],
      selectedWarehouse: ['all'],
      selectedLockerType: ['all'],
      contractedM3: [0],
    });

    // Habilitar/Deshabilitar campos Legacy SOLO si está en MODO CREACIÓN
    this.newClientForm.get('isLegacyClient')?.valueChanges.subscribe(isLegacy => {
      if (this.isEditMode) return; // No hacer nada si estamos editando

      const fields = ['legacyStartDate', 'legacyInitialAmount', 'legacyNextIncreaseDate', 'isLegacy6MonthPromo', 'prepaidMonths'];
      const requiredFields = ['legacyStartDate', 'legacyInitialAmount', 'legacyNextIncreaseDate'];

      if (isLegacy) {
        fields.forEach(field => this.newClientForm.get(field)?.enable());
        requiredFields.forEach(field => this.newClientForm.get(field)?.setValidators(Validators.required));
      } else {
        fields.forEach(field => {
          this.newClientForm.get(field)?.disable();
          this.newClientForm.get(field)?.clearValidators(); 
          this.newClientForm.get(field)?.reset(); 
        });
        this.newClientForm.get('isLegacy6MonthPromo')?.setValue(false);
        this.newClientForm.get('prepaidMonths')?.setValue(0);
      }
      fields.forEach(field => this.newClientForm.get(field)?.updateValueAndValidity());
    });

     // Listener para recalcular monto y m3
     const lockersAsignados = this.newClientForm.get('lockersAsignados') as FormArray;
     lockersAsignados.valueChanges.subscribe((ids: (number | null)[]) => {
       const validIds = ids.filter((id): id is number => typeof id === 'number' && id > 0);
       this.newClientForm.get('montoManual')?.setValue(this.calculateTotalAmount(validIds), { emitEvent: false });
       this.newClientForm.get('contractedM3')?.setValue(this.calculateTotalM3(validIds), { emitEvent: false });
     });
  }

  /**
   * Convierte '2025-10-25T00:00:00' (o un objeto Date) a un string '2025-10'
   * para el <input type="month">.
   */
  private formatDateToYYYYMM(date: Date | string | null | undefined): string | null {
    if (!date) return null;
    try {
      // Usar UTC para evitar problemas de zona horaria
      const d = new Date(date);
      const year = d.getUTCFullYear(); 
      const month = (d.getUTCMonth() + 1).toString().padStart(2, '0');
      return `${year}-${month}`;
    } catch (e) {
      console.error("Error al formatear fecha:", e);
      return null;
    }
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

      const matchingPaymentMethod = this.paymentMethods.find(m => m.name === data.preferredPaymentMethod);

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
        
        isLegacyClient: !!data.initialAmount, 
        
        legacyStartDate: data.registrationDate, 
        legacyInitialAmount: data.initialAmount, 
        legacyNextIncreaseDate: this.formatDateToYYYYMM(data.nextIncreaseDay), // <-- LÓGICA CORREGIDA
        isLegacy6MonthPromo: data.increaseFrequencyMonths === 6
        // prepaidMonths: 0, 
      });

      // Si estamos editando, deshabilitamos la sección legacy
      if (this.isEditMode) {
        this.newClientForm.get('isLegacyClient')?.disable();
      }

      const initialLockerIds = this.newClientForm.get('lockersAsignados')?.value || [];
      const validInitialIds = initialLockerIds.filter((id: any): id is number => typeof id === 'number' && id > 0);
      this.newClientForm.get('contractedM3')?.setValue(this.calculateTotalM3(validInitialIds));
  }

  // --- Getters (emails, telefonos) ---
  get emails(): FormArray { return this.newClientForm.get('emails') as FormArray; }
  addEmail(): void { this.emails.push(this.fb.control('', [Validators.required, Validators.email])); }
  removeEmail(index: number): void { if (this.emails.length > 1) this.emails.removeAt(index); }
  get telefonos(): FormArray { return this.newClientForm.get('telefonos') as FormArray; }
  addTelefono(): void { this.telefonos.push(this.fb.control('')); }
  removeTelefono(index: number): void { if (this.telefonos.length > 1) this.telefonos.removeAt(index); }

  // --- Getters (filteredLockers, costSummary, getLockerDetails) ---
  get filteredLockers(): Locker[] {
    const search = this.newClientForm.value.lockerSearch?.toLowerCase() || '';
    const warehouseId = this.newClientForm.value.selectedWarehouse;
    const typeId = this.newClientForm.value.selectedLockerType;

    return this.availableLockers.filter((locker) => {
      const searchMatch = search === '' || locker.identifier.toLowerCase().includes(search) || (locker.features && locker.features.toLowerCase().includes(search));
      const warehouseMatch = warehouseId === 'all' || locker.warehouseId === Number(warehouseId);
      const typeMatch = typeId === 'all' || locker.lockerTypeId === Number(typeId);
      return searchMatch && warehouseMatch && typeMatch;
    });
  }
  
  get costSummary() {
      const assignedIds = (this.newClientForm?.get('lockersAsignados')?.value as (number | null)[]) || [];
      const validIds = assignedIds.filter((id): id is number => typeof id === 'number' && id > 0);
      const totalM3 = this.calculateTotalM3(validIds);
      return { totalM3 };
  }
  
  getLockerDetails(lockerId: number | null) {
      if (typeof lockerId !== 'number' || lockerId <= 0) {
         return { locker: null, warehouse: null, lockerType: null };
      }
      const locker = this.availableLockers.find((l) => l.id === lockerId);
      if (!locker) {
         return { locker: null, warehouse: null, lockerType: null };
      }
      const warehouse = this.warehouses.find((w) => w.id === locker.warehouseId);
      const lockerType = this.lockerTypes.find((lt) => lt.id === locker.lockerTypeId);
      return {
          locker: locker,
          warehouse: warehouse || null,
          lockerType: lockerType || null,
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

  
  onSubmit(): void {
    const formValue = this.newClientForm.getRawValue();

    // Validar campos requeridos que están en el *ngIf
    const requiredLegacyFields = ['legacyStartDate', 'legacyInitialAmount', 'legacyNextIncreaseDate'];
    let legacyFieldsInvalid = false;
    if (formValue.isLegacyClient && !this.isEditMode) {
        for (const field of requiredLegacyFields) {
            const control = this.newClientForm.get(field);
            if (!control?.value) {
                control?.markAsTouched(); // Marcar para mostrar error
                legacyFieldsInvalid = true;
            }
        }
    }

    if (this.newClientForm.invalid || legacyFieldsInvalid) {
      Object.values(this.newClientForm.controls).forEach(control => {
        control.markAsTouched();
        control.updateValueAndValidity({ onlySelf: true });
        if (control instanceof FormArray) {
           (control as FormArray).controls.forEach(arrayControl => arrayControl.markAsTouched());
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
    
    this.isLoading = true;
    
    const isEditing = this.isEditMode;
    const isLegacy = formValue.isLegacyClient;

    // --- LÓGICA DE CONVERSIÓN DE FECHA ---
    let nextIncreaseDate: Date | null = null;
    let legacyStartDate: Date | null = null;

    if (isLegacy) {
        if(formValue.legacyNextIncreaseDate) {
            nextIncreaseDate = new Date(formValue.legacyNextIncreaseDate + '-01T12:00:00Z');
        }
        if(formValue.legacyStartDate) {
            legacyStartDate = new Date(formValue.legacyStartDate + 'T12:00:00Z');
        }
    } else if (isEditing) {
      // Si editamos un cliente NO-LEGACY, mantenemos su fecha ancla existente
      nextIncreaseDate = this.clientData?.nextIncreaseDay ? new Date(this.clientData.nextIncreaseDay) : null;
    }

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

      registrationDate: isLegacy ? legacyStartDate! : (isEditing ? new Date(this.clientData!.registrationDate) : new Date()),
      startDate: isLegacy ? legacyStartDate! : (isEditing ? new Date(this.clientData!.registrationDate) : new Date()),
      prepaidMonths: isLegacy ? Number(formValue.prepaidMonths) : 0,
      isLegacyClient: isLegacy,
      isLegacy6MonthPromo: formValue.isLegacy6MonthPromo, 
      
      legacyInitialAmount: isLegacy ? formValue.legacyInitialAmount : null,
      
      // --- CORRECCIÓN FINAL ---
      // Si nextIncreaseDate es null, envia 'undefined' para que coincida con el tipo `Date | undefined`
      legacyNextIncreaseDate: nextIncreaseDate || undefined,
    };

    console.log('Enviando DTO:', dto);

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
        
        let errorDetails = '';
        if (err.error && err.error.errors) {
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


  // --- Métodos Helper de Cálculo ---

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