import { Component } from '@angular/core';
import { PendingRentalDTO } from '../../core/dtos/rental/PendingRentalDTO';
import { RentalService } from '../../core/services/rental-service/rental.service';
import { PaymentService } from '../../core/services/payment-service/payment.service';
import { CommonModule } from '@angular/common';
import { IconComponent } from '../../shared/components/icon/icon.component';
import { Payment } from '../../core/models/payment';
import { FormsModule } from '@angular/forms';
import { CreatePaymentDTO } from '../../core/dtos/payment/CreatePaymentDTO';
import { NgxPaginationModule } from 'ngx-pagination';
import { PaymentMethodService } from '../../core/services/paymentMethod-service/payment-method.service';
import { PaymentMethod } from '../../core/models/payment-method';

@Component({
  selector: 'app-dashboard',
  imports: [CommonModule, IconComponent, FormsModule, NgxPaginationModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent {

  //Pagination
  page: number = 1;
  itemsPerPage: number = 10;

  page2: number = 1;
  itemsPerPage2: number = 10;

  //load tables
  pendingRentals: PendingRentalDTO[] = [];
  payments: Payment[] = [];

  //load the payment methods
  paymentMethods: PaymentMethod[] = [];

  //filters
  searchPending: string = '';
  searchPayment: string = '';

  // Arrays filtered
  filteredPendingRentals: PendingRentalDTO[] = [];
  filteredPayments: Payment[] = [];

  //payments and modal
  showPaymentModal = false;
  selectedClientName = '';
  selectedPaymentIdentifier = 0;
  selectedBalance = 0;
  selectedCurrentRent = 0;

  //dto save payment
  paymentDto: CreatePaymentDTO = {
    clientId: 0,
    rentalId: 0,
    movementType: 'CREDITO',
    concept: 'Pago de alquiler',
    amount: 0,
    paymentMethodId: 0
  };
 
  constructor(
    private rentalService: RentalService,
    private paymentService: PaymentService,
    private paymentMethodService: PaymentMethodService
  ) {}

  ngOnInit(): void {
    this.LoadPedingRentals();
    this.LoadPayments();
    this.loadPaymentMethods();
  }

  LoadPedingRentals(): void{
    this.rentalService.getPendingRentals().subscribe({
      next: (data) => {
        this.filteredPendingRentals = data;
        this.pendingRentals = data;
      },
      error: (err) => console.error('Error al cargar pendientes:', err)
    });
  }

  LoadPayments(): void{
    this.paymentService.getPayments().subscribe({
      next: (data) => {
        const sorted = data.sort((a, b) => {
        const dateA = new Date(a.paymentDate).getTime();
        const dateB = new Date(b.paymentDate).getTime();
        return dateB - dateA; 
      });

      this.payments = sorted;
      this.filteredPayments = sorted;
        console.log(data);
      },
       error: (err) => console.error('Error al cargar payments:', err)
    });
  }

  loadPaymentMethods(): void {
    this.paymentMethodService.getPaymentMethods().subscribe({
      next: (data) =>{
        this.paymentMethods = data;
      },
      error: (err) => console.error('error al cargar los metodos de pago',err)
    });
  }

  getNamePaymentMethodById(id: number): string {
  const method = this.paymentMethods.find(m => m.id === id);
  return method ? method.name : 'Desconocido';
  }

  openPaymentModalWith(item: PendingRentalDTO) {

    const currentDate = new Date();
    const monthNames = [
      'enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio',
      'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'
    ];
    const currentMonth = monthNames[currentDate.getMonth()];

    this.paymentDto = {
      clientId: item.clientId ?? 0,
      rentalId: item.id ?? 0,
      movementType: 'CREDITO',
      concept: `Pago alquiler ${currentMonth}`,
      amount: 0,
      paymentMethodId: 1,
    };
    this.selectedClientName = item.clientName ?? '';
    this.selectedPaymentIdentifier = item.paymentIdentifier ?? '';
    this.selectedBalance = item.balance ?? '';
    this.selectedCurrentRent = item.currentRent ?? '';
    this.showPaymentModal = true;
  }

  closePaymentModal() { this.showPaymentModal = false; }

  savePayment(dto: CreatePaymentDTO): void {

    if (!dto.amount || dto.amount <= 0) {
      alert('⚠️ Debes ingresar un monto válido antes de guardar el pago.');
      return;
    }

    if (!dto.paymentMethodId) {
      alert('⚠️ Debes seleccionar un método de pago.');
      return;
    }
     
    this.paymentService.CreatePayment(dto).subscribe({
      next: () => {
        alert('✅ Pago registrado correctamente.');
        this.closePaymentModal();
        setTimeout(() => this.LoadPayments(), 100); 
        setTimeout(() => this.LoadPedingRentals(), 100);
      },
      error: (err) => console.error('Error al guardar payment:', err)
    });
  }

 filterPendingRentals(): void {
  const term = this.searchPending.toLowerCase().trim();

  this.filteredPendingRentals = this.pendingRentals.filter(item => {
    const clientName = (item.clientName ?? '').toString().toLowerCase();
    const paymentIdentifier = (item.paymentIdentifier ?? '').toString().toLowerCase();
    const lockerIdentifiers = (item.lockerIdentifiers ?? '').toString().toLowerCase();

    return (
      clientName.includes(term) ||
      paymentIdentifier.includes(term) ||
      lockerIdentifiers.includes(term)
    );
  });
}


filterPayments(): void {
  const term = this.searchPayment.toLowerCase().trim();

  this.filteredPayments = this.payments.filter(item => {
    const clientName = (item.clientName ?? '').toString().toLowerCase();
    const paymentIdentifier = (item.paymentIdentifier ?? '').toString().toLowerCase();
    const paymentMethodId = (item.paymentMethodId ?? '').toString().toLowerCase();

    return (
      clientName.includes(term) ||
      paymentIdentifier.includes(term) ||
      paymentMethodId.includes(term)
    );
  });

  this.filteredPayments.sort((a, b) => {
    const dateA = new Date(a.paymentDate).getTime();
    const dateB = new Date(b.paymentDate).getTime();
    return dateB - dateA;
  });
}
  
}
