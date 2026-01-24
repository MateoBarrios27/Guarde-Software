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
  
  searchClient: string = '';

  amountOriginal = 0;

  selectedPreferredPaymentId: number = 1;


  paymentDto: CreatePaymentDTO = {
      clientId: 0,
      movementType: 'CREDITO',
      concept: 'Pago de alquiler',
      amount: 0,
      paymentMethodId: 1,
      date: new Date(),
      isAdvancePayment: false,
      advanceMonths: null
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

  this.searchClient = '';
  this.selectedClientId = 0;
  this.selectedClientIdentifier = 0;
  this.selectedClientName = '';

  this.manualDateEnabled = false;
  const now = new Date();
  this.dateString = now.toISOString().split('T')[0];

  this.paymentDto = {
    clientId: 0,
    movementType: 'CREDITO',
    concept: ` `,
    amount: 0,
    paymentMethodId: 1,
    date: now,
    isAdvancePayment: false,
    advanceMonths: null
  };

  this.updateConceptFromDate(now);
}


OpenPaymentModal() {
  this.showClientModal = true;

  this.searchClient = '';
  this.selectedClientId = 0;
  this.selectedClientIdentifier = 0;


  const now = new Date();

  this.manualDateEnabled = false;
  this.dateString = now.toISOString().split('T')[0];

  this.paymentDto = {
    clientId: 0,
    movementType: 'CREDITO',
    concept: ` `,
    amount: 0,
    paymentMethodId: 1,
    date: now,
    isAdvancePayment: false,
    advanceMonths: null
  };

  this.updateConceptFromDate(now);
}


commision:number = 0;
newAmount: number = 0;

// AmountWithComission(amount: number, paymentMethodId: number): number{

//     const numericId = Number(paymentMethodId);
//     const method = this.paymentMethods.find(m => m.id === numericId);
//     this.commision = method ? method.commission : 0;
//     this.newAmount = amount + (amount * this.commision / 100);

//     return this.newAmount;
// }
private calcCommissions(selectedMethodId: number, preferredPaymentId: number) {
  const selectedCommission = this.getCommissionByMethodId(selectedMethodId);
  const includedCommission = this.getCommissionByMethodId(preferredPaymentId);
  const extraCommission = Math.max(0, selectedCommission - includedCommission);

  return { selectedCommission, includedCommission, extraCommission };
}


 private getCommissionByMethodId(paymentMethodId: number): number {
  const id = Number(paymentMethodId);
  if (!id || Number.isNaN(id)) return 0;
  const method = this.paymentMethods.find(m => m.id === id);
  return method?.commission ?? 0;
}


  AmountWithComission(amount: number, selectedMethodId: number, preferredPaymentId: number): number {
    const selectedCommission = this.getCommissionByMethodId(selectedMethodId);

    const includedCommission = this.getCommissionByMethodId(preferredPaymentId);

    const extraCommission = Math.max(0, selectedCommission - includedCommission);

    this.commision = extraCommission;

    const newAmount = amount + (amount * extraCommission / 100);
    this.newAmount = newAmount;

    return newAmount;
  }

savePaymentModal(dto : CreatePaymentDTO){


      if (!this.paymentMethods?.length) {
        Swal.fire({
          icon: 'warning',
          title: 'Cargando métodos de pago',
          text: 'Esperá a que carguen los métodos de pago para registrar el pago.',
          confirmButtonColor: '#2563eb'
        });
        return;
      }

      if (!this.paymentDto.clientId || this.paymentDto.clientId <= 0) {
        Swal.fire({
          icon: 'warning',
          title: 'Cliente requerido',
          text: 'Tenés que seleccionar un cliente antes de registrar el pago.',
          confirmButtonColor: '#2563eb'
        });
        return;
      }

      if (this.manualDateEnabled && this.dateString) {
        const [year, month, day] = this.dateString.split('-').map(Number);
        const currentTime = new Date();
        this.paymentDto.date = new Date(
          year,
          month - 1,
          day,
          currentTime.getHours(),
          currentTime.getMinutes(),
          currentTime.getSeconds()
        );
      }

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

      if (dto.isAdvancePayment) {
        if (
          dto.advanceMonths === null ||
          dto.advanceMonths === undefined ||
          isNaN(Number(dto.advanceMonths)) ||
          dto.advanceMonths < 1
        ) {
          Swal.fire({
            icon: 'warning',
            title: 'Pago adelantado incompleto',
            text: 'Debes ingresar la cantidad de meses adelantados (mínimo 1).',
            confirmButtonText: 'Entendido',
            confirmButtonColor: '#2563eb',
          });
          return;
        }
      }

      this.amountOriginal = dto.amount;

      const { selectedCommission, includedCommission, extraCommission } =
        this.calcCommissions(dto.paymentMethodId, this.selectedPreferredPaymentId);

      // Para mantener consistencia con tu variable usada en pantalla
      this.commision = extraCommission;

      dto.amount = this.AmountWithComission(dto.amount, dto.paymentMethodId, this.selectedPreferredPaymentId);
      
      Swal.fire({
      title: 'Confirmar registro de pago',
      html: `
        <div class="text-left space-y-3">
          <div class="pb-3 border-b border-gray-200">
            <div class="text-sm text-gray-500">Método de pago</div>
            <div class="text-base font-semibold text-gray-800">
              ${this.getNamePaymentMethodById(dto.paymentMethodId)}
            </div>
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="p-3 rounded-lg bg-gray-50 border border-gray-200">
              <div class="text-sm text-gray-500">Monto base</div>
              <div class="text-lg font-semibold text-gray-900">$${this.amountOriginal}</div>
            </div>

            <div class="p-3 rounded-lg bg-blue-50 border border-blue-200">
              <div class="text-sm text-blue-800">Total a cobrar</div>
              <div class="text-lg font-bold text-blue-900">$${dto.amount}</div>
            </div>
          </div>

          <div class="p-4 rounded-lg border border-gray-200 bg-white">
            <div class="text-base font-semibold text-gray-800 mb-2">Detalle de comisiones</div>

            <div class="flex justify-between text-sm text-gray-700">
              <span>Comisión del método</span>
              <span class="font-semibold">${selectedCommission}%</span>
            </div>

            <div class="flex justify-between text-sm text-gray-700 mt-1">
              <span>Comisión incluida (predefinida)</span>
              <span class="font-semibold">${includedCommission}%</span>
            </div>

            <div class="flex justify-between text-sm text-gray-700 mt-1 pt-2 border-t border-gray-200">
              <span>Comisión extra aplicada</span>
              <span class="font-semibold">${extraCommission}%</span>
            </div>

            ${
              extraCommission === 0
                ? `<div class="mt-2 text-xs text-gray-500">
                    No se aplica comisión extra porque el monto base ya contempla una comisión mayor o igual.
                  </div>`
                : `<div class="mt-2 text-xs text-gray-500">
                    Se aplica únicamente la diferencia de comisión por elegir un método con mayor porcentaje.
                  </div>`
            }
          </div>

          <div class="text-xs text-gray-500">
            Nota: el monto base ya incluye la comisión del método preferido del cliente.
          </div>
        </div>
      `,
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Registrar pago',
      cancelButtonText: 'Cancelar',
      buttonsStyling: false,
      customClass: {
        confirmButton:
          'bg-blue-600 text-white px-4 py-2 rounded-md hover:bg-blue-700 transition-all duration-150',
        cancelButton:
          'bg-gray-200 text-gray-800 px-4 py-2 rounded-md hover:bg-gray-300 transition-all duration-150',
        actions: 'flex justify-end gap-3 mt-4',
        popup: 'rounded-xl shadow-lg p-4',
        icon: 'scale-60 mt-1',
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
        dto.amount = this.amountOriginal;
      });
}

selectClient(client: any) {
  this.selectedClientId = client.id;
  this.selectedClientIdentifier = client.paymentIdentifier;
  this.paymentDto.clientId = client.id;
  
  this.selectedPreferredPaymentId = Number(client.preferredPaymentMethodId ?? 1); 
  console.log(client.preferredPaymentMethodId);
  console.log('preferred:', this.selectedPreferredPaymentId);
}



  toggleManualDate() {
  this.manualDateEnabled = !this.manualDateEnabled;

  if (!this.manualDateEnabled) {
    const now = new Date();
    this.paymentDto.date = now;
    this.dateString = now.toISOString().split('T')[0];

    if (this.paymentDto.isAdvancePayment) {
      this.updateAdvanceConcept();
    } else {
      this.updateConceptFromDate(now);
    }
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
  const dateWithTime = new Date(
    year,
    month - 1,
    day,
    currentTime.getHours(),
    currentTime.getMinutes(),
    currentTime.getSeconds()
  );

  this.paymentDto.date = dateWithTime;
  this.dateString = value;

  if (this.paymentDto.isAdvancePayment) {
    this.updateAdvanceConcept();
  } else {
    this.updateConceptFromDate(dateWithTime);
  }
}


    get filteredClients(): Client[] {
      const term = this.searchClient?.toLowerCase().trim();

      if (!term) return this.clients;

      return this.clients.filter(c => {
        const fullName = `${c.firstName ?? ''} ${c.lastName ?? ''}`.toLowerCase();
        const identifier = (c.paymentIdentifier ?? '').toString().toLowerCase();

        return fullName.includes(term) || identifier.includes(term);
      });
    }

    private updateAdvanceConcept() {
      const months = this.paymentDto.advanceMonths;

      if (months === null || months === undefined || months === 0) {
        this.paymentDto.concept = 'Pago adelantado';
        return;
      }

      this.paymentDto.concept = `Pago adelantado de ${months} mes${months === 1 ? '' : 'es'}`;
    }

    onAdvancePaymentToggle() {
      if (!this.paymentDto.isAdvancePayment) {
        this.paymentDto.advanceMonths = null;
        this.updateConceptFromDate(this.paymentDto.date);
      } else {
        this.updateAdvanceConcept();
      }
    }

    onAdvanceMonthsChange() {
      if (this.paymentDto.isAdvancePayment) {
        this.updateAdvanceConcept();
      }
    }


}
