import { Component } from '@angular/core';
import { PaymentService } from '../../core/services/payment-service/payment.service';
import { Payment } from '../../core/models/payment';
import { RentalService } from '../../core/services/rental-service/rental.service';
import { PaymentMethodService } from '../../core/services/paymentMethod-service/payment-method.service';
import { PendingRentalDTO } from '../../core/dtos/rental/PendingRentalDTO';
import { PaymentMethod } from '../../core/models/payment-method';
import { DetailedPaymentDTO } from '../../core/dtos/payment/DetailedPaymentDTO';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { IconComponent } from '../../shared/components/icon/icon.component';
import { NgxPaginationModule } from 'ngx-pagination';

@Component({
  selector: 'app-finances',
  imports: [FormsModule, CommonModule, IconComponent,NgxPaginationModule],
  templateUrl: './finances.component.html',
  styleUrl: './finances.component.css'
})
export class FinancesComponent{

  constructor
  (
    private paymentService: PaymentService,
    private rentalService: RentalService,
    private paymentMethodService: PaymentMethodService
  ){}

  pendingRentals: PendingRentalDTO[] = [];
  payments: DetailedPaymentDTO[] = [];
  paymentMethods: PaymentMethod[] = [];

  filteredPayments: DetailedPaymentDTO[] = [];
  searchPayment: string = '';

  page: number = 1;
  itemsPerPage: number = 10;

  ngOnInit(): void {
    this.LoadPayments();
    this.loadPaymentMethods();
  }

  LoadPayments(): void{
    this.paymentService.getDetailedPayment().subscribe({
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

  // filterPayments(): void {
  //   const term = this.searchPayment.toLowerCase().trim();

  //   this.filteredPayments = this.payments.filter(item => {
  //     const clientName = (item.clientName ?? '').toString().toLowerCase();
  //     const paymentIdentifier = (item.paymentIdentifier ?? '').toString().toLowerCase();
  //     const paymentMethod = (item.paymentMethodName ?? '').toString().toLowerCase();

  //     return (
  //       clientName.includes(term) ||
  //       paymentIdentifier.includes(term) ||
  //       paymentMethod.includes(term)
  //     );
  //   });

  //   this.filteredPayments.sort((a, b) => {
  //     const dateA = new Date(a.paymentDate).getTime();
  //     const dateB = new Date(b.paymentDate).getTime();
  //     return dateB - dateA;
  //   });
  // }

  selectedMethodFilter: string = '';
  selectedMonth: string = '';

  resetFilters(): void {
    this.searchPayment = '';
    this.selectedMethodFilter = '';
    this.selectedMonth = '';
    this.filteredPayments = [...this.payments];
  }

    getTotalRecaudado(): number {
      return this.payments.reduce((sum, p) => sum + p.amount, 0);
    }

    getPagosMesActual(): number {
      const currentMonth = new Date().getMonth();
      return this.payments.filter(p => new Date(p.paymentDate).getMonth() === currentMonth).length;
    }

    getPagosPorMetodo(method: string): number {
      return this.payments.filter(p => p.paymentMethodName === method).length;
    }

    uniqueClientsCount(): number {
      const uniqueClients = new Set(this.payments.map(p => p.clientName));
      return uniqueClients.size;
    }

   filterPayments(): void {
  const term = this.searchPayment.toLowerCase().trim();
  const method = this.selectedMethodFilter.toLowerCase();
  const month = this.selectedMonth;

  this.filteredPayments = this.payments.filter(p => {
    const clientName = p.clientName?.toLowerCase() || '';
    const paymentIdentifier = p.paymentIdentifier?.toLowerCase() || '';
    const warehouseName = p.warehouse_name?.toLowerCase() || '';
    const lockerIdentifier = p.lockerIdentifier?.toLowerCase() || '';
    const paymentMethodName = p.paymentMethodName?.toLowerCase() || '';

    const matchesSearch =
      clientName.includes(term) ||
      paymentIdentifier.includes(term) ||
      warehouseName.includes(term) ||   // 🆕 filtra por depósito
      lockerIdentifier.includes(term);  // 🆕 filtra por locker

    const matchesMethod =
      !method || paymentMethodName === method;

    const matchesMonth =
      !month ||
      new Date(p.paymentDate).toISOString().startsWith(month);

    return matchesSearch && matchesMethod && matchesMonth;
  });

  this.filteredPayments.sort(
    (a, b) => new Date(b.paymentDate).getTime() - new Date(a.paymentDate).getTime()
  );
}


}
