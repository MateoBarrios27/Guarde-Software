import { Component, OnInit } from '@angular/core';
import { Locker } from '../../core/models/locker';
import { LockerService } from '../../core/services/locker-service/locker.service';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Warehouse } from '../../core/models/warehouse';
import { WarehouseService } from '../../core/services/warehouse-service/warehouse.service';

@Component({
  selector: 'app-lockers',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './lockers.component.html',
  styleUrls: ['./lockers.component.css']
})
export class LockersComponent implements OnInit {
  lockers: Locker[] = [];
  selectedLocker: Locker | null = null;

  warehouses: Warehouse[] = [];

  // filtros
  searchTerm = '';
  selectedWarehouse = '';
  selectedStatus = '';

 

  constructor(private lockerService: LockerService, private warehouseService: WarehouseService) {}

  ngOnInit(): void {
    this.loadLockers();
    this.loadWarehouses();
  }

  loadLockers(): void {
    this.lockerService.getLockers().subscribe({
      next: (data) => {
        this.lockers = data;
      },
      error: (err) => {
        console.error('Error cargando lockers', err);
      }
    });
  }

  loadWarehouses(): void {
    this.warehouseService.getWarehouse().subscribe({
      next: (data) => {
        this.warehouses = data;
        console.log('depositos: ',data);
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
    if (confirm(`¿Seguro que quieres borrar el locker ${locker.identifier}?`)) {
      this.lockerService.deleteLocker(locker.id).subscribe({
        next: () => this.loadLockers(),
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
}

