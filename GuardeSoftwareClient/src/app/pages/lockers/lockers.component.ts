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

  lockerUpdate : LockerUpdateDTO = {
    identifier: '',
    status: '',
    features: ''
  }

  lockerOriginal : LockerUpdateDTO = {
    identifier: '',
    status: '',
    features: ''
  }

  showUpdateLockerModal = false;
  idLockerUpdated = 0;
  warehouseId = 0;

  // filtros
  searchTerm = '';
  selectedWarehouse = '';
  selectedStatus = '';

  page: number = 1;
  itemsPerPage: number = 10;

  constructor(private lockerService: LockerService, private warehouseService: WarehouseService) {}

  ngOnInit(): void {
    this.loadLockers();
    this.loadWarehouses();
  }

  loadLockers(): void {
    this.lockerService.getLockers().subscribe({
      next: (data) => {
        this.lockers = data;
        console.log(data);
      },
      error: (err) => {
        console.error('Error cargando lockers', err);
      }
    });
  }

  loadWarehouses(): void {
    this.warehouseService.getWarehouses().subscribe({
      next: (data) => {
        this.warehouses = data;
      },
      error: (err) => {
        console.error('Error al cargar warehouses', err);
      }
    });
  }
 
  releaseLocker(locker: Locker): void {
    if (!locker.id) return;
    this.lockerService.updateLockerStatus(locker.id, { status: 'DISPONIBLE' }).subscribe({
      next: () => this.loadLockers(),
      error: (err) => console.error('Error liberando locker', err)
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
          confirmButton:
            'bg-blue-600 text-white px-4 py-2 p-2 rounded-md hover:bg-blue-700 transition-all duration-150',
          cancelButton:
            'bg-gray-200 text-gray-800 px-4 py-2 rounded-md hover:bg-gray-300 transition-all duration-150',
           actions:
            'flex justify-center gap-4 mt-4',
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
    return this.lockers.filter(l =>
      (this.selectedWarehouse ? l.warehouseId === +this.selectedWarehouse : true) &&
      (this.selectedStatus ? l.status === this.selectedStatus : true) &&
      (this.searchTerm
        ? (l.identifier?.toLowerCase().includes(this.searchTerm.toLowerCase()) ||
           l.features?.toLowerCase().includes(this.searchTerm.toLowerCase()))
        : true)
    );
  }

  getWarehouseName(id: number): string {
    const w = this.warehouses.find(w => w.id === id);
    return w ? w.name : 'N/A';
  }

  closeUpdateLockerModal() { this.showUpdateLockerModal = false; }

  openUpdateLockerModal(item: Locker){
    this.lockerUpdate = {
      identifier : item.identifier,
      status : item.status,
      features: item.features
    };
    this.lockerOriginal = {
      identifier : item.identifier,
      status : item.status,
      features: item.features
    }
    this.idLockerUpdated = item.id;
    this.warehouseId = item.warehouseId;
    this.showUpdateLockerModal = true;
  }

  saveLockerUpdated(id: number,dto: LockerUpdateDTO): void{

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

    const hasChanged = JSON.stringify(dto) !== JSON.stringify(this.lockerOriginal);

    if (!hasChanged) {
      Swal.fire({
            icon: 'warning',
            title: 'Error actualizando baulera',
            text: 'No se detectaron cambios para actualizar..',
            confirmButtonText: 'Entendido',
            confirmButtonColor: '#2563eb'
          });
      return;
    }

    this.lockerService.updateLocker(id,dto).subscribe({
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
}

