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
import { ClientService } from '../../core/services/client-service/client.service';
import { Client } from '../../core/models/client';
import { CreatePaymentDTO } from '../../core/dtos/payment/CreatePaymentDTO';
import Swal from 'sweetalert2';

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
    private paymentMethodService: PaymentMethodService,
    private clientService: ClientService
  ){}

  clients: Client[] = [];
  showClientModal = false;
  selectedClientId: number | 0 = 0;
  selectedClientName: string = '';
  selectedClientIdentifier: number | 0 = 0;

  pendingRentals: PendingRentalDTO[] = [];
  payments: DetailedPaymentDTO[] = [];
  paymentMethods: PaymentMethod[] = [];

  filteredPayments: DetailedPaymentDTO[] = [];
  searchPayment: string = '';

  page: number = 1;
  itemsPerPage: number = 10;

   //for logic of payment date edit
  manualDateEnabled = false;
  dateString: string = '';
  

  paymentDto: CreatePaymentDTO = {
      clientId: 0,
      movementType: 'CREDITO',
      concept: 'Pago de alquiler',
      amount: 0,
      paymentMethodId: 1,
      date: new Date()
    };

  ngOnInit(): void {
    this.loadPayments();
    this.loadPaymentMethods();
    this.loadClients();
  }

  loadPayments(): void{
    this.paymentService.getDetailedPayment().subscribe({
      next: (data) => {
        const sorted = data.sort((a, b) => {
        const dateA = new Date(a.paymentDate).getTime();
        const dateB = new Date(b.paymentDate).getTime();
        return dateB - dateA; 
      });

      this.payments = sorted;
      this.filteredPayments = sorted;
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

  loadClients(): void {
    this.clientService.getClients().subscribe({
      next: (data) => {
        this.clients = data;
        console.log(data);
      },
      error: (err) => console.log('error cargando clientes: ',err)
    });
    
  }

  getNamePaymentMethodById(id: number | string | null | undefined): string {
    if (id === null || id === undefined) return 'Desconocido';

    const numericId = Number(id);
    if (Number.isNaN(numericId)) return 'Desconocido';

    const method = this.paymentMethods.find(m => m.id === numericId);
    return method ? method.name : 'Desconocido';
  }

  getClientNameById(id: number): string {
  const client = this.clients.find(m => m.id === id);
  return client ? client.firstName + ' ' + client.lastName: ' ';
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
    const paymentMethodName = p.paymentMethodName?.toLowerCase() || '';

    const matchesSearch =
      clientName.includes(term) ||
      paymentIdentifier.includes(term)

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

closeClientModal() {
  this.showClientModal = false;

  this.selectedClientId = 0;
  this.selectedClientIdentifier = 0;
  this.selectedClientName = '';

  this.paymentDto = {
      clientId: 0,
      movementType: 'CREDITO',
      concept: ` `,
      amount: 0,
      paymentMethodId: 1,
      date: new Date()
    };
}

OpenPaymentModal(){
  this.showClientModal = true;

   const now = new Date();

    this.paymentDto = {
      clientId: 0,
      movementType: 'CREDITO',
      concept: ` `,
      amount: 0,
      paymentMethodId: 1,
      date: new Date()
    };

    this.updateConceptFromDate(now);

}

savePaymentModal(dto : CreatePaymentDTO){
  if (!dto.amount || dto.amount <= 0) {
        Swal.fire({
        icon: 'warning',
        title: 'Monto inválido',
        text: 'Debes ingresar un monto válido antes de guardar el pago.',
        confirmButtonText: 'Entendido',
        confirmButtonColor: '#2563eb', 
      });
        return;
      }
  
      if (!dto.paymentMethodId) {
         Swal.fire({
        icon: 'warning',
        title: 'Método de pago requerido',
        text: 'Debes seleccionar un método de pago antes de continuar.',
        confirmButtonText: 'Entendido',
        confirmButtonColor: '#2563eb'
      });
        return;
      }
      
      Swal.fire({
      title: '¿Deseas registrar este pago?',
      html: `<p class="text-gray-700">Medio de pago: <b>${this.getNamePaymentMethodById(dto.paymentMethodId)}</b></p>
             <p class="text-gray-700">Importe: <b>$${dto.amount}</b></p>`,
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
          this.paymentService.CreatePayment(dto).subscribe({
            next: () => {
              Swal.fire({
              title: 'Pago registrado',
              text: 'El pago fue registrado correctamente.',
              icon: 'success',
              confirmButtonText: 'Aceptar',
              confirmButtonColor: '#2563eb'
            });
              this.closeClientModal();
              setTimeout(() => this.loadPayments(), 100); 
            },
            error: (err) => {
            console.error('Error al guardar payment:', err);
            Swal.fire({
              title: 'Error',
              text: 'Hubo un problema al registrar el pago.',
              icon: 'error',
              confirmButtonText: 'Aceptar',
              confirmButtonColor: '#2563eb'
            });
            }
          });  
        }
      });
}

selectClient(client: any) {
  this.selectedClientId = client.id;
  this.selectedClientIdentifier = client.paymentIdentifier;
  this.paymentDto.clientId = client.id;
}



  toggleManualDate() {
    this.manualDateEnabled = !this.manualDateEnabled;

    if (!this.manualDateEnabled) {
      const now = new Date();
      this.paymentDto.date = now;
      this.dateString = now.toISOString().split('T')[0];
      this.updateConceptFromDate(now);
    }
  }  

  private updateConceptFromDate(date: Date) {
    const monthNames = [
      'enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio',
      'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'
    ];

    const monthName = monthNames[date.getMonth()];

    this.paymentDto.concept = `Pago alquiler ${monthName}`;
  }

  onManualDateChange(value: string) {
    if (!value) return;
    const [year, month, day] = value.split('-').map(Number);
    const currentTime = new Date();
    const dateWithTime = new Date(year, month - 1, day, currentTime.getHours(), currentTime.getMinutes(), currentTime.getSeconds());

    this.paymentDto.date = dateWithTime;
    this.dateString = value;
    this.updateConceptFromDate(dateWithTime);
  }

}
