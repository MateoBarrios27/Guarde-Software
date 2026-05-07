import { Component, OnInit } from '@angular/core';
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
export class DashboardComponent implements OnInit {

  //Pagination
  page: number = 1;
  itemsPerPage: number = 10;

  page2: number = 1;
  itemsPerPage2: number = 10;

  //load tables
  pendingRentals: PendingRentalDTO[] = [];
  payments: Payment[] = [];
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
    advanceMonths: 0
  };

  manualDateEnabled = false;
  dateString: string = '';
  selectedPreferredPaymentId: number = 0;
  commision:number = 0;
  newAmount: number = 0;

  // --- VARIABLES PARA EL MODAL DE AUMENTO ---
  selectedIncreaseAnchorDate: string | null = null;
  hasIncreaseInPeriod: boolean = false;
  isIncreaseNextMonth: boolean = false;
  showIncreaseOverlay: boolean = false;
  increaseResolved: boolean = false;
  projectedNewRent: number = 0;
  projectedNextIncreaseDate: Date | null = null;
  increasePromptReason: string = '';
  increasePercentage: number = 0;
  currentIncreaseFlow: 'advance' | 'normal' | 'none' = 'none';

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

  formatARS = (value: number) => {
    return new Intl.NumberFormat('es-AR', {
      style: 'currency', currency: 'ARS', minimumFractionDigits: 2, maximumFractionDigits: 2
    }).format(value);
  };

  LoadPedingRentals(): void{
    this.rentalService.getPendingRentals().subscribe({
      next: (data) => {
        this.filteredPendingRentals = data;
        this.pendingRentals = data;
        console.log('Pending rentals cargados:', data);
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

  getNamePaymentMethodById(id: number | string | null | undefined): string {
    if (id === null || id === undefined) return 'Desconocido';
    const numericId = Number(id);
    if (Number.isNaN(numericId)) return 'Desconocido';
    const method = this.paymentMethods.find(m => m.id === numericId);
    return method ? method.name : 'Desconocido';
  }

  openPaymentModalWith(item: any) { // Idealmente es PendingRentalDTO, asegurate que traiga increaseAnchorDate
    const now = new Date();

    this.selectedPreferredPaymentId = Number(item.preferredPayment ?? item.preferredPaymentMethodId ?? 1); // Cuidado: En el HTML decía preferredPayment, lo ajusto al nombre estándar
    this.selectedClientName = item.clientName ?? '';
    this.selectedPaymentIdentifier = Number(item.paymentIdentifier ?? 0);
    this.selectedBalance = Number(item.balance ?? 0);
    this.selectedCurrentRent = Number(item.currentRent ?? 0);
    this.selectedIncreaseAnchorDate = item.increaseAnchorDate ?? null;

    this.increaseResolved = false;
    this.increasePercentage = 0;
    this.showIncreaseOverlay = false;
    this.currentIncreaseFlow = 'none';

    // Sugerencia inteligente de monto
    const suggestedAmount = this.selectedBalance < 0 
        ? Math.abs(this.selectedBalance) 
        : this.selectedCurrentRent;

    this.paymentDto = {
      clientId: item.clientId ?? 0,
      movementType: 'CREDITO',
      concept: ` `,
      amount: suggestedAmount, 
      paymentMethodId: this.selectedPreferredPaymentId,
      date: new Date(),
      isAdvancePayment: false,
      advanceMonths: 0
    };

    this.updateConceptFromDate(now);
    this.dateString = now.toISOString().split('T')[0];
    this.showPaymentModal = true;
    this.manualDateEnabled = false;

    this.checkIncreaseLogic();
    this.onAmountChange(this.paymentDto.amount);
  }

  closePaymentModal() { 
    this.showPaymentModal = false;
    this.manualDateEnabled = false;
    this.showIncreaseOverlay = false;
    this.currentIncreaseFlow = 'none';
    this.paymentDto.isAdvancePayment = false;
    this.paymentDto.advanceMonths = 0;
  }

  private getCommissionByMethodId(paymentMethodId: number): number {
    const id = Number(paymentMethodId);
    const method = this.paymentMethods.find(m => m.id === id);
    return method?.commission ?? 0; 
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

  // =========================================================
  // --- LÓGICA DE AUMENTOS Y PAGOS ADELANTADOS ---
  // =========================================================

  private roundToNearest1000(amount: number): number {
    if (amount === 0) return 0;
    return Math.round(amount / 1000) * 1000;
  }

  private updateProjectedDate() {
    if (this.selectedIncreaseAnchorDate) {
      let currentAnchor = new Date(this.selectedIncreaseAnchorDate);
      this.projectedNextIncreaseDate = new Date(currentAnchor.getFullYear(), currentAnchor.getMonth() + 3, 1);
    }
  }

  onIncreasePercentageBlur() {
    const rent = this.selectedCurrentRent || 0;
    const perc = this.increasePercentage || 0;
    let newRent = rent + (rent * (perc / 100));

    const methodName = this.getNamePaymentMethodById(this.selectedPreferredPaymentId).toLowerCase();
    if (methodName.includes('efectivo')) {
      newRent = this.roundToNearest1000(newRent);
      if (rent > 0) {
        this.increasePercentage = parseFloat((((newRent - rent) / rent) * 100).toFixed(2));
      }
    }

    this.projectedNewRent = newRent;
    this.updateProjectedDate();
  }

  onProjectedRentBlur() {
    const rent = this.selectedCurrentRent || 0;
    let targetRent = this.projectedNewRent || 0;

    const methodName = this.getNamePaymentMethodById(this.selectedPreferredPaymentId).toLowerCase();
    if (methodName.includes('efectivo')) {
      targetRent = this.roundToNearest1000(targetRent);
      this.projectedNewRent = targetRent; 
    }
    
    if (rent === 0) {
      this.increasePercentage = 0;
    } else {
      const perc = ((targetRent - rent) / rent) * 100;
      this.increasePercentage = parseFloat(perc.toFixed(2));
    }
    
    this.updateProjectedDate();
  }

  checkIncreaseLogic() {
    this.hasIncreaseInPeriod = false;
    this.isIncreaseNextMonth = false;

    if (!this.selectedIncreaseAnchorDate) return;

    let baseDate = this.manualDateEnabled && this.dateString ? new Date(this.dateString) : new Date();
    let anchorDate = new Date(this.selectedIncreaseAnchorDate);
    let nextMonth = new Date(baseDate.getFullYear(), baseDate.getMonth() + 1, 1);

    let monthsToPay = (this.paymentDto.isAdvancePayment && this.paymentDto.advanceMonths) ? this.paymentDto.advanceMonths : 1;

    for (let i = 0; i < monthsToPay; i++) {
      let mDate = new Date(baseDate.getFullYear(), baseDate.getMonth() + i, 1);
      if (mDate.getFullYear() === anchorDate.getFullYear() && mDate.getMonth() === anchorDate.getMonth()) {
        this.hasIncreaseInPeriod = true;
        break;
      }
    }

    if (!this.hasIncreaseInPeriod) {
      let monthAfterPayment = new Date(baseDate.getFullYear(), baseDate.getMonth() + monthsToPay, 1);
      if (anchorDate.getFullYear() === monthAfterPayment.getFullYear() && anchorDate.getMonth() === monthAfterPayment.getMonth()) {
        this.isIncreaseNextMonth = true;
      }
    }
  }

  calculateAdvancePayment(): void {
    const suggestedAmount = this.selectedBalance < 0 
      ? Math.abs(this.selectedBalance) 
      : this.selectedCurrentRent;

    if (!this.paymentDto.isAdvancePayment || !this.paymentDto.advanceMonths) {
      this.paymentDto.amount = suggestedAmount;
      this.onAmountChange(this.paymentDto.amount);
      return;
    }

    let currentRent = this.selectedCurrentRent;
    let totalToPay = 0;
    let baseDate = this.manualDateEnabled && this.dateString ? new Date(this.dateString) : new Date();
    let anchorDate = this.selectedIncreaseAnchorDate ? new Date(this.selectedIncreaseAnchorDate) : null;

    const currentMethodName = this.getNamePaymentMethodById(this.paymentDto.paymentMethodId).toLowerCase();
    const isEfectivo = currentMethodName.includes('efectivo');

    for (let i = 0; i < this.paymentDto.advanceMonths; i++) {
      if (i === 0) {
        totalToPay += suggestedAmount;
      } 
      else {
        let currentMonthDate = new Date(baseDate.getFullYear(), baseDate.getMonth() + i, 1);
        let rentForThisMonth = currentRent;

        if (anchorDate && currentMonthDate >= new Date(anchorDate.getFullYear(), anchorDate.getMonth(), 1)) {
          if (this.paymentDto.increasePercentage && this.paymentDto.increasePercentage > 0) {
            rentForThisMonth = currentRent + (currentRent * (this.paymentDto.increasePercentage / 100));
            if (isEfectivo) {
              rentForThisMonth = this.roundToNearest1000(rentForThisMonth);
            }
          }
        }
        totalToPay += rentForThisMonth;
      }
    }

    this.paymentDto.amount = totalToPay;
    this.onAmountChange(totalToPay);
  }

  onAdvancePaymentToggle() {
    if (!this.paymentDto.isAdvancePayment) {
      this.paymentDto.advanceMonths = undefined;
      let targetDate = this.manualDateEnabled && this.dateString ? new Date(this.dateString) : new Date();
      this.updateConceptFromDate(targetDate);
    } else {
      this.paymentDto.advanceMonths = 1;
      this.updateAdvanceConcept();
    }
    this.checkIncreaseLogic();
    this.calculateAdvancePayment();
  }

  onAdvanceMonthsChange() {
    this.updateAdvanceConcept();
    this.checkIncreaseLogic();
    this.calculateAdvancePayment();
  }

  // =========================================================
  // --- GUARDADO Y FLUJO DEL MODAL ---
  // =========================================================

  savePayment(dto: CreatePaymentDTO): void {
    if (!dto.amount || dto.amount <= 0) { Swal.fire({ icon: 'warning', title: 'Monto inválido', text: 'Debes ingresar un monto válido.'}); return; }
    if (!dto.paymentMethodId) { Swal.fire({ icon: 'warning', title: 'Método requerido', text: 'Seleccioná un método de pago.'}); return; }
    if (dto.isAdvancePayment && (!dto.advanceMonths || dto.advanceMonths < 1)) { Swal.fire({ icon: 'warning', title: 'Meses inválidos', text: 'Mínimo 1 mes.'}); return; }

    if (this.manualDateEnabled && this.dateString) {
      const [year, month, day] = this.dateString.split('-').map(Number);
      const currentTime = new Date();
      this.paymentDto.date = new Date(year, month - 1, day, currentTime.getHours(), currentTime.getMinutes(), currentTime.getSeconds());
    }

    this.checkIncreaseLogic();

    const isPriceLocked = this.paymentDto.isAdvancePayment && this.paymentDto.advanceMonths && this.paymentDto.advanceMonths >= 6;
    const affectsCurrentTotal = this.hasIncreaseInPeriod;

    if (affectsCurrentTotal && !isPriceLocked && !this.increaseResolved) {
      // FLUJO A
      this.currentIncreaseFlow = 'advance';
      this.increasePromptReason = 'Dentro de los meses que está pagando, el cliente tiene programado un aumento.';
      this.calculateProjectedRent();
      this.showIncreaseOverlay = true;
    } else {
      // FLUJO B
      this.currentIncreaseFlow = (this.isIncreaseNextMonth && !isPriceLocked) ? 'normal' : 'none';
      this.showSummarySwal();
    }
  }

  confirmIncrease() {
    this.paymentDto.increasePercentage = this.increasePercentage;
    this.increaseResolved = true;
    this.showIncreaseOverlay = false;
    
    if (this.currentIncreaseFlow === 'advance') {
      this.calculateAdvancePayment(); 
      this.showSummarySwal(); 
    } else if (this.currentIncreaseFlow === 'normal') {
      this.executeBackendCall(); 
    }
  }

  skipIncrease() {
    this.paymentDto.increasePercentage = 0;
    this.increasePercentage = 0;
    this.increaseResolved = true;
    this.showIncreaseOverlay = false;
    
    if (this.currentIncreaseFlow === 'advance') {
      this.calculateAdvancePayment();
      this.showSummarySwal();
    } else if (this.currentIncreaseFlow === 'normal') {
      this.executeBackendCall();
    }
  }

  showSummarySwal() {
    const calc = this.getCalculatedAmounts(this.paymentDto.amount, this.paymentDto.paymentMethodId, this.selectedPreferredPaymentId);

    let commissionHtml = '';
    if (calc.isSurcharge) {
        commissionHtml = `<div class="flex justify-between text-sm text-orange-600 mt-1 pt-2 border-t border-gray-200"><span>Porción retenida por comisión</span><span class="font-bold">- ${this.formatARS(calc.difference)}</span></div>`;
    } else if (calc.isDiscount) {
        commissionHtml = `<div class="flex justify-between text-sm text-green-600 mt-1 pt-2 border-t border-gray-200"><span>Bonificación a favor aplicada</span><span class="font-bold">+ ${this.formatARS(calc.difference)}</span></div>`;
    } else {
        commissionHtml = `<div class="mt-2 text-xs text-gray-500">Mismo método de pago habitual. Sin diferencias.</div>`;
    }

    Swal.fire({
      title: 'Resumen de Transacción',
      html: `
        <div class="text-left space-y-3">
          <div class="pb-3 border-b border-gray-200">
            <div class="text-sm text-gray-500">Método de pago utilizado</div>
            <div class="text-base font-semibold text-gray-800">${this.getNamePaymentMethodById(this.paymentDto.paymentMethodId)}</div>
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
              <span>Acreditación base</span><span class="font-semibold">${this.formatARS(calc.amountEntered)}</span>
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
      customClass: { confirmButton: 'bg-blue-600 text-white px-4 py-2 rounded-md hover:bg-blue-700 transition-all duration-150 mx-2', cancelButton: 'bg-gray-200 text-gray-800 px-4 py-2 rounded-md', actions: 'flex justify-end mt-4', popup: 'rounded-xl shadow-lg p-4' }
    }).then((result) => {
      if (result.isConfirmed) {
        if (this.currentIncreaseFlow === 'normal' && !this.increaseResolved) {
          this.increasePromptReason = 'El próximo mes le corresponde una actualización de abono.';
          this.calculateProjectedRent();
          this.showIncreaseOverlay = true;
        } else {
          this.executeBackendCall();
        }
      }
    });
  }

  calculateProjectedRent() {
    const rent = this.selectedCurrentRent || 0;
    const perc = this.increasePercentage || 0;
    let newRent = rent + (rent * (perc / 100));

    const methodName = this.getNamePaymentMethodById(this.selectedPreferredPaymentId).toLowerCase();
    if (methodName.includes('efectivo')) {
      // 1. Redondeamos el monto proyectado
      newRent = this.roundToNearest1000(newRent);
      
      // 2. RE-CALCULAMOS el porcentaje basado en el monto ya redondeado
      // Esto es vital para que el backend reciba el % exacto que da el número redondo
      if (rent > 0) {
        const exactPerc = ((newRent - rent) / rent) * 100;
        this.increasePercentage = parseFloat(exactPerc.toFixed(4)); // Usamos más decimales para precisión
      }
    }

    this.projectedNewRent = newRent;
    this.updateProjectedDate();
  }

  executeBackendCall() {
    const calc = this.getCalculatedAmounts(this.paymentDto.amount, this.paymentDto.paymentMethodId, this.selectedPreferredPaymentId);
    
    const payloadToSave: CreatePaymentDTO = {
      ...this.paymentDto,
      amount: calc.amountEntered,
      commissionAmount: calc.isSurcharge ? calc.difference : (calc.isDiscount ? -calc.difference : 0),
      commissionConcept: calc.isSurcharge ? `Recargo por pago en ${this.getNamePaymentMethodById(this.paymentDto.paymentMethodId)} (${calc.selectedCommission}%)` : (calc.isDiscount ? `Bonificación por pago en ${this.getNamePaymentMethodById(this.paymentDto.paymentMethodId)} (${calc.selectedCommission}%)` : '')
    };

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
  }

  blurInput(event: Event): void {
    (event.target as HTMLElement).blur();
  }

  filterPendingRentals(): void {
    const term = this.searchPending.toLowerCase().trim();
    this.filteredPendingRentals = this.pendingRentals.filter(item => {
      const clientName = (item.clientName ?? '').toString().toLowerCase();
      const paymentIdentifier = (item.paymentIdentifier ?? '').toString().toLowerCase();
      const lockerIdentifiers = (item.lockerIdentifiers ?? '').toString().toLowerCase();
      return (clientName.includes(term) || paymentIdentifier.includes(term) || lockerIdentifiers.includes(term));
    });
  }

  filterPayments(): void {
    const term = this.searchPayment.toLowerCase().trim();
    this.filteredPayments = this.payments.filter(item => {
      const clientName = (item.clientName ?? '').toString().toLowerCase();
      const paymentIdentifier = (item.paymentIdentifier ?? '').toString().toLowerCase();
      const paymentMethodId = (item.paymentMethodId ?? '').toString().toLowerCase();
      return (clientName.includes(term) || paymentIdentifier.includes(term) || paymentMethodId.includes(term));
    });
    this.filteredPayments.sort((a, b) => new Date(b.paymentDate).getTime() - new Date(a.paymentDate).getTime());
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
    const monthNames = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'];
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

  private updateAdvanceConcept() {
    const months = this.paymentDto.advanceMonths;
    if (months === null || months === undefined || months === 0) {
      this.paymentDto.concept = 'Pago adelantado';
      return;
    }
    this.paymentDto.concept = `Pago adelantado de ${months} mes${months === 1 ? '' : 'es'}`;
  }

  openReceiptModal(item: Payment) {
    this.receiptPaymentInfo = item;
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
    this.receiptDateStr = '';
  }

  confirmGenerateReceipt() {
    if (!this.receiptPaymentInfo) return;
    if (this.receiptConcepts.some(c => !c.description.trim())) {
      Swal.fire('Atención', 'Todas las descripciones deben estar completas.', 'warning');
      return;
    }

    const [year, month, day] = this.receiptDateStr.split('-');
    const finalReceiptDate = `${day}/${month}/${year}`;

    this.pdfGeneratorService.generateBauleraReceipt({
      date: finalReceiptDate, 
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
}