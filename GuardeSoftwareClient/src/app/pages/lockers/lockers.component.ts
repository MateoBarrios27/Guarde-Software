import { Component, OnInit } from '@angular/core';
import { Locker } from '../../core/models/locker';
import { LockerService } from '../../core/services/locker-service/locker.service';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-lockers',
  imports: [CommonModule],
  templateUrl: './lockers.component.html',
  styleUrl: './lockers.component.css'
})
export class LockersComponent implements OnInit {
  lockers: Locker[] = [];
  selectedLocker: Locker | null = null;

  constructor(private lockerService: LockerService) {}

  ngOnInit(): void {
    this.loadLockers();
  }

  loadLockers(): void {
    this.lockerService.getLockers().subscribe({
      next: (data) => {
        this.lockers = data;
        console.log('Lockers cargados:', this.lockers);
      },
      error: (err) => {
        console.error('Error cargando lockers', err);
      }
    });
  }

  liberarLocker(locker: Locker): void {
    if (!locker.id) return;
    this.lockerService.UpdateLockerStatus(locker.id, { status: 'DISPONIBLE' }).subscribe({
      next: () => {
        this.loadLockers();
      },
      error: (err) => console.error('Error liberando locker', err)
    });
  }

  borrarLocker(locker: Locker): void {
    if (!locker.id) return;
    if (confirm(`¿Seguro que quieres borrar el locker ${locker.id}?`)) {
      this.lockerService.deleteLocker(locker.id).subscribe({
        next: () => {
          this.loadLockers();
        },
        error: (err) => console.error('Error eliminando locker', err)
      });
    }
  }

  get ocupados(): number {
  return this.lockers.filter(l => l.status === 'OCUPADO').length;
  }

  get libres(): number {
    return this.lockers.filter(l => l.status === 'DISPONIBLE').length;
  }
}
