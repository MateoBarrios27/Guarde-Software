import {
  Component,
  OnInit,
  Input,
  Output,
  EventEmitter,
  OnChanges,
  SimpleChanges,
  WritableSignal,
  signal,
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
import { PhoneInputDto } from '../../../core/dtos/phone/PhoneInputDto';
import { CurrencyFormatDirective } from "../../directives/currency-format.directive";
import { AuthService } from '../../../core/services/auth-service/auth.service';

@Component({
  selector: 'app-create-client-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, IconComponent, CurrencyFormatDirective],
  templateUrl: './create-client-modal.component.html',
})
export class CreateClientModalComponent implements OnInit, OnChanges {
  private _clientData: ClientDetailDTO | null = null;
  public isEditMode = false; 

  @Input() isReactivation = false;
  @Input() clientData: ClientDetailDTO | null = null;
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
  public phonesSignal: WritableSignal<PhoneInputDto[]> = signal([
    { number: '', whatsapp: true },
  ]);

    public assignmentMode: 'request' | 'direct' = 'request';

  constructor(
    private fb: FormBuilder,
    private lockerService: LockerService,
    private warehouseService: WarehouseService,
    private paymentMethodService: PaymentMethodService,
    private lockerTypeService: LockerTypeService,
    private billingTypeService: BillingTypeService,
    private clientService: ClientService,
    private authService: AuthService
  ) {
    this.newClientForm = this.fb.group({});
  }

  ngOnInit(): void {
    this.loadFormData();
  }

  ngOnChanges(changes: SimpleChanges): void {
    
    // Si cambia clientData o isReactivation, reinicializamos la lógica
    if (changes['clientData'] || changes['isReactivation']) {
        
        // Actualizamos variables locales
        this._clientData = this.clientData;
        this.isEditMode = !!this.clientData;

        // 1. Inicializar formulario (Aquí 'this.isReactivation' YA TIENE el valor correcto)
        this.initNewClientForm();

        // 2. Si ya tenemos los datos maestros (warehouses, etc), poblamos el form
        if (this.areBasicDataLoaded) {
            this.tryPopulateForm();
        }
    }
  }

  private loadFormData(): void {
    this.isLoading = true;
    this.areBasicDataLoaded = false;

    const commonObservables = {
      warehouses: this.warehouseService.getWarehouses(),
      paymentMethods: this.paymentMethodService.getPaymentMethods(),
      billingTypes: this.billingTypeService.getBillingTypes(),
      lockers: this.lockerService.getLockers(),           
      lockerTypes: this.lockerTypeService.getLockerTypes()
    };

    forkJoin(commonObservables).subscribe({
      next: (results: any) => {
        this.warehouses = results.warehouses;
        this.paymentMethods = results.paymentMethods;
        this.billingTypes = results.billingTypes;
        this.lockerTypes = results.lockerTypes; 

        this.availableLockers = results.lockers.filter(
          (l: Locker) =>
            l.status.toLowerCase() === 'disponible' ||
            (this.clientData?.lockersList?.some(
              (assignedLocker) => assignedLocker.id === l.id
            ) === true)
        );

        this.areBasicDataLoaded = true;
        this.isLoading = false;
        
        this.tryPopulateForm();
      },
      error: (err) => {
        this.isLoading = false;
        console.error('Error cargando datos:', err);
        Swal.fire('Error', 'No se pudieron cargar los datos necesarios.', 'error');
      },
    });
  }

  private tryPopulateForm(): void {
    if (this.newClientForm && Object.keys(this.newClientForm.controls).length > 0) {
        
        if (this.isEditMode && this._clientData) {
            console.log('Poblando formulario. Reactivación:', this.isReactivation);
            this.populateForm(this._clientData);
        } else if (!this.isEditMode) {
            // Modo Creación
            this.newClientForm.get('isLegacyClient')?.enable();
            this.phonesSignal.set([{ number: '', whatsapp: true }]);
        }
    }
  }

  private initNewClientForm(): void {
    // 1. Inicialización Base: TODOS los controles deben existir desde el principio
    this.newClientForm = this.fb.group({
      numeroIdentificacion: [''],
      nombreCompleto: ['', Validators.required],
      tipoDocumento: ['DNI'],
      numeroDocumento: ['', Validators.required],
      cuit: [''],
      emails: this.fb.array([
        this.fb.control('', [Validators.required, Validators.email]),
      ]),
      telefonos: this.fb.array([this.createPhoneGroup()]),
      direccion: ['', Validators.required],
      codigoPostal: [''],
      receiveCommunications: [true],
      condicionIVA: [null, Validators.required],
      metodoPago: [null, Validators.required],
      billingTypeId: [null, Validators.required],

      // Legacy
      isLegacyClient: [{ value: false, disabled: true }],
      legacyStartDate: [{ value: null, disabled: true }],
      legacyInitialAmount: [{ value: null, disabled: true }],
      legacyNextIncreaseDate: [{ value: null, disabled: true }],
      isLegacy6MonthPromo: [{ value: false, disabled: true }],
      prepaidMonths: [{ value: null, disabled: true }],

      observaciones: [''],
      montoManual: [null,[Validators.required, Validators.min(0)]],
      contractedM3: [0],
      occupiedSpaces: [null],

      // --- SIEMPRE INICIALIZAMOS LOS CONTROLES DE LOCKERS Y ESPACIOS ---
      spaceRequests: this.fb.array([]), // Para el modo 'request'
      lockersAsignados: this.fb.array([]), // Para el modo 'direct' o edición
      lockerSearch: [''],
      selectedWarehouse: ['all'],
      selectedLockerType: ['all'],
    });

    // 2. Suscripciones y lógica de negocio para los FormArrays
    
    // A. Lógica para Lockers Físicos
    const lockersAsignados = this.newClientForm.get('lockersAsignados') as FormArray;
    lockersAsignados.valueChanges.subscribe((ids: (number | null)[]) => {
      const validIds = ids.filter((id): id is number => typeof id === 'number' && id > 0);
      
      // Solo recalculamos los M3 basados en lockers físicos si estamos editando
      // o si estamos creando en modo "directo"
      if (this.isEditMode || (!this.isEditMode && this.assignmentMode === 'direct')) {
          this.newClientForm.get('contractedM3')?.setValue(this.calculateTotalM3(validIds), { emitEvent: false });
      }
    });

    const spaceRequests = this.newClientForm.get('spaceRequests') as FormArray;
    if (!this.isEditMode || this.isReactivation) {
        this.addSpaceRequest();
        this.setAssignmentMode(this.assignmentMode);
    }
    
    spaceRequests.valueChanges.subscribe((requests: any[]) => {
      let totalM3 = 0;
      requests.forEach((req) => {
        const m3 = parseFloat(req.m3) || 0;
        const qty = parseInt(req.quantity, 10) || 0;
        totalM3 += m3 * qty;
      });
      
      // Solo recalculamos los M3 basados en solicitudes si estamos creando en modo "request"
      if (!this.isEditMode && this.assignmentMode === 'request') {
          this.newClientForm.get('contractedM3')?.setValue(totalM3, { emitEvent: false });
      }
    });

    // 3. Suscripción Legacy
    this.newClientForm.get('isLegacyClient')?.valueChanges.subscribe((isLegacy) => {
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

  createPhoneGroup(): FormGroup {
    return this.fb.group({
      number: [''],
      whatsapp: [true],
    });
  }

  
  get spaceRequests(): FormArray {
    return this.newClientForm.get('spaceRequests') as FormArray;
  }

  createSpaceRequestGroup(): FormGroup {
    return this.fb.group({
      warehouseId: [null, Validators.required],
      m3: [null, [Validators.min(0)]], // <-- Le quitamos el Validators.required y cambiamos el min a 0
      quantity: [1, [Validators.required, Validators.min(1)]],
      comment: [''], // <-- Nuevo campo para el texto del comentario
      showComment: [false] // <-- Flag auxiliar para la UI (ocultar/mostrar input)
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
      
      // SOLO cargar lockers si NO es reactivación
      if (!this.isReactivation) {
          const lockersFormArray = this.newClientForm.get('lockersAsignados') as FormArray;
          if (lockersFormArray) {
            lockersFormArray.clear();
            if (data.lockersList && data.lockersList.length > 0) {
                data.lockersList.forEach(locker => {
                    if (locker?.id > 0) lockersFormArray.push(this.fb.control(locker.id));
                });
            }
          }
      }

      if (data.email?.length > 0) data.email.forEach(e => this.emails.push(this.fb.control(e, [Validators.required, Validators.email]))); else this.addEmail();
      
      if (data.phones && data.phones.length > 0) {
        data.phones.forEach(p => this.telefonos.push(this.fb.group({ number: [p.number], whatsapp: [p.whatsapp] })));
      } else {
        this.addTelefono(); 
      }

      const matchingPaymentMethod = this.paymentMethods.find(m => m.name === data.preferredPaymentMethod);

      this.newClientForm.patchValue({
        numeroIdentificacion: data.paymentIdentifier,
        nombreCompleto: data.fullName,
        numeroDocumento: data.dni,
        cuit: data.cuit,
        condicionIVA: data.ivaCondition,
        metodoPago: matchingPaymentMethod || null,
        direccion: data.address,
        observaciones: data.notes,
        montoManual: data.rentAmount,
        billingTypeId: data.billingTypeId || null,
        isLegacyClient: !!data.initialAmount, 
        legacyStartDate: this.formatDateToYYYYMMDD(data.registrationDate), 
        legacyInitialAmount: data.initialAmount, 
        legacyNextIncreaseDate: this.formatDateToYYYYMM(data.nextIncreaseDay),
        isLegacy6MonthPromo: data.increaseFrequencyMonths === 6,
        occupiedSpaces: data.occupiedSpaces || 0,
        receiveCommunications: data.receiveCommunications ?? true,
      });

      if (!this.isReactivation) {
          const initialLockerIds = this.newClientForm.get('lockersAsignados')?.value || [];
          const validInitialIds = initialLockerIds.filter((id: any): id is number => typeof id === 'number' && id > 0);
          this.newClientForm.get('contractedM3')?.setValue(this.calculateTotalM3(validInitialIds));
      }
  }

  get emails(): FormArray { return this.newClientForm.get('emails') as FormArray; }
  addEmail(): void { this.emails.push(this.fb.control('', [Validators.required, Validators.email])); }
  removeEmail(index: number): void { if (this.emails.length > 1) this.emails.removeAt(index); }
  
  get telefonos(): FormArray {
    return this.newClientForm.get('telefonos') as FormArray;
  }

  addTelefono(): void {
    this.telefonos.push(this.createPhoneGroup());
  }

  removeTelefono(index: number): void {
    if (this.telefonos.length > 1) {
      this.telefonos.removeAt(index);
    }
  }
  
  getWarehouseName(id: number | null): string {
    if (!id) return 'N/A';
    const w = this.warehouses.find(w => w.id === id);
    return w ? w.name : 'Desconocido';
  }
  
  get filteredLockers(): Locker[] {
    if (!this.isEditMode && this.assignmentMode !== 'direct') return []; 
    
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
    Object.values(this.newClientForm.controls).forEach((control) => {
      control.markAsTouched({ onlySelf: true });
      if (control instanceof FormArray) {
        (control as FormArray).controls.forEach((arrayControl) =>
          arrayControl.markAsTouched({ onlySelf: true })
        );
      }
    });
    console.warn('Formulario inválido:', formValue);
    Swal.fire(
      'Formulario Inválido',
      'Por favor, completa todos los campos requeridos (*).',
      'warning'
    );
    return;
  }

  // --- ROBUST PAYMENT METHOD VALIDATION ---
  const paymentMethodId = formValue.metodoPago?.id;
  if (!paymentMethodId || paymentMethodId <= 0) {
    console.error('Valor de metodoPago inválido:', formValue.metodoPago);
    Swal.fire(
      'Error de Validación',
      'Método de pago no seleccionado o inválido.',
      'error'
    );
    return;
  }

  const isEditing = this.isEditMode;
  const isLegacy = formValue.isLegacyClient;

  if (isLegacy && !isEditing) {
    if (
      !formValue.legacyStartDate ||
      !formValue.legacyInitialAmount ||
      !formValue.legacyNextIncreaseDate
    ) {
      Swal.fire(
        'Datos Incompletos',
        'Para un cliente antiguo, la Fecha de Ingreso, Monto Inicial y Próximo Aumento son requeridos.',
        'warning'
      );
      return;
    }
  }

  this.isLoading = true;

  let nextIncreaseDate: Date | null = null;
  let legacyStartDate: Date | null = null;

  if (isLegacy) {
    if (formValue.legacyNextIncreaseDate)
      nextIncreaseDate = new Date(
        formValue.legacyNextIncreaseDate + '-01T12:00:00Z'
      );
    if (formValue.legacyStartDate)
      legacyStartDate = new Date(formValue.legacyStartDate + 'T12:00:00Z');
  } else if (isEditing) {
    nextIncreaseDate = this.clientData?.nextIncreaseDay
      ? new Date(this.clientData.nextIncreaseDay)
      : null;
  }

  const safeRegistrationDate =
    isEditing && this.clientData?.registrationDate
      ? new Date(this.clientData.registrationDate)
      : new Date();

  const dto: CreateClientDTO = {
    id: isEditing ? this.clientData?.id : undefined,
    paymentIdentifier: Number(formValue.numeroIdentificacion) || 0,
    fullName: formValue.nombreCompleto,
    notes: formValue.observaciones || null,
    dni: formValue.numeroDocumento || null,
    cuit: formValue.cuit || null,
    billingTypeId: formValue.billingTypeId,
    emails: formValue.emails.filter(
      (e: string | null): e is string => !!e && !!e.trim()
    ),
    phones: formValue.telefonos
      .filter((p: any) => p.number && p.number.trim())
      .map((p: any) => ({
        number: p.number.trim(),
        whatsapp: !!p.whatsapp,
      })),

    addressDto: {
      street: formValue.direccion,
      city: '',
      province: '',
    },
    receiveCommunications: !!formValue.receiveCommunications,
    preferredPaymentMethodId: paymentMethodId,
    ivaCondition: formValue.condicionIVA || null,
    amount: formValue.montoManual,
    userID: 1, 

    registrationDate:
      isLegacy && legacyStartDate ? legacyStartDate : safeRegistrationDate,
    startDate:
      isLegacy && legacyStartDate ? legacyStartDate : safeRegistrationDate,

    prepaidMonths: isLegacy ? Number(formValue.prepaidMonths) : 0,
    isLegacyClient: isLegacy,
    isLegacy6MonthPromo: formValue.isLegacy6MonthPromo,
    legacyInitialAmount: isLegacy ? formValue.legacyInitialAmount : null,
    legacyNextIncreaseDate: nextIncreaseDate || undefined,

    lockerIds: [],
    spaceRequests: [],
    contractedM3: formValue.contractedM3 || 0,
    occupiedSpaces: formValue.occupiedSpaces || 0,
  };

  if (isEditing) {
      dto.lockerIds = formValue.lockersAsignados.filter(
        (id: any): id is number => typeof id === 'number' && id > 0
      );
      dto.spaceRequests = []; 
    } else {
      if (this.assignmentMode === 'request') {
        dto.spaceRequests = formValue.spaceRequests.map((req: any) => ({
           warehouseId: req.warehouseId,
           m3: req.m3 || 0,
           quantity: req.quantity,
           comment: req.comment || null
        }));
        dto.lockerIds = []; 
      } else {
        dto.spaceRequests = [];
        dto.lockerIds = formValue.lockersAsignados.filter(
          (id: any): id is number => typeof id === 'number' && id > 0
        );
      }
    }

  console.log('Enviando DTO:', dto);

  let apiCall: Observable<any>;
    
    if (this.isReactivation) {
      apiCall = this.clientService.reactivateClient(this.clientData!.id, dto);
    } else if (isEditing) {
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
        confirmButtonColor: '#2563eb',
      });
    },
    error: (err) => {
      this.isLoading = false;
      console.error('Error al guardar:', err);
      let errorDetails = '';
      if (err.error && err.error.errors) {
        errorDetails = Object.entries(err.error.errors)
          .map(
            ([key, value]) =>
              `<b>${key}:</b> ${(value as string[]).join(', ')}`
          )
          .join('<br>');
      }
      const errorMsg =
        errorDetails ||
        err.error?.message ||
        err.statusText ||
        'Ocurrió un error desconocido.';
      Swal.fire({
        icon: 'error',
        title: `Error al ${isEditing ? 'actualizar' : 'crear'}`,
        html: `No se pudo guardar el cliente.<br><br><small class="text-left">${errorMsg}</small>`,
        confirmButtonColor: '#2563eb',
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

  blurInput(event: Event): void {
    (event.target as HTMLElement).blur();
  }

  setAssignmentMode(mode: 'request' | 'direct'): void {
    this.assignmentMode = mode;
    const spaceRequests = this.newClientForm.get('spaceRequests') as FormArray;
    
    if (mode === 'direct') {
      spaceRequests.disable(); 
    } else {
      spaceRequests.enable();
    }
  }
}