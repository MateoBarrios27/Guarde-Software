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
  private areBasicDataLoaded = false;

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
    this.isLoading = true; // Indicar carga
    this.areBasicDataLoaded = false; // Resetear flag

    // Usamos forkJoin para esperar a que TODAS las llamadas terminen
    forkJoin({
      warehouses: this.warehouseService.getWarehouses(),
      lockers: this.lockerService.getLockers(), // Traer TODOS, filtraremos después
      lockerTypes: this.lockerTypeService.getLockerTypes(),
      paymentMethods: this.paymentMethodService.getPaymentMethods(),
      // increaseRegimens: this.increaseRegimenService.getIncreaseRegimens() // Comentado si no se usa
    }).subscribe({
      next: (results) => {
        this.warehouses = results.warehouses;
        this.lockerTypes = results.lockerTypes;
        this.paymentMethods = results.paymentMethods;

        // Filtramos lockers disponibles + los del cliente actual
        this.availableLockers = results.lockers.filter(
          (l) => l.status.toLowerCase() === 'disponible' ||
                 // Check si el ID del locker está en la lista que viene del clientData
                 (this.clientData?.lockersList?.some(assignedLocker => assignedLocker.id === l.id) ?? false)
        );

        console.log('Datos básicos cargados');
        this.areBasicDataLoaded = true;
        this.isLoading = false;
        this.tryPopulateForm(); // Intentar poblar ahora
      },
      error: (err) => {
        console.error('Error cargando datos para el modal:', err);
        this.isLoading = false;
        // Mostrar un error al usuario
        this.showToastNotification('Error al cargar datos necesarios.', 'error');
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
        // Podrías resetear algo específico aquí si fuera necesario para 'Crear'
    } else {
        console.log("Esperando datos básicos o datos del cliente...");
    }
  }

  private initNewClientForm(): void {
      // ... (sin cambios aquí, asegúrate que 'telefonos' y 'emails' estén correctos) ...
    this.newClientForm = this.fb.group({
      // --- Sección Personal ---
      numeroIdentificacion: [''],
      nombre: ['', Validators.required],
      apellido: ['', Validators.required],
      tipoDocumento: ['DNI'], // Lo mantenemos por lógica, pero lo ocultamos en HTML
      numeroDocumento: ['', Validators.required],
      cuit: [''], // <-- 1. CAMBIO: CUIT añadido
      
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
      documento: [null], // Tipo Factura
      
      // --- Sección Financiera (Nueva) ---
      isLegacyClient: [false, Validators.required], // <-- 2. CAMBIO: Interruptor
      legacyStartDate: [null], // <-- 3. CAMBIO: Fecha de ingreso manual
      prepaidMonths: [0], // <-- 4. CAMBIO: Meses pagados
      billingType: [''],
      
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

     // Listener para recalcular monto (SIN CAMBIOS)
     const lockersAsignados = this.newClientForm.get('lockersAsignados') as FormArray;
     lockersAsignados.valueChanges.subscribe((ids: (number | null)[]) => {
       const validIds = ids.filter((id): id is number => typeof id === 'number' && id > 0);
       this.newClientForm.get('montoManual')?.setValue(this.calculateTotalAmount(validIds), { emitEvent: false });
       this.newClientForm.get('contractedM3')?.setValue(this.calculateTotalM3(validIds), { emitEvent: false });
     });

      // Añadir cálculo inicial de m3
      this.newClientForm.addControl('contractedM3', this.fb.control(0)); // Añadimos control para m3

  }

    private populateForm(data: ClientDetailDTO): void {
      console.log('Poblando formulario con datos:', data);

      this.emails.clear();
      this.telefonos.clear();
      (this.newClientForm.get('lockersAsignados') as FormArray).clear(); // Asegúrate que el nombre coincide

      // Poblar emails
      if (data.email && data.email.length > 0) {
        data.email.forEach(e => this.emails.push(this.fb.control(e, [Validators.required, Validators.email])));
      } else { this.addEmail(); }

      // Poblar teléfonos
      if (data.phone && data.phone.length > 0) {
        data.phone.forEach(p => this.telefonos.push(this.fb.control(p)));
      } else { this.addTelefono(); }

      // Poblar lockers (Asegúrate que lockersList tenga 'id')
       if (data.lockersList && data.lockersList.length > 0) {
           data.lockersList.forEach(locker => {
               // Verificar que locker.id exista y sea un número
               if (locker && typeof locker.id === 'number' && locker.id > 0) {
                   (this.newClientForm.get('lockersAsignados') as FormArray).push(this.fb.control(locker.id));
                   console.log(`Pushed locker ID: ${locker.id} to form array`); // Log para confirmar
               } else {
                   console.warn("Locker inválido encontrado en ClientDetailDTO durante populateForm:", locker);
               }
           });
           // Forzar actualización visual del form array (puede ayudar en algunos casos)
           (this.newClientForm.get('lockersAsignados') as FormArray).updateValueAndValidity();
           console.log("FormArray lockersAsignados después de poblar:", this.newClientForm.get('lockersAsignados')?.value);
       }

      const matchingPaymentMethod = this.paymentMethods.find(
        method => method.name === data.preferredPaymentMethod // Buscar por nombre si el DTO trae nombre
                 // O method => method.id === data.preferredPaymentMethodId // Si el DTO trae ID
      );

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
        provincia: data.province, // Ajusta si usas 'Province' en C# DTO
        observaciones: data.notes, // Asegúrate que ambos sean string
        montoManual: data.rentAmount,
        billingType: data.billingType,
        // No mapeamos lockersAsignados aquí, ya se hizo arriba
        // No mapeamos campos de aumento
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
    if (this.newClientForm.invalid) {
      // ... (lógica para marcar como touched) ...
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
      console.warn('Formulario inválido:', this.newClientForm.value);
      this.showToastNotification('Por favor, completa todos los campos requeridos.', 'error');
      return;
    }

    this.isLoading = true;
    const formValue = this.newClientForm.value;

    if (!formValue.metodoPago || !formValue.metodoPago.id) {
        console.error('Error: Método de pago no seleccionado o inválido.', formValue.metodoPago);
        this.showToastNotification('Error: Método de pago inválido.', 'error');
        this.isLoading = false;
        return;
    }
    
    // --- 5. CAMBIO: Mapeo de DTO actualizado ---
    const isEditing = !!this.clientData;
    const isLegacy = formValue.isLegacyClient;

    const dto: CreateClientDTO = {
      id: isEditing ? this.clientData?.id : undefined,
      paymentIdentifier: Number(formValue.numeroIdentificacion) || 0,
      firstName: formValue.nombre,
      lastName: formValue.apellido,
      notes: formValue.observaciones || null,
      dni: formValue.numeroDocumento || null,
      cuit: formValue.cuit || null,
      billingType: formValue.billingType || null,
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

      // --- Lógica de fechas y prepago (MODIFICADA) ---
      registrationDate: isLegacy ? formValue.legacyStartDate : (isEditing ? this.clientData?.registrationDate : new Date()),
      startDate: isLegacy ? formValue.legacyStartDate : (isEditing ? this.clientData?.registrationDate : new Date()),
      prepaidMonths: isLegacy ? Number(formValue.prepaidMonths) : 0,
      isLegacyClient: isLegacy, // <-- PROPIEDAD AÑADIDA
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
        let errorDetails = '';
        if (err.error && err.error.errors) {
            errorDetails = Object.entries(err.error.errors)
                                 .map(([key, value]) => `${key}: ${(value as string[]).join(', ')}`)
                                 .join('; ');
        }
        const errorMsg = errorDetails || err.error?.message || err.statusText || 'Ocurrió un error desconocido.';
        this.showToastNotification(`Error al guardar: ${errorMsg}`, 'error');
      },
    });
  }


  private calculateTotalM3(ids: number[]): number {
      let totalM3 = 0;
      ids.forEach((id) => {
          const details = this.getLockerDetails(id);
          if (details.lockerType && details.lockerType.m3) { // Verificar que m3 exista
              totalM3 += details.lockerType.m3;
          }
      });
      // Redondear a 2 decimales si es necesario
      return Math.round(totalM3 * 100) / 100;
   }

  // Asegúrate de tener este método para mostrar toasts
  private showToastNotification(message: string, type: 'success' | 'error'): void {
      // Tu lógica de toast
      console.log(`Toast (${type}): ${message}`); // Placeholder
  }

  // Asegúrate de tener este método para mostrar toasts (si no lo tienes ya)


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
