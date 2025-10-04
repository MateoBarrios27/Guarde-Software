import { Component } from '@angular/core';
import { PendingRentalDTO } from '../../core/dtos/rental/pendingRentalDTO';
import { RentalService } from '../../core/services/rental-service/rental.service';
import { CommonModule } from '@angular/common';
import { IconComponent } from '../../shared/components/icon/icon.component';

@Component({
  selector: 'app-dashboard',
  imports: [CommonModule, IconComponent],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent {

  pendingRentals: PendingRentalDTO[] = [];

  constructor(private rentalService: RentalService) {}

  ngOnInit(): void {
    this.rentalService.getPendingRental().subscribe({
      next: (data) => {
        this.pendingRentals = data;
        console.log('Pendientes cargados:', data);
      },
      error: (err) => console.error('Error al cargar pendientes:', err)
    });
  }
}
