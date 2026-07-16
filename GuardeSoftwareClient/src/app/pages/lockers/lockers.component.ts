import { AfterViewInit, Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { Locker } from '../../core/models/locker';
import { LockerService } from '../../core/services/locker-service/locker.service';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Warehouse } from '../../core/models/warehouse';
import { WarehouseService } from '../../core/services/warehouse-service/warehouse.service';
import { NgxPaginationModule } from 'ngx-pagination';
import { IconComponent } from '../../shared/components/icon/icon.component';
import { LockerUpdateDTO } from '../../core/dtos/locker/LockerUpdateDTO';
import Swal from 'sweetalert2';

// --- NUEVAS IMPORTACIONES ---
import { LockerType } from '../../core/models/locker-type';
import { LockerTypeService } from '../../core/services/lockerType-service/locker-type.service';
import { CreateLockerDTO } from '../../core/dtos/locker/CreateLockerDTO';

@Component({
  selector: 'app-lockers',
  standalone: true,
  imports: [CommonModule, FormsModule, NgxPaginationModule, IconComponent],
  templateUrl: './lockers.component.html',
  styleUrls: ['./lockers.component.css']
})
export class LockersComponent implements OnInit, AfterViewInit, OnDestroy {
  lockers: Locker[] = [];
  selectedLocker: Locker | null = null;
  
  warehouses: Warehouse[] = [];
  lockerTypes: LockerType[] = [];

  // --- Modal de Actualización ---
  lockerUpdate: LockerUpdateDTO = {
    identifier: '',
    status: '',
    features: '',
    lockerTypeId: 0,
    warehouseId: 0
  };
  lockerOriginal: LockerUpdateDTO = { ...this.lockerUpdate }; 
  showUpdateLockerModal = false;
  idLockerUpdated = 0;
  warehouseId = 0; 
  selectedLockerForEdit: Locker | null = null;

  // --- Modal de Creación  ---
  public showCreateLockerModal = false;
  public newLocker: CreateLockerDTO = this.getDefaultNewLocker();

  // filtros
  searchTerm = '';
  selectedWarehouse = '';
  selectedStatus = '';

  // sorting
  public sortField: string = 'identifier';
  public sortDirection: 'asc' | 'desc' = 'asc';

  // popover filters
  public showTagsPopover = false;
  public statusList: string[] = ['DISPONIBLE', 'OCUPADO', 'MANTENIMIENTO'];
  public selectedWarehouseIds: number[] = [];
  public selectedStatuses: string[] = [];
  public selectedLockerTypeIds: number[] = [];

  @ViewChild('tagsPopoverRef') tagsPopoverRef!: ElementRef;
  @ViewChild('tagsButtonRef') tagsButtonRef!: ElementRef;

  page: number = 1;
  itemsPerPage: number = 500; 

  // --- VARIABLES PARA EL BOTÓN FLOTANTE ---
  @ViewChild('bottomAnchor') bottomAnchor!: ElementRef;
  pointingUp: boolean = false;
  private scrollObserver!: IntersectionObserver;

  constructor(
    private lockerService: LockerService, 
    private warehouseService: WarehouseService,
    private lockerTypeService: LockerTypeService
  ) {}

  ngOnInit(): void {
    this.loadLockers();
    this.loadWarehouses();
    this.loadLockerTypes();
  }

  ngAfterViewInit() {
    this.scrollObserver = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        if (entry.isIntersecting) {
          this.pointingUp = true;
        } else {
          this.pointingUp = false;
        }
      });
    }, { threshold: 0 });

    if (this.bottomAnchor) {
      this.scrollObserver.observe(this.bottomAnchor.nativeElement);
    }
  }

  ngOnDestroy() {
    if (this.scrollObserver) {
      this.scrollObserver.disconnect();
    }
  }  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.showTagsPopover) return;
    const clickedInsidePopover = this.tagsPopoverRef && this.tagsPopoverRef.nativeElement.contains(event.target);
    const clickedInsideButton = this.tagsButtonRef && this.tagsButtonRef.nativeElement.contains(event.target);
    if (!clickedInsidePopover && !clickedInsideButton) {
      this.showTagsPopover = false;
    }
  }

  toggleTagsPopover(): void {
    this.showTagsPopover = !this.showTagsPopover;
  }

  toggleWarehouseId(id: number): void {
    const idx = this.selectedWarehouseIds.indexOf(id);
    if (idx > -1) {
      this.selectedWarehouseIds.splice(idx, 1);
    } else {
      this.selectedWarehouseIds.push(id);
    }
    this.page = 1;
  }

  toggleStatus(status: string): void {
    const idx = this.selectedStatuses.indexOf(status);
    if (idx > -1) {
      this.selectedStatuses.splice(idx, 1);
    } else {
      this.selectedStatuses.push(status);
    }
    this.page = 1;
  }

  toggleLockerTypeId(id: number): void {
    const idx = this.selectedLockerTypeIds.indexOf(id);
    if (idx > -1) {
      this.selectedLockerTypeIds.splice(idx, 1);
    } else {
      this.selectedLockerTypeIds.push(id);
    }
    this.page = 1;
  }

  clearAllTags(): void {
    this.selectedWarehouseIds = [];
    this.selectedStatuses = [];
    this.selectedLockerTypeIds = [];
    this.page = 1;
  }

  get totalActiveTagsCount(): number {
    return (
      this.selectedWarehouseIds.length +
      this.selectedStatuses.length +
      this.selectedLockerTypeIds.length
    );
  }

  handleSort(field: string): void {
    if (this.sortField === field) {
      if (this.sortDirection === 'asc') {
        this.sortDirection = 'desc';
      } else {
        this.sortField = 'identifier';
        this.sortDirection = 'asc';
      }
    } else {
      this.sortField = field;
      this.sortDirection = 'asc';
    }
  }

  getSortIcon(field: string): string {
    if (this.sortField !== field) {
      return 'arrow-up-down';
    }
    return this.sortDirection === 'asc' ? 'arrow-up' : 'arrow-down';
  }

  toggleScroll() {
    const scrollContainer = document.getElementById('main-scroll');
    if (!scrollContainer) return;

    if (this.pointingUp) {
      // 1. Si apunta arriba: Subimos al tope de un saque
      scrollContainer.scrollTo({ top: 0, behavior: 'smooth' });
    } else {
      // 2. Si apunta abajo: Bajamos en bloques (ej: 70 bauleras = ~3850px)
      const scrollAmount = 3850;
      scrollContainer.scrollBy({ top: scrollAmount, behavior: 'smooth' });
    }
  }

  loadLockers(): void {
    this.lockerService.getLockers().subscribe({
      next: (data) => { this.lockers = data; },
      error: (err) => { console.error('Error cargando lockers', err); }
    });
  }

  loadWarehouses(): void {
    this.warehouseService.getWarehouses().subscribe({
      next: (data) => { this.warehouses = data; },
      error: (err) => { console.error('Error al cargar warehouses', err); }
    });
  }

  loadLockerTypes(): void {
    this.lockerTypeService.getLockerTypes().subscribe({
      next: (data) => { this.lockerTypes = data; },
      error: (err) => { console.error('Error al cargar locker types', err); }
    });
  }
  
  deleteLocker(locker: Locker): void {
    if (!locker.id) return;

    if(locker.status == 'OCUPADO') {
      Swal.fire({
          icon: 'warning',
          title: 'Error al eliminar locker',
          text: 'No puedes dar de baja un locker actualmente ocupado.',
          confirmButtonText: 'Entendido',
          confirmButtonColor: '#2563eb'
        });
      return;
    }

    Swal.fire({
        title: '¿Seguro que quieres dar de baja la baulera?',
        html: `<p class="text-gray-700">Deposito: <b>${this.getWarehouseName(locker.warehouseId)}</b></p>
        <p class="text-gray-700">Identificador: <b>${locker.identifier}</b></p>`,
        icon: 'question',
        showCancelButton: true, 
        confirmButtonText: 'Confirmar',
        cancelButtonText: 'Cancelar',
        buttonsStyling: false,
        customClass: {
          confirmButton: 'bg-blue-600 text-white px-4 py-2 p-2 rounded-md hover:bg-blue-700 transition-all duration-150',
          cancelButton: 'bg-gray-200 text-gray-800 px-4 py-2 rounded-md hover:bg-gray-300 transition-all duration-150',
          actions: 'flex justify-center gap-4 mt-4',
          popup: 'rounded-xl shadow-lg'
        }
        }).then((result) => {
          if (result.isConfirmed) {
            this.lockerService.deleteLocker(locker.id).subscribe({
              next: () => {
                Swal.fire({
                    title: 'Baulera eliminada',
                    text: 'la baulera fue dada de baja correctamente.',
                    icon: 'success',
                    confirmButtonText: 'Aceptar',
                    confirmButtonColor: '#2563eb'
                  });
                setTimeout(() => this.loadLockers(), 100)
              },
              error: (err) => {
                console.error('Error deleting locker', err);
                Swal.fire({
                    title: 'Error',
                    text: 'Hubo un problema al dar de baja la baulera.',
                    icon: 'error',
                    confirmButtonText: 'Aceptar',
                    confirmButtonColor: '#2563eb'
                  });
              }
            });
          }
        });
  }

  get ocupados(): number {
    return this.filteredLockers.filter(l => l.status === 'OCUPADO').length;
  }
  get libres(): number {
    return this.filteredLockers.filter(l => l.status === 'DISPONIBLE').length;
  }
  get mantenimiento(): number {
    return this.filteredLockers.filter(l => l.status === 'MANTENIMIENTO').length;
  }

  get filteredLockers(): Locker[] {
    const filtered = this.lockers.filter(item => {
      const warehouseMatch = (this.selectedWarehouseIds.length === 0 && (!this.selectedWarehouse || item.warehouseId.toString() === this.selectedWarehouse)) ||
                             (this.selectedWarehouseIds.length > 0 && this.selectedWarehouseIds.includes(item.warehouseId));
      
      const statusMatch = (this.selectedStatuses.length === 0 && (!this.selectedStatus || item.status === this.selectedStatus)) ||
                          (this.selectedStatuses.length > 0 && this.selectedStatuses.includes(item.status));

      const lockerTypeMatch = this.selectedLockerTypeIds.length === 0 || this.selectedLockerTypeIds.includes(item.lockerTypeId);

      const searchLower = this.searchTerm.toLowerCase().trim();
      const searchMatch = !this.searchTerm || 
        item.identifier.toLowerCase().includes(searchLower) ||
        (item.features && item.features.toLowerCase().includes(searchLower)) ||
        (item.clientName && item.clientName.toLowerCase().includes(searchLower));

      return warehouseMatch && statusMatch && lockerTypeMatch && searchMatch;
    });

    return filtered.sort((a, b) => {
      let comparison = 0;

      if (this.sortField === 'identifier') {
        comparison = (a.identifier || '').localeCompare(b.identifier || '', undefined, { numeric: true, sensitivity: 'base' });
      } else if (this.sortField === 'warehouse') {
        comparison = this.getWarehouseName(a.warehouseId).localeCompare(this.getWarehouseName(b.warehouseId));
      } else if (this.sortField === 'lockerType') {
        comparison = this.getLockerTypeName(a.lockerTypeId).localeCompare(this.getLockerTypeName(b.lockerTypeId));
      } else if (this.sortField === 'features') {
        comparison = (a.features || '').localeCompare(b.features || '');
      } else if (this.sortField === 'status') {
        const weightA = this.getStatusSortWeight(a.status);
        const weightB = this.getStatusSortWeight(b.status);
        comparison = weightA !== weightB ? weightA - weightB : (a.status || '').localeCompare(b.status || '');
      } else if (this.sortField === 'clientName') {
        comparison = (a.clientName || '').localeCompare(b.clientName || '');
      }

      if (comparison === 0 && this.sortField !== 'identifier') {
        comparison = (a.identifier || '').localeCompare(b.identifier || '', undefined, { numeric: true, sensitivity: 'base' });
      }

      return this.sortDirection === 'asc' ? comparison : -comparison;
    });
  }

  getFilteredLockers(): Locker[] {
    return this.filteredLockers;
  }

  private getStatusSortWeight(status: string): number {
    switch (status) {
      case 'DISPONIBLE':
        return 1;
      case 'MANTENIMIENTO':
        return 2;
      case 'OCUPADO':
        return 3;
      default:
        return 4;
    }
  }

  getWarehouseName(id: number): string {
    const w = this.warehouses.find(w => w.id === id);
    return w ? w.name : 'N/A';
  }

  // --- NUEVOS MÉTODOS VISUALES ---
  getLockerTypeName(id: number): string {
    const lt = this.lockerTypes.find(lt => lt.id === id);
    return lt ? lt.name : 'N/A';
  }

  getStatusColor(status: string): string {
    switch (status) {
      case 'DISPONIBLE':
        return 'bg-green-100 text-green-800';
      case 'OCUPADO':
        return 'bg-red-100 text-red-800';
      case 'MANTENIMIENTO':
        return 'bg-yellow-100 text-yellow-800';
      default:
        return 'bg-gray-100 text-gray-800';
    }
  }

  // --- Métodos Modal Editar (Corregidos) ---
  closeUpdateLockerModal() { this.showUpdateLockerModal = false; }

  openUpdateLockerModal(item: Locker){
    this.selectedLockerForEdit = item;
    this.lockerUpdate = {
      identifier : item.identifier,
      status : item.status,
      features: item.features,
      lockerTypeId: item.lockerTypeId,
      warehouseId: item.warehouseId
    };
    this.lockerOriginal = { ...this.lockerUpdate };
    this.idLockerUpdated = item.id;
    this.warehouseId = item.warehouseId;
    this.showUpdateLockerModal = true;
  }

  saveLockerUpdated(id: number, dto: LockerUpdateDTO): void {
     if (!dto.identifier || dto.identifier.trim() === '' || !dto.warehouseId || dto.warehouseId === 0 || !dto.lockerTypeId || dto.lockerTypeId === 0 || !dto.status || dto.status.trim() === '') {
       Swal.fire({
            icon: 'warning',
            title: 'Error actualizando baulera',
            text: 'Debes completar todos los campos obligatorios antes de guardar.',
            confirmButtonText: 'Entendido',
            confirmButtonColor: '#2563eb'
          });
       return;
     }

     const hasChanged = dto.identifier !== this.lockerOriginal.identifier ||
                        dto.status !== this.lockerOriginal.status ||
                        dto.features !== this.lockerOriginal.features ||
                        dto.lockerTypeId !== this.lockerOriginal.lockerTypeId ||
                        dto.warehouseId !== this.lockerOriginal.warehouseId;

     if (!hasChanged) {
       Swal.fire({
            icon: 'info',
            title: 'Sin cambios',
            text: 'No se detectaron cambios para actualizar.',
            confirmButtonText: 'Entendido',
            confirmButtonColor: '#2563eb'
          });
       return;
     }

     const isAssignedToClient = Boolean(
       (this.selectedLockerForEdit?.rentalId && this.selectedLockerForEdit.rentalId > 0) ||
       (this.selectedLockerForEdit?.clientName && this.selectedLockerForEdit.clientName.trim() !== '') ||
       this.lockerOriginal.status === 'OCUPADO'
     );

     if (isAssignedToClient && dto.status === 'DISPONIBLE' && this.lockerOriginal.status !== 'DISPONIBLE') {
       const clientNameText = this.selectedLockerForEdit?.clientName && this.selectedLockerForEdit.clientName.trim() !== ''
         ? `al cliente <b>${this.selectedLockerForEdit.clientName}</b>`
         : 'a un cliente';

       Swal.fire({
         title: '¿Confirmar cambio a disponible?',
         html: `<p class="text-gray-700 mb-2">Estás poniendo en estado <b>DISPONIBLE</b> una baulera que actualmente le pertenece ${clientNameText}.</p>
                <p class="text-gray-700">Si confirmas, la baulera quedará disponible y se le quitará al cliente.</p>`,
         icon: 'warning',
         showCancelButton: true,
         confirmButtonText: 'Confirmar y quitar',
         cancelButtonText: 'Cancelar',
         buttonsStyling: false,
         customClass: {
           confirmButton: 'bg-blue-600 text-white px-4 py-2 rounded-md hover:bg-blue-700 transition-all duration-150',
           cancelButton: 'bg-gray-200 text-gray-800 px-4 py-2 rounded-md hover:bg-gray-300 transition-all duration-150 ml-4',
           actions: 'flex justify-center gap-4 mt-4',
           popup: 'rounded-xl shadow-lg'
         }
       }).then((result) => {
         if (result.isConfirmed) {
           this.executeLockerUpdate(id, dto);
         }
       });
       return;
     }

     this.executeLockerUpdate(id, dto);
  }

  private executeLockerUpdate(id: number, dto: LockerUpdateDTO): void {
     this.lockerService.updateLocker(id, dto).subscribe({
       next: () => {
         Swal.fire({
                  title: 'Baulera actualizada',
                  text: 'la baulera fue actualizada correctamente.',
                  icon: 'success',
                  confirmButtonText: 'Aceptar',
                  confirmButtonColor: '#2563eb'
                });
         this.closeUpdateLockerModal();
         setTimeout(() => this.loadLockers(), 100); 
       },
       error: (err) => {
         console.error('Error locker update', err)
         Swal.fire({
                  title: 'Error',
                  text: 'Hubo un problema al actualizar la baulera.',
                  icon: 'error',
                  confirmButtonText: 'Aceptar',
                  confirmButtonColor: '#2563eb'
                });
       }
     });
  }

  
  private getDefaultNewLocker(): CreateLockerDTO {
    return {
      identifier: '',
      warehouseId: 0,
      lockerTypeId: 0,
      features: '',
      status: 'DISPONIBLE'
    };
  }

  blurInput(event: Event): void {
    (event.target as HTMLElement).blur();
  }

  openCreateLockerModal(): void {
    this.newLocker = this.getDefaultNewLocker();
    this.showCreateLockerModal = true;
  }

  closeCreateLockerModal(): void {
    this.showCreateLockerModal = false;
  }

  saveNewLocker(): void {
    // --- Validación (Corregida) ---
    // CAMBIO: Validamos contra 0 en lugar de !this.newLocker.warehouseId
    if (!this.newLocker.identifier || !this.newLocker.warehouseId || this.newLocker.warehouseId === 0 || !this.newLocker.lockerTypeId || this.newLocker.lockerTypeId === 0) {
      Swal.fire({
        icon: 'error',
        title: 'Campos incompletos',
        text: 'Por favor, completa el Identificador, Depósito y Tipo de Locker.',
        confirmButtonColor: '#2563eb'
      });
      return;
    }

    this.lockerService.createLocker(this.newLocker).subscribe({
      next: () => {
        Swal.fire({
          title: 'Baulera Creada',
          text: `La baulera "${this.newLocker.identifier}" fue creada exitosamente.`,
          icon: 'success',
          confirmButtonText: 'Aceptar',
          confirmButtonColor: '#2563eb'
        });
        this.closeCreateLockerModal();
        this.loadLockers(); // Recargar la lista
      },
      error: (err) => {
        console.error('Error creando locker', err);
        Swal.fire({
          title: 'Error',
          text: 'Hubo un problema al crear la baulera. ' + (err.error?.message || ''),
          icon: 'error',
          confirmButtonText: 'Aceptar',
          confirmButtonColor: '#2563eb'
        });
      }
    });
  }
}

