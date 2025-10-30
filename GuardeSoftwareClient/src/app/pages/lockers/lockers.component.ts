import { Component, OnInit } from '@angular/core';
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
export class LockersComponent implements OnInit {
  lockers: Locker[] = [];
  selectedLocker: Locker | null = null;
  
  warehouses: Warehouse[] = [];
  lockerTypes: LockerType[] = []; // <-- NUEVA PROPIEDAD

  // --- Modal de Actualización (Corregido) ---
  lockerUpdate: LockerUpdateDTO = {
    identifier: '',
    status: '',
    features: '',
    lockerTypeId: 0 // Añadido para consistencia
  };
  lockerOriginal: LockerUpdateDTO = { ...this.lockerUpdate }; // Copia inicial
  showUpdateLockerModal = false;
  idLockerUpdated = 0;
  warehouseId = 0; // Solo para mostrar, no se edita

  // --- Modal de Creación (Corregido) ---
  public showCreateLockerModal = false;
  public newLocker: CreateLockerDTO = this.getDefaultNewLocker();

  // filtros
  searchTerm = '';
  selectedWarehouse = '';
  selectedStatus = '';

  page: number = 1;
  itemsPerPage: number = 10;

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
                console.error('Error eliminando baulera', err)
                Swal.fire({
                    title: 'Error',
                    text: 'Hubo un problema al eliminar la baulera.',
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
    const filtered = this.lockers.filter(l =>
      (this.selectedWarehouse ? l.warehouseId === +this.selectedWarehouse : true) &&
      (this.selectedStatus ? l.status === this.selectedStatus : true) &&
      (this.searchTerm
        ? (l.identifier?.toLowerCase().includes(this.searchTerm.toLowerCase()) ||
           l.features?.toLowerCase().includes(this.searchTerm.toLowerCase()))
        : true)
    );

    // Añadimos el ordenamiento
    return filtered.sort((a, b) => {
      const weightA = this.getStatusSortWeight(a.status);
      const weightB = this.getStatusSortWeight(b.status);

      if (weightA !== weightB) {
        return weightA - weightB; // Ordenar por estado
      }

      // Si el estado es el mismo, ordenar por identificador
      return (a.identifier || '').localeCompare(b.identifier || '');
    });
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
    this.lockerUpdate = {
      identifier : item.identifier,
      status : item.status,
      features: item.features,
      lockerTypeId: item.lockerTypeId // <-- CAMBIO: Añadido
    };
    this.lockerOriginal = { ...this.lockerUpdate }; // Copia correcta
    this.idLockerUpdated = item.id;
    this.warehouseId = item.warehouseId;
    this.showUpdateLockerModal = true;
  }

  saveLockerUpdated(id: number, dto: LockerUpdateDTO): void {
     if (!dto.identifier || dto.identifier.trim() === '') {
       Swal.fire({
            icon: 'warning',
            title: 'Error actualizando baulera',
            text: 'Debes ingresar un identificador antes de guardar.',
            confirmButtonText: 'Entendido',
            confirmButtonColor: '#2563eb'
          });
       return;
     }

     // El tipo de Locker no se puede editar desde aquí, solo identifier, status, features
     const hasChanged = dto.identifier !== this.lockerOriginal.identifier ||
                        dto.status !== this.lockerOriginal.status ||
                        dto.features !== this.lockerOriginal.features;

     if (!hasChanged) {
       Swal.fire({
            icon: 'info', // 'info' es mejor que 'warning'
            title: 'Sin cambios',
            text: 'No se detectaron cambios para actualizar.',
            confirmButtonText: 'Entendido',
            confirmButtonColor: '#2563eb'
          });
       return;
     }

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

  // --- Métodos Modal Crear (Corregidos) ---
  
  private getDefaultNewLocker(): CreateLockerDTO {
    // CAMBIO: Usamos 0 como valor por defecto, ya que null no es un 'number'
    return {
      identifier: '',
      warehouseId: 0,
      lockerTypeId: 0,
      features: '',
      status: 'DISPONIBLE'
    };
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

