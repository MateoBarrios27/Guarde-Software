import { Component } from '@angular/core';
import { PendingRentalDTO } from '../../core/dtos/rental/PendingRentalDTO';
import { RentalService } from '../../core/services/rental-service/rental.service';
import { PaymentService } from '../../core/services/payment-service/payment.service';
import { CommonModule } from '@angular/common';
import { IconComponent } from '../../shared/components/icon/icon.component';
import { Payment } from '../../core/models/payment';
import { FormsModule } from '@angular/forms';
import { CreatePaymentDTO } from '../../core/dtos/payment/CreatePaymentDTO';

@Component({
  selector: 'app-dashboard',
  imports: [CommonModule, IconComponent, FormsModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent {

  pendingRentals: PendingRentalDTO[] = [];

  payments: Payment[] = [];

  showPaymentModal = false;
  selectedClientName = '';
  selectedPaymentIdentifier = 0;

  paymentDto: CreatePaymentDTO = {
    clientId: 0,
    rentalId: 0,
    movementType: 'CREDITO',
    concept: 'Pago de alquiler',
    amount: 0
  };
 
  constructor(private rentalService: RentalService, private paymentService: PaymentService) {}

  ngOnInit(): void {
    this.LoadPedingRentals();
    this.LoadPayments();
  }

  LoadPedingRentals(): void{
    this.rentalService.getPendingRental().subscribe({
      next: (data) => {
        this.pendingRentals = data;
        console.log('Pendientes cargados:', data);
      },
      error: (err) => console.error('Error al cargar pendientes:', err)
    });
  }

  LoadPayments(): void{
    this.paymentService.getPayment().subscribe({
      next: (data) => {
        this.payments = data;
      },
       error: (err) => console.error('Error al cargar payments:', err)
    });
  }

  openPaymentModalWith(item: PendingRentalDTO) {
    this.paymentDto = {
      clientId: item.clientId ?? 0,
      rentalId: item.id ?? 0,
      movementType: 'CREDITO',
      concept: `Pago alquiler`,
      amount: item.currentRent ?? 0
    };
    this.selectedClientName = item.clientName ?? '';
    this.selectedPaymentIdentifier = item.paymentIdentifier ?? '';
    this.showPaymentModal = true;
  }

  closePaymentModal() { this.showPaymentModal = false; }
  
}
