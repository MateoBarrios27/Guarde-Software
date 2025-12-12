import {
  Component,
  OnInit,
  Input,
  Output,
  EventEmitter,
} from '@angular/core';
import { CommonModule } from '@angular/common';
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
import { SpaceRequestDetailDto } from '../../../core/dtos/rentalSpaceRequest/GetSpaceRequestDetailDto';
import { CreateClientDTO } from '../../../core/dtos/client/CreateClientDTO'; 
import { LockerService } from '../../../core/services/locker-service/locker.service';
import { WarehouseService } from '../../../core/services/warehouse-service/warehouse.service';
import { PaymentMethodService } from '../../../core/services/paymentMethod-service/payment-method.service';
import { LockerTypeService } from '../../../core/services/lockerType-service/locker-type.service';
import { ClientService } from '../../../core/services/client-service/client.service';
import { forkJoin, Observable, of } from 'rxjs'; 
import { BillingType } from '../../../core/models/billing-type.model';
import { BillingTypeService } from '../../../core/services/billingType-service/billing-type.service';

@Component({
  selector: 'app-create-client-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, IconComponent],
  templateUrl: './create-client-modal.component.html',
})
export class CreateClientModalComponent implements OnInit {
  private _clientData: ClientDetailDTO | null = null;
  public isEditMode = false; 

  @Input()
  set clientData(data: ClientDetailDTO | null) {
    this._clientData = data;
    this.isEditMode = !!data; 
    
    // Inicializar formulario
    this.initNewClientForm(); 
    
    // Si los datos ya están cargados, poblar
    if (this.areBasicDataLoaded) {
        this.tryPopulateForm();
    }
  }

  get clientData(): ClientDetailDTO | null { return this._clientData; }

  @Output() closeModal = new EventEmitter<void>();
  @Output() saveSuccess = new EventEmitter<void>();

  public newClientForm!: FormGroup;

  // --- Datos ---
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
  ) {
    this.initNewClientForm();
  }

  ngOnInit(): void {
    this.loadFormData();
  }

  private loadFormData(): void {
    this.isLoading = true; 
    this.areBasicDataLoaded = false; 

    const commonObservables = {
      warehouses: this.warehouseService.getWarehouses(),
      paymentMethods: this.paymentMethodService.getPaymentMethods(),
      billingTypes: this.billingTypeService.getBillingTypes()
    };

    const editObservables = {
      lockers: this.isEditMode ? this.lockerService.getLockers() : of([] as Locker[]),
      lockerTypes: this.isEditMode ? this.lockerTypeService.getLockerTypes() : of([] as LockerType[])
    };

    forkJoin({ ...commonObservables, ...editObservables }).subscribe({
      next: (results: any) => {
        this.warehouses = results.warehouses;
        this.paymentMethods = results.paymentMethods;
        this.billingTypes = results.billingTypes;

        if (this.isEditMode) {
          this.lockerTypes = results.lockerTypes;
          this.availableLockers = results.lockers.filter(
            (l: Locker) => l.status.toLowerCase() === 'disponible' ||
                   (this.clientData?.lockersList?.some(assignedLocker => assignedLocker.id === l.id) === true)
          );
        }
        
        this.areBasicDataLoaded = true;
        this.isLoading = false;
        this.tryPopulateForm();
      },
      error: (err) => {
        this.isLoading = false;
        console.error('Error cargando datos:', err);
        Swal.fire('Error de Carga', 'No se pudieron cargar los datos necesarios.', 'error');
      }
    });
  }

  private tryPopulateForm(): void {
    if (this.newClientForm && this.isEditMode && this._clientData) {
      console.log('Intentando poblar formulario para edición...');
      this.populateForm(this._clientData);
    } else if (this.newClientForm && !this.isEditMode) {
        // Modo Creación: Solo habilitar legacy si se desea
        this.newClientForm.get('isLegacyClient')?.enable();
    }
  }

  private initNewClientForm(): void {
    this.newClientForm = this.fb.group({
      numeroIdentificacion: [''],
      nombre: ['', Validators.required],
      apellido: ['', Validators.required],
      tipoDocumento: ['DNI'], 
      numeroDocumento: ['', Validators.required],
      cuit: [''],
      emails: this.fb.array([ this.fb.control('', [Validators.required, Validators.email]), ]),
      telefonos: this.fb.array([this.fb.control('')]),
      direccion: ['', Validators.required],
      ciudad: ['', Validators.required],
      codigoPostal: [''],
      provincia: ['', Validators.required],
      condicionIVA: [null, Validators.required],
      metodoPago: [null, Validators.required],
      billingTypeId: [null, Validators.required],
      
      // Legacy
      isLegacyClient: [{ value: false, disabled: true }], 
      legacyStartDate: [{ value: null, disabled: true }],
      legacyInitialAmount: [{ value: null, disabled: true }],
      legacyNextIncreaseDate: [{ value: null, disabled: true }],
      isLegacy6MonthPromo: [{ value: false, disabled: true }],
      prepaidMonths: [{ value: 0, disabled: true }], 
      
      observaciones: [''],
      
      montoManual: [0, [Validators.required, Validators.min(0)]],
      contractedM3: [0],
      occupiedSpaces: [0], // <-- Nuevo campo, inicializado en 0
    });

    // Lógica Condicional Arrays
    if (this.isEditMode) {
      this.newClientForm.addControl('lockersAsignados', this.fb.array([]));
      this.newClientForm.addControl('lockerSearch', this.fb.control(''));
      this.newClientForm.addControl('selectedWarehouse', this.fb.control('all'));
      this.newClientForm.addControl('selectedLockerType', this.fb.control('all'));

      const lockersAsignados = this.newClientForm.get('lockersAsignados') as FormArray;
      lockersAsignados.valueChanges.subscribe((ids: (number | null)[]) => {
        const validIds = ids.filter((id): id is number => typeof id === 'number' && id > 0);
        this.newClientForm.get('contractedM3')?.setValue(this.calculateTotalM3(validIds), { emitEvent: false });
      });

    } else {
      this.newClientForm.addControl('spaceRequests', this.fb.array([]));
      this.addSpaceRequest();
      
      const spaceRequests = this.newClientForm.get('spaceRequests') as FormArray;
      spaceRequests.valueChanges.subscribe((requests: any[]) => {
          let totalM3 = 0;
          let totalSpaces = 0;
          requests.forEach(req => {
              const m3 = parseFloat(req.m3) || 0;
              const qty = parseInt(req.quantity, 10) || 0;
              totalM3 += (m3 * qty);
              totalSpaces += qty;
          });
          this.newClientForm.get('contractedM3')?.setValue(totalM3, { emitEvent: false });
          // Actualizar también occupiedSpaces automáticamente en creación
          // this.newClientForm.get('occupiedSpaces')?.setValue(totalSpaces, { emitEvent: false });
      });
    }

    this.newClientForm.get('isLegacyClient')?.valueChanges.subscribe(isLegacy => {
      if (this.isEditMode) return; 
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
  }
  
  get spaceRequests(): FormArray {
    return this.newClientForm.get('spaceRequests') as FormArray;
  }

  createSpaceRequestGroup(): FormGroup {
    return this.fb.group({
      warehouseId: [null, Validators.required],
      m3: [null, [Validators.required, Validators.min(0.1)]],
      quantity: [1, [Validators.required, Validators.min(1)]],
    });
  }

  addSpaceRequest(): void {
    this.spaceRequests.push(this.createSpaceRequestGroup());
  }

  removeSpaceRequest(index: number): void {
    if (this.spaceRequests.length > 0) { 
      this.spaceRequests.removeAt(index);
    }
  }

  updateQuantity(index: number, change: number): void {
      const control = this.spaceRequests.at(index).get('quantity');
      if (control) {
          let newValue = (control.value || 0) + change;
          if (newValue < 1) newValue = 1;
          control.setValue(newValue);
      }
  }

  private formatDateToYYYYMM(date: Date | string | null | undefined): string | null {
    if (!date) return null;
    try {
      const d = new Date(date);
      const year = d.getUTCFullYear(); 
      const month = (d.getUTCMonth() + 1).toString().padStart(2, '0');
      return `${year}-${month}`;
    } catch (e) { return null; }
  }
  
  private formatDateToYYYYMMDD(date: Date | string | null | undefined): string | null {
    if (!date) return null;
    try {
      const d = new Date(date);
      return d.toISOString().split('T')[0];
    } catch (e) { return null; }
  }

  private populateForm(data: ClientDetailDTO): void {
      this.emails.clear();
      this.telefonos.clear();
      
      const lockersFormArray = this.newClientForm.get('lockersAsignados') as FormArray;
      if (lockersFormArray) {
        lockersFormArray.clear();
        if (data.lockersList && data.lockersList.length > 0) {
            data.lockersList.forEach(locker => {
                if (locker?.id > 0) lockersFormArray.push(this.fb.control(locker.id));
            });
            lockersFormArray.updateValueAndValidity();
        }
      }

      if (data.email?.length > 0) data.email.forEach(e => this.emails.push(this.fb.control(e, [Validators.required, Validators.email]))); else this.addEmail();
      if (data.phone?.length > 0) data.phone.forEach(p => this.telefonos.push(this.fb.control(p))); else this.addTelefono();

      // --- CORRECCIÓN CLAVE PARA PAYMENT METHOD ---
      // Buscamos el objeto en la lista cargada que coincida con el nombre
      const matchingPaymentMethod = this.paymentMethods.find(m => m.name === data.preferredPaymentMethod);
      // Si no lo encuentra, null (lo cual es válido, pero el usuario tendrá que seleccionarlo)

      this.newClientForm.patchValue({
        numeroIdentificacion: data.paymentIdentifier,
        nombre: data.name,
        apellido: data.lastName,
        numeroDocumento: data.dni,
        cuit: data.cuit,
        condicionIVA: data.ivaCondition,
        metodoPago: matchingPaymentMethod || null, // Asignamos el OBJETO
        direccion: data.address,
        ciudad: data.city,
        provincia: data.province,
        observaciones: data.notes,
        montoManual: data.rentAmount,
        billingTypeId: data.billingTypeId || null,
        
        isLegacyClient: !!data.initialAmount, 
        legacyStartDate: this.formatDateToYYYYMMDD(data.registrationDate), 
        legacyInitialAmount: data.initialAmount, 
        legacyNextIncreaseDate: this.formatDateToYYYYMM(data.nextIncreaseDay),
        isLegacy6MonthPromo: data.increaseFrequencyMonths === 6,
        
        occupiedSpaces: data.occupiedSpaces || 0, // Poblar el nuevo campo
      });

      const initialLockerIds = this.newClientForm.get('lockersAsignados')?.value || [];
      const validInitialIds = initialLockerIds.filter((id: any): id is number => typeof id === 'number' && id > 0);
      this.newClientForm.get('contractedM3')?.setValue(this.calculateTotalM3(validInitialIds));
  }

  get emails(): FormArray { return this.newClientForm.get('emails') as FormArray; }
  addEmail(): void { this.emails.push(this.fb.control('', [Validators.required, Validators.email])); }
  removeEmail(index: number): void { if (this.emails.length > 1) this.emails.removeAt(index); }
  get telefonos(): FormArray { return this.newClientForm.get('telefonos') as FormArray; }
  addTelefono(): void { this.telefonos.push(this.fb.control('')); }
  removeTelefono(index: number): void { if (this.telefonos.length > 1) this.telefonos.removeAt(index); }
  
  getWarehouseName(id: number | null): string {
    if (!id) return 'N/A';
    const w = this.warehouses.find(w => w.id === id);
    return w ? w.name : 'Desconocido';
  }
  
  get filteredLockers(): Locker[] {
    if (!this.isEditMode) return []; 
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
  
  getLockerDetails(lockerId: number | null) {
      if (typeof lockerId !== 'number' || lockerId <= 0) return { locker: null, warehouse: null, lockerType: null };
      const locker = this.availableLockers.find((l) => l.id === lockerId);
      if (!locker) return { locker: null, warehouse: null, lockerType: null };
      const warehouse = this.warehouses.find((w) => w.id === locker.warehouseId);
      const lockerType = this.lockerTypes.find((lt) => lt.id === locker.lockerTypeId);
      return { locker: locker, warehouse: warehouse || null, lockerType: lockerType || null };
  }
  
  handleLockerToggle(lockerId: number): void {
    const assigned = this.newClientForm.get('lockersAsignados') as FormArray;
    const index = assigned.controls.findIndex((ctrl) => ctrl.value === lockerId);
    if (index > -1) assigned.removeAt(index); else assigned.push(this.fb.control(lockerId));
  }

  
  onSubmit(): void {
    const formValue = this.newClientForm.getRawValue();

    if (this.newClientForm.invalid) {
      Object.values(this.newClientForm.controls).forEach(control => {
        control.markAsTouched({ onlySelf: true });
        if (control instanceof FormArray) {
           (control as FormArray).controls.forEach(arrayControl => arrayControl.markAsTouched({ onlySelf: true }));
        }
      });
      console.warn('Formulario inválido:', formValue);
      Swal.fire('Formulario Inválido', 'Por favor, completa todos los campos requeridos (*).', 'warning');
      return;
    }

    // --- VALIDACIÓN DE MÉTODO DE PAGO ROBUSTA ---
    // Verificamos si existe el objeto Y si tiene ID.
    const paymentMethodId = formValue.metodoPago?.id;
    if (!paymentMethodId || paymentMethodId <= 0) {
        console.error('Valor de metodoPago inválido:', formValue.metodoPago);
        Swal.fire('Error de Validación', 'Método de pago no seleccionado o inválido.', 'error');
        return;
    }
    
    const isEditing = this.isEditMode;
    const isLegacy = formValue.isLegacyClient;
    
    if (isLegacy && !isEditing) { 
        if (!formValue.legacyStartDate || !formValue.legacyInitialAmount || !formValue.legacyNextIncreaseDate) {
            Swal.fire('Datos Incompletos', 'Para un cliente antiguo, la Fecha de Ingreso, Monto Inicial y Próximo Aumento son requeridos.', 'warning');
            return;
        }
    }

    this.isLoading = true;
    
    let nextIncreaseDate: Date | null = null;
    let legacyStartDate: Date | null = null;

    if (isLegacy) {
        if(formValue.legacyNextIncreaseDate) nextIncreaseDate = new Date(formValue.legacyNextIncreaseDate + '-01T12:00:00Z');
        if(formValue.legacyStartDate) legacyStartDate = new Date(formValue.legacyStartDate + 'T12:00:00Z');
    } else if (isEditing) {
      nextIncreaseDate = this.clientData?.nextIncreaseDay ? new Date(this.clientData.nextIncreaseDay) : null;
    }

    const safeRegistrationDate = isEditing && this.clientData?.registrationDate 
                                 ? new Date(this.clientData.registrationDate) 
                                 : new Date();

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
      preferredPaymentMethodId: paymentMethodId, // <-- USAMOS EL ID EXTRAÍDO
      ivaCondition: formValue.condicionIVA || null,
      amount: formValue.montoManual,
      userID: 1,
      
      registrationDate: isLegacy && legacyStartDate ? legacyStartDate : safeRegistrationDate,
      startDate: isLegacy && legacyStartDate ? legacyStartDate : safeRegistrationDate,
      
      prepaidMonths: isLegacy ? Number(formValue.prepaidMonths) : 0,
      isLegacyClient: isLegacy,
      isLegacy6MonthPromo: formValue.isLegacy6MonthPromo, 
      legacyInitialAmount: isLegacy ? formValue.legacyInitialAmount : null,
      legacyNextIncreaseDate: nextIncreaseDate || undefined,

      lockerIds: [],
      spaceRequests: [],
      contractedM3: formValue.contractedM3 || 0,
      occupiedSpaces: formValue.occupiedSpaces || 0, // <-- MAPEO DEL NUEVO CAMPO
    };
    
    if (isEditing) {
        dto.lockerIds = formValue.lockersAsignados.filter((id: any): id is number => typeof id === 'number' && id > 0);
    } else {
        dto.spaceRequests = formValue.spaceRequests;
    }

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
        this.saveSuccess.emit();
        Swal.fire({
          icon: 'success',
          title: `Cliente ${isEditing ? 'actualizado' : 'creado'}`,
          text: `El cliente se ha guardado correctamente.`,
          confirmButtonColor: '#2563eb'
        });
      },
      error: (err) => {
        this.isLoading = false;
        console.error('Error al guardar:', err);
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