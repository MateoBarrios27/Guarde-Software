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
      alert('⚠️ No puedes eliminar un locker ocupado');
      return;
    }
    if (confirm(`¿Seguro que quieres borrar el locker ${locker.identifier}?`)) {
      this.lockerService.deleteLocker(locker.id).subscribe({
        next: () => setTimeout(() => this.loadLockers(), 100),
        error: (err) => console.error('Error eliminando locker', err)
      });
    }
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
      alert('⚠️ Debes ingresar un identificador antes de guardar el pago.');
      return;
    }

    const hasChanged = JSON.stringify(dto) !== JSON.stringify(this.lockerOriginal);

    if (!hasChanged) {
      alert('⚠️ No se detectaron cambios para actualizar.');
      return;
    }

    this.lockerService.updateLocker(id,dto).subscribe({
      next: () => {
        alert('✅ locker actualizado correctamente.');
        this.closeUpdateLockerModal();
        setTimeout(() => this.loadLockers(), 100); 
      },
      error: (err) => console.error('Error locker update', err)
    });
  }
}

