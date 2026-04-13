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
import Swal from 'sweetalert2';
import { PdfGeneratorService } from '../../core/services/pdfGenerator-service/pdf-generator.service';
import { CurrencyFormatDirective } from '../../shared/directives/currency-format.directive';

@Component({
  selector: 'app-dashboard',
  imports: [CommonModule, IconComponent, FormsModule, NgxPaginationModule, CurrencyFormatDirective],
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
  receiptDateStr: string = '';
  
  //receipt modal
  showReceiptModal = false;
  receiptPaymentInfo: Payment | null = null;
  receiptConcepts: { description: string, amount: number }[] = [];

  //dto save payment
  paymentDto: CreatePaymentDTO = {
    clientId: 0,
    movementType: 'CREDITO',
    concept: 'Pago de alquiler',
    amount: 0,
    paymentMethodId: 0,
    date: new Date(),
    isAdvancePayment: false,
    advanceMonths: null
  };

  //for logic of payment date edit
  manualDateEnabled = false;
  dateString: string = '';

  amountOriginal= 0;
  isAdvancePayment = false;
  advanceMonths: number | null = null;

  selectedPreferredPaymentId: number = 1;
  commision:number = 0;
  newAmount: number = 0;

  constructor(
    private rentalService: RentalService,
    private paymentService: PaymentService,
    private paymentMethodService: PaymentMethodService,
    private pdfGeneratorService: PdfGeneratorService
  ) {}

  ngOnInit(): void {
    this.LoadPedingRentals();
    this.LoadPayments();
    this.loadPaymentMethods();
  }

  private updateAdvanceConcept() {
    const months = this.paymentDto.advanceMonths;

    if (months === null || months === undefined || months === 0) {
      this.paymentDto.concept = 'Pago adelantado';
      return;
    }

    this.paymentDto.concept = `Pago adelantado de ${months} mes${months === 1 ? '' : 'es'}`;
  }

  formatARS = (value: number) => {
    return new Intl.NumberFormat('es-AR', {
      style: 'currency',
      currency: 'ARS',
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    }).format(value);
  };



  LoadPedingRentals(): void{
    this.rentalService.getPendingRentals().subscribe({
      next: (data) => {
        this.filteredPendingRentals = data;
        this.pendingRentals = data;
        console.log(data);
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
        console.log("payment methods: ", data);
      },
      error: (err) => console.error('error al cargar los metodos de pago',err)
    });
  }

  getNamePaymentMethodById(id: number | string | null | undefined): string {
    if (id === null || id === undefined) return 'Desconocido';

    const numericId = Number(id);
    if (Number.isNaN(numericId)) return 'Desconocido';

    const method = this.paymentMethods.find(m => m.id === numericId);
    return method ? method.name : 'Desconocido';
  }

  openPaymentModalWith(item: PendingRentalDTO) {

    const now = new Date();

    this.isAdvancePayment = false;
    this.advanceMonths = null;

    this.selectedPreferredPaymentId = Number(item.preferredPayment ?? 1);

    this.paymentDto = {
      clientId: item.clientId ?? 0,
      movementType: 'CREDITO',
      concept: ` `,
      amount: 0,
      paymentMethodId: 1,
      date: new Date(),
      isAdvancePayment: false,
      advanceMonths: null
    };

    this.updateConceptFromDate(now);

    this.dateString = now.toISOString().split('T')[0];

    this.selectedClientName = item.clientName ?? '';
    this.selectedPaymentIdentifier = item.paymentIdentifier ?? '';
    this.selectedBalance = item.balance ?? '';
    this.selectedCurrentRent = item.currentRent ?? '';
    this.showPaymentModal = true;
  }

  closePaymentModal() { 
    this.showPaymentModal = false;
    this.manualDateEnabled = false;
    this.isAdvancePayment = false;
    this.advanceMonths = null;
    this.paymentDto.isAdvancePayment = false;
    this.paymentDto.advanceMonths = null;
  }

  private getCommissionByMethodId(paymentMethodId: number): number {
    const id = Number(paymentMethodId);
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

  private calcCommissions(selectedMethodId: number, preferredPaymentId: number) {
    const selectedCommission = this.getCommissionByMethodId(selectedMethodId);
    const includedCommission = this.getCommissionByMethodId(preferredPaymentId);
    const extraCommission = Math.max(0, selectedCommission - includedCommission);

    return { selectedCommission, includedCommission, extraCommission };
  }

  blurInput(event: Event): void {
    (event.target as HTMLElement).blur();
  }

  getCalculatedAmounts(amountEntered: number, selectedMethodId: number, preferredPaymentId: number) {
    if (!amountEntered || amountEntered <= 0) {
        return { amountEntered: 0, equivalentDebtPaid: 0, difference: 0, isSurcharge: false, isDiscount: false, selectedCommission: 0, includedCommission: 0 };
    }

    const selectedCommission = this.getCommissionByMethodId(selectedMethodId);
    const includedCommission = this.getCommissionByMethodId(preferredPaymentId);
    
    if (selectedCommission === includedCommission) {
        return { amountEntered, equivalentDebtPaid: amountEntered, difference: 0, isSurcharge: false, isDiscount: false, selectedCommission, includedCommission };
    }

    const rawBaseAmount = amountEntered / (1 + (selectedCommission / 100));
    const equivalentDebtPaid = rawBaseAmount * (1 + (includedCommission / 100));
    const difference = amountEntered - equivalentDebtPaid;

    return {
        amountEntered: amountEntered,
        equivalentDebtPaid: equivalentDebtPaid,
        difference: Math.abs(difference),
        isSurcharge: difference > 0,
        isDiscount: difference < 0,
        selectedCommission,
        includedCommission
    };
  }

  onAmountChange(newAmount: number) {
      if (newAmount && this.paymentDto.paymentMethodId) {
          const calc = this.getCalculatedAmounts(newAmount, this.paymentDto.paymentMethodId, this.selectedPreferredPaymentId);
          this.commision = calc.difference; 
          this.newAmount = calc.equivalentDebtPaid; 
      } else {
          this.commision = 0;
          this.newAmount = 0;
      }
  }

  // 3. GUARDADO Y MODAL DE CONFIRMACIÓN
  savePayment(dto: CreatePaymentDTO): void {
    if (!dto.amount || dto.amount <= 0) {
      Swal.fire({ icon: 'warning', title: 'Monto inválido', text: 'Debes ingresar un monto válido antes de guardar el pago.', confirmButtonText: 'Entendido', confirmButtonColor: '#2563eb' });
      return;
    }
    if (!dto.paymentMethodId) {
       Swal.fire({ icon: 'warning', title: 'Método de pago requerido', text: 'Debes seleccionar un método de pago antes de continuar.', confirmButtonText: 'Entendido', confirmButtonColor: '#2563eb' });
      return;
    }
    if (dto.isAdvancePayment) {
      if (dto.advanceMonths === null || dto.advanceMonths === undefined || isNaN(Number(dto.advanceMonths)) || dto.advanceMonths < 1) {
        Swal.fire({ icon: 'warning', title: 'Pago adelantado incompleto', text: 'Debes ingresar la cantidad de meses adelantados.', confirmButtonText: 'Entendido', confirmButtonColor: '#2563eb' });
        return;
      }
    }

    const amountPhysicalEntered = dto.amount;
    const calc = this.getCalculatedAmounts(amountPhysicalEntered, dto.paymentMethodId, this.selectedPreferredPaymentId);

    const payloadToSave: CreatePaymentDTO = {
      ...dto,
      amount: calc.amountEntered,
      commissionAmount: calc.isSurcharge ? calc.difference : (calc.isDiscount ? -calc.difference : 0),
      commissionConcept: calc.isSurcharge 
          ? `Recargo por pago en ${this.getNamePaymentMethodById(dto.paymentMethodId)} (${calc.selectedCommission}%)`
          : (calc.isDiscount ? `Bonificación por pago en ${this.getNamePaymentMethodById(dto.paymentMethodId)} (${calc.selectedCommission}%)` : '')
    };

    let commissionHtml = '';
    if (calc.isSurcharge) {
        commissionHtml = `
            <div class="flex justify-between text-sm text-orange-600 mt-1 pt-2 border-t border-gray-200">
              <span>Porción retenida por comisión</span>
              <span class="font-bold">- ${this.formatARS(calc.difference)}</span>
            </div>
            <div class="mt-2 text-[11px] text-gray-500 leading-tight">Al pagar con un método más caro (${calc.selectedCommission}%), una parte del ingreso no cancela deuda.</div>`;
    } else if (calc.isDiscount) {
        commissionHtml = `
            <div class="flex justify-between text-sm text-green-600 mt-1 pt-2 border-t border-gray-200">
              <span>Bonificación a favor aplicada</span>
              <span class="font-bold">+ ${this.formatARS(calc.difference)}</span>
            </div>
            <div class="mt-2 text-[11px] text-gray-500 leading-tight">Al pagar con un método más barato (${calc.selectedCommission}%), el ingreso "rinde más" en su cuenta.</div>`;
    } else {
        commissionHtml = `<div class="mt-2 text-xs text-gray-500">Mismo método de pago habitual. Sin diferencias.</div>`;
    }

    Swal.fire({
    title: 'Resumen de Transacción',
      html: `
        <div class="text-left space-y-3">
          <div class="pb-3 border-b border-gray-200">
            <div class="text-sm text-gray-500">Método de pago utilizado</div>
            <div class="text-base font-semibold text-gray-800">${this.getNamePaymentMethodById(dto.paymentMethodId)}</div>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <div class="p-3 rounded-lg bg-gray-50 border border-gray-200">
              <div class="text-sm text-gray-500">Dinero ingresado</div>
              <div class="text-lg font-semibold text-gray-900">${this.formatARS(calc.amountEntered)}</div>
            </div>
            <div class="p-3 rounded-lg ${calc.isDiscount ? 'bg-green-50 border-green-200' : (calc.isSurcharge ? 'bg-orange-50 border-orange-200' : 'bg-blue-50 border-blue-200')}">
              <div class="text-sm ${calc.isDiscount ? 'text-green-800' : (calc.isSurcharge ? 'text-orange-800' : 'text-blue-800')}">Deuda cancelada</div>
              <div class="text-lg font-bold ${calc.isDiscount ? 'text-green-900' : (calc.isSurcharge ? 'text-orange-900' : 'text-blue-900')}">${this.formatARS(calc.equivalentDebtPaid)}</div>
            </div>
          </div>
          <div class="p-4 rounded-lg border border-gray-200 bg-white">
            <div class="text-base font-semibold text-gray-800 mb-2">Desglose de cuenta corriente</div>
            <div class="flex justify-between text-sm text-gray-700">
              <span>Acreditación base</span>
              <span class="font-semibold">${this.formatARS(calc.amountEntered)}</span>
            </div>
            ${commissionHtml}
          </div>
        </div>
      `,
      icon: 'info',
      showCancelButton: true,
      confirmButtonText: 'Registrar pago',
      cancelButtonText: 'Cancelar',
      buttonsStyling: false,
      customClass: {
        confirmButton: 'bg-blue-600 text-white px-4 py-2 rounded-md hover:bg-blue-700 transition-all duration-150',
        cancelButton: 'bg-gray-200 text-gray-800 px-4 py-2 rounded-md hover:bg-gray-300 transition-all duration-150',
        actions: 'flex justify-end gap-3 mt-4',
        popup: 'rounded-xl shadow-lg p-4',
        icon: 'scale-60 mt-1',
      }
    }).then((result) => {
      if (result.isConfirmed) {
        this.paymentService.CreatePayment(payloadToSave).subscribe({
          next: () => {
            Swal.fire({ title: 'Pago registrado', text: 'El pago y el ajuste fueron registrados correctamente.', icon: 'success', confirmButtonColor: '#2563eb' });
            this.closePaymentModal();
            setTimeout(() => this.LoadPayments(), 100); 
            setTimeout(() => this.LoadPedingRentals(), 100);
          },
          error: (err) => {
            console.error('Error al guardar payment:', err);
            Swal.fire({ title: 'Error', text: 'Hubo un problema al registrar el pago.', icon: 'error', confirmButtonColor: '#2563eb' });
          }
        });  
      } else {
         dto.amount = amountPhysicalEntered; 
      }
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
    const dateWithTime = new Date(year, month - 1, day, currentTime.getHours(), currentTime.getMinutes(), currentTime.getSeconds());

    this.paymentDto.date = dateWithTime;
    this.dateString = value;
      if (this.paymentDto.isAdvancePayment) {
        this.updateAdvanceConcept();
      } else {
        this.updateConceptFromDate(dateWithTime);
      }
  }

  openReceiptModal(item: Payment) {
    this.receiptPaymentInfo = item;
    
    // Formateamos la fecha del pago a YYYY-MM-DD para que el <input type="date"> la entienda
    const dt = new Date(item.paymentDate);
    const y = dt.getFullYear();
    const m = (dt.getMonth() + 1).toString().padStart(2, '0');
    const d = dt.getDate().toString().padStart(2, '0');
    this.receiptDateStr = `${y}-${m}-${d}`;

    this.receiptConcepts = [
      { description: 'SERVICIO DE BAULERAS', amount: item.amount }
    ];
    this.showReceiptModal = true;
  }

  closeReceiptModal() {
    this.showReceiptModal = false;
    this.receiptPaymentInfo = null;
    this.receiptConcepts = [];
    this.receiptDateStr = ''; // Limpiamos
  }

  confirmGenerateReceipt() {
    if (!this.receiptPaymentInfo) return;

    if (this.receiptConcepts.some(c => !c.description.trim())) {
      Swal.fire('Atención', 'Todas las descripciones deben estar completas.', 'warning');
      return;
    }

    // Convertimos la fecha elegida en el input (YYYY-MM-DD) al formato del PDF (DD/MM/YYYY)
    const [year, month, day] = this.receiptDateStr.split('-');
    const finalReceiptDate = `${day}/${month}/${year}`;

    this.pdfGeneratorService.generateBauleraReceipt({
      date: finalReceiptDate, // USAMOS LA FECHA ELEGIDA EN EL INPUT
      clientNumber: Number(this.receiptPaymentInfo.paymentIdentifier) || 0,
      clientName: this.receiptPaymentInfo.clientName ?? "",
      concepts: this.receiptConcepts,
      totalAmount: this.receiptTotalAmount
    });

    this.closeReceiptModal();
  }

  addReceiptConcept() {
    this.receiptConcepts.push({ description: '', amount: 0 });
  }

  removeReceiptConcept(index: number) {
    if (this.receiptConcepts.length > 1) {
      this.receiptConcepts.splice(index, 1);
    }
  }

  get receiptTotalAmount(): number {
    return this.receiptConcepts.reduce((acc, curr) => acc + (curr.amount || 0), 0);
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
