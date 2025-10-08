import { Component, OnInit } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import {
  FormBuilder,
  FormGroup,
  FormArray,
  Validators,
  ReactiveFormsModule,
  FormsModule,
} from '@angular/forms';
import { NgxPaginationModule } from 'ngx-pagination';
import { IconComponent } from '../../shared/components/icon/icon.component';
import { PhonePipe } from '../../shared/pipes/phone.pipe';

// --- IMPORTACIÓN DE MODELOS ---
import { TableClient } from '../../core/dtos/client/TableClientDto';
import { GetClientsRequest } from '../../core/dtos/client/GetClientsRequest';
import { Locker } from '../../core/models/locker';
import { Warehouse } from '../../core/models/warehouse';
import { PaymentMethod } from '../../core/models/payment-method';
import { IncreaseRegimen } from '../../core/models/increase-regimen';
import { LockerType } from '../../core/models/locker-type'; // **NUEVO MODELO**

// --- IMPORTACIÓN DE SERVICIOS ---
import { ClientService } from '../../core/services/client-service/client.service';
import { LockerService } from '../../core/services/locker-service/locker.service';
import { WarehouseService } from '../../core/services/warehouse-service/warehouse.service';
import { PaymentMethodService } from '../../core/services/paymentMethod-service/payment-method.service';
import { IncreaseRegimenService } from '../../core/services/increaseRegimen-service/increase-regimen.service';
import { LockerTypeService } from '../../core/services/lockerType-service/locker-type.service';

import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';

@Component({
  selector: 'app-clients',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    IconComponent,
    CurrencyPipe,
    NgxPaginationModule,
    PhonePipe,
    ReactiveFormsModule,
  ],
  templateUrl: './clients.component.html',
})
export class ClientsComponent implements OnInit {
  // --- LÓGICA DE LA TABLA (SIN CAMBIOS) ---
  public activeTab: 'clientes' | 'pagos' = 'clientes';
  public clientes: TableClient[] = [];
  public totalClientes = 0;
  public isLoading = false;
  public estadisticas = {
    total: 0,
    alDia: 0,
    morosos: 0,
    pendientes: 0,
    dadosBaja: 0,
  };
  public searchClientes = '';
  private searchSubject = new Subject<string>();
  public filterEstadoClientes = 'Todos';
  public showInactivos = false;
  public currentPageClientes = 1;
  public itemsPerPageClientes = 10;
  public itemsPerPageOptions = [10, 20, 50];
  public sortFieldClientes = 'FirstName';
  public sortDirectionClientes: 'asc' | 'desc' = 'asc';
  public readonly Math = Math;

  public showNewClientModal = false;
  public newClientForm!: FormGroup;

  public warehouses: Warehouse[] = [];
  public availableLockers: Locker[] = [];
  public lockerTypes: LockerType[] = [];
  public paymentMethods: PaymentMethod[] = [];
  public increaseRegimens: IncreaseRegimen[] = [];


  constructor(
    private clientService: ClientService,
    private lockerService: LockerService,
    private warehouseService: WarehouseService,
    private paymentMethodService: PaymentMethodService,
    private increaseRegimenService: IncreaseRegimenService,
    private lockerTypeService: LockerTypeService,
    private fb: FormBuilder
  ) {}

  ngOnInit(): void {
    this.initNewClientForm();
    this.loadInitialData();
  }

  loadInitialData(): void {
    this.loadClients();
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
    this.paymentMethodService
      .getPaymentMethods()
      .subscribe((data) => (this.paymentMethods = data));
    this.increaseRegimenService
      .getIncreaseRegimens()
      .subscribe((data) => (this.increaseRegimens = data));
  }


private initNewClientForm(): void {
    this.newClientForm = this.fb.group({
      // --- CAMPO AÑADIDO ---
      numeroIdentificacion: [''], 
      
      nombre: ['', Validators.required],
      apellido: ['', Validators.required],
      tipoDocumento: ['DNI'],
      numeroDocumento: ['', Validators.required],
      emails: this.fb.array([this.fb.control('', [Validators.required, Validators.email])]),
      telefonos: this.fb.array([this.fb.control('')]),
      direccion: ['', Validators.required],
      ciudad: ['', Validators.required],
      provincia: ['', Validators.required],
      condicionIVA: [null, Validators.required],
      metodoPago: ['efectivo'],
      documento: [null, Validators.required],
      observaciones: [''],
      lockersAsignados: this.fb.array([], Validators.required),
      lockerSearch: [''],
      selectedWarehouse: ['all'],
      selectedLockerType: ['all'],
      periodicidadAumento: ['4'],
      porcentajeAumento: [''],

      // --- CAMPO AÑADIDO PARA EL MONTO MANUAL ---
      montoManual: [0, [Validators.required, Validators.min(0)]]
    });

    // --- LÓGICA AÑADIDA: Actualiza el monto manual cuando cambian los lockers ---
    const lockersAsignados = this.newClientForm.get('lockersAsignados') as FormArray;
    lockersAsignados.valueChanges.subscribe((ids: number[]) => {
      let totalAmount = 0;
      ids.forEach(id => {
        const locker = this.availableLockers.find(l => l.id === id);
        const type = this.lockerTypes.find(lt => lt.id === locker?.lockerTypeId);
        if (type) {
          totalAmount += type.amount;
        }
      });
      // Actualiza el campo del formulario con el nuevo total calculado
      this.newClientForm.get('montoManual')?.setValue(totalAmount);
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
      const locker = this.availableLockers.find(l => l.id === id);
      const type = this.lockerTypes.find(lt => lt.id === locker?.lockerTypeId);
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

  openNewClientModal(): void {
    this.showNewClientModal = true;
  }
  closeNewClientModal(): void {
    this.showNewClientModal = false;
    // Resets the form to initial state
    this.newClientForm.reset({
      tipoDocumento: 'DNI',
      metodoPago: 'efectivo',
      emails: [''],
      telefonos: [''],
      selectedWarehouse: 'all',
      selectedLockerType: 'all',
      lockerSearch: '',
    });
    this.emails.clear();
    this.addEmail();
    this.telefonos.clear();
    this.addTelefono();
    (this.newClientForm.get('lockersAsignados') as FormArray).clear();
  }

  onSubmit(): void {
    if (this.newClientForm.invalid) {
      this.newClientForm.markAllAsTouched();
      return;
    }
    console.log('Enviando al backend:', this.newClientForm.value);
  }

  loadClients(): void {
    this.isLoading = true;

    const request: GetClientsRequest = {
      pageNumber: this.currentPageClientes,

      pageSize: this.itemsPerPageClientes,

      sortField: this.sortFieldClientes,

      sortDirection: this.sortDirectionClientes,

      searchTerm: this.searchClientes || undefined,

      active: !this.showInactivos,
    };

    this.clientService.getTableClients(request).subscribe({
      next: (result) => {
        this.clientes = result.items;

        this.totalClientes = result.totalCount;

        this.estadisticas.total = result.totalCount;

        this.isLoading = false;
      },

      error: (err) => {
        console.error('Error al cargar clientes:', err);

        this.isLoading = false;
      },
    });
  }

  get totalPages(): number {
    return Math.ceil(this.totalClientes / this.itemsPerPageClientes);
  }

  handleSort(field: string): void {
    if (this.sortFieldClientes === field) {
      this.sortDirectionClientes =
        this.sortDirectionClientes === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortFieldClientes = field;

      this.sortDirectionClientes = 'asc';
    }

    this.loadClients();
  }

  onSearchChange(): void {
    this.searchSubject.next(this.searchClientes);
  }

  onFilterChange(): void {
    this.currentPageClientes = 1;

    this.loadClients();
  }

  onItemsPerPageChange(): void {
    this.currentPageClientes = 1;

    this.loadClients();
  }

  onPageChange(newPage: number): void {
    if (newPage > 0 && newPage <= this.totalPages) {
      this.currentPageClientes = newPage;

      this.loadClients();
    }
  }

  handleResetFilters(): void {
    this.searchClientes = '';

    this.filterEstadoClientes = 'Todos';

    this.showInactivos = false;

    this.currentPageClientes = 1;

    this.sortFieldClientes = 'FirstName';

    this.sortDirectionClientes = 'asc';

    this.loadClients();
  } // --- Style Helpers ---

  getEstadoBadgeColor(estado: string): string {
    const colors: Record<string, string> = {
      'Al día': 'bg-green-100 text-green-800',
      Moroso: 'bg-red-100 text-red-800',
      Pendiente: 'bg-yellow-100 text-yellow-800',
      Baja: 'bg-gray-200 text-gray-800',
    };

    return colors[estado] || 'bg-gray-100 text-gray-800';
  }

  getDocumentoBadgeColor(documento: string): string {
    const colors: Record<string, string> = {
      SF: 'bg-blue-100 text-blue-800',
      FB: 'bg-green-100 text-green-800',
      FA: 'bg-purple-100 text-purple-800',
      FBN: 'bg-orange-100 text-orange-800',
    };

    return colors[documento] || 'bg-gray-100 text-gray-800';
  }
}
