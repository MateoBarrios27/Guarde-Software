import { Component, OnInit, HostListener } from '@angular/core';
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
  selectedPreviousBalance = 0;
  selectedCurrentRent = 0;
  selectedMonthsUnpaid = 0;
  receiptDateStr: string = '';
  
  //receipt modal
  showReceiptModal = false;
  receiptPaymentInfo: Payment | null = null;
  receiptConcepts: { description: string, amount: number }[] = [];
  receiptTotalAmountCustom: number = 0;

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
  isPreviousBalanceSelected: boolean = false;
  public selectedPendingSurcharge: number = 0;
  public selectedInterestAmount: number = 0;
  public selectedSurchargeAction: string = 'next_payment';
  public applyScenarioCInterest: boolean = false;
  public selectedNextPaymentDay: Date | string | null = null;
  public customScenarioCInterest: number | null = null;

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

  formatARSInput(value: number): string {
    return new Intl.NumberFormat('es-AR', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    }).format(value || 0);
  }

  attachCurrencyFormatListener(inputEl: HTMLInputElement, onChangeCallback: (val: number) => void) {
    inputEl.addEventListener('input', (event: any) => {
      const input = event.target as HTMLInputElement;
      let val = input.value;
      let cursorPosition = input.selectionStart || 0;
      const originalLength = val.length;
      const isNegative = val.startsWith('-');

      val = val.replace(/[^0-9,]/g, '');
      let parts = val.split(',');
      if (parts.length > 2) {
        parts.pop(); 
        val = parts.join(',');
      }
      if (parts.length === 2 && parts[1].length > 2) {
        parts[1] = parts[1].substring(0, 2);
      }
      let integerPart = parts[0];
      if (integerPart) {
        integerPart = integerPart.replace(/\B(?=(\d{3})+(?!\d))/g, ".");
      }
      let formattedStr = integerPart;
      if (parts.length > 1) {
        formattedStr += ',' + parts[1];
      } else if (val.endsWith(',')) {
        formattedStr += ',';
      }
      if (isNegative) {
        formattedStr = formattedStr !== '' ? '-' + formattedStr : '-';
      }
      input.value = formattedStr;
      const newLength = formattedStr.length;
      cursorPosition = cursorPosition + (newLength - originalLength);
      if (cursorPosition < 0) cursorPosition = 0;
      setTimeout(() => input.setSelectionRange(cursorPosition, cursorPosition), 0);

      const cleanValue = formattedStr.replace(/\./g, '').replace(',', '.');
      const numberValue = parseFloat(cleanValue);
      onChangeCallback(isNaN(numberValue) ? 0 : numberValue);
    });

    inputEl.addEventListener('blur', () => {
      const val = inputEl.value;
      if (val === '-' || !val) {
        onChangeCallback(0);
        inputEl.value = '';
        return;
      }
      const cleanValue = val.replace(/\./g, '').replace(',', '.');
      const numberValue = parseFloat(cleanValue);
      if (!isNaN(numberValue)) {
        onChangeCallback(numberValue);
        inputEl.value = this.formatARSInput(numberValue);
      } else {
        onChangeCallback(0);
        inputEl.value = '';
      }
    });
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

  getPaymentDay(): number {
    if (!this.paymentDto || !this.paymentDto.date) return new Date().getDate();
    return new Date(this.paymentDto.date).getDate();
  }

  isNextPaymentInNextMonthOrLater(): boolean {
    const nextDate = this.selectedNextPaymentDay;
    if (!nextDate) return false;
    const d = new Date(nextDate);
    const payDate = new Date(this.paymentDto?.date || new Date());
    const nextMonthIndex = d.getFullYear() * 12 + d.getMonth();
    const payMonthIndex = payDate.getFullYear() * 12 + payDate.getMonth();
    return nextMonthIndex > payMonthIndex;
  }

  isPotentiallyDuplicate(): boolean {
    if (this.paymentDto?.isAdvancePayment || this.isPreviousBalanceSelected) return false;
    return this.isNextPaymentInNextMonthOrLater() || this.selectedBalance >= 0;
  }

  getSurchargeScenario(): 'A' | 'B' | 'C' | 'D' {
    if (this.isPreviousBalanceSelected) return 'D';
    const day = this.getPaymentDay();
    const hasSurcharge = (this.selectedPendingSurcharge > 0);
    if (hasSurcharge && day <= 10) return 'A';
    if (hasSurcharge && day > 10) return 'B';
    if (!hasSurcharge && day > 10 && this.selectedCurrentRent > 0 && !this.isNextPaymentInNextMonthOrLater()) return 'C';
    if (this.paymentDto?.isAdvancePayment) return 'D';
    return 'D';
  }

  calculateInterestAmount(): number {
    const scenario = this.getSurchargeScenario();
    if (scenario === 'A' || scenario === 'B') {
      return this.selectedPendingSurcharge;
    }
    if (scenario === 'C') {
      if (this.customScenarioCInterest !== null) {
        return this.customScenarioCInterest;
      }
      const baseImponible = (this.selectedInterestAmount || 0) + (this.selectedCurrentRent || 0);
      const rawPenalty = baseImponible * 0.10;
      return Math.floor(rawPenalty / 100) * 100;
    }
    return 0;
  }

  modifyInterestAmount(fromSummary: boolean = false): void {
    const scenario = this.getSurchargeScenario();
    const currentAmt = (scenario === 'C') ? this.calculateInterestAmount() : (this.selectedPendingSurcharge || 0);

    Swal.fire({
      title: 'Modificar Monto de Recargo / Interés',
      input: 'text',
      inputValue: this.formatARSInput(currentAmt),
      text: 'Ingresá el nuevo monto que deseas aplicar o registrar por mora:',
      showCancelButton: true,
      confirmButtonText: 'Guardar',
      cancelButtonText: 'Cancelar',
      confirmButtonColor: '#2563eb',
      didOpen: () => {
        const inputEl = Swal.getInput();
        if (inputEl) {
          this.attachCurrencyFormatListener(inputEl, () => {});
        }
      },
      preConfirm: (value) => {
        if (!value) return 0;
        const cleanVal = value.replace(/\./g, '').replace(',', '.');
        const parsed = parseFloat(cleanVal);
        return isNaN(parsed) ? 0 : parsed;
      }
    }).then((result) => {
      if (result.isConfirmed && result.value !== undefined) {
        if (scenario === 'C') {
          this.customScenarioCInterest = result.value;
        } else {
          this.selectedPendingSurcharge = result.value;
        }
        if (fromSummary) {
          setTimeout(() => this.showSummarySwal(), 0);
        }
      }
    });
  }

  getNamePaymentMethodById(id: number | string | null | undefined): string {
    if (id === null || id === undefined) return 'Desconocido';
    const numericId = Number(id);
    if (Number.isNaN(numericId)) return 'Desconocido';
    const method = this.paymentMethods.find(m => m.id === numericId);
    return method ? method.name : 'Desconocido';
  }

  openPaymentModalWith(item: any) { 
    const now = new Date();

    this.selectedPreferredPaymentId = Number(item.preferredPayment ?? item.preferredPaymentMethodId ?? 1); 
    this.selectedClientName = item.clientName ?? '';
    this.selectedPaymentIdentifier = Number(item.paymentIdentifier ?? 0);
    this.selectedBalance = Number(item.balance ?? 0);
    this.selectedPreviousBalance = Number(item.previousBalance ?? 0);
    this.selectedCurrentRent = Number(item.currentRent ?? 0);
    this.selectedMonthsUnpaid = Number(item.monthsUnpaid ?? 0);
    this.selectedIncreaseAnchorDate = item.increaseAnchorDate ?? null;
    this.selectedPendingSurcharge = Number(item.pendingSurcharge ?? 0);
    this.selectedInterestAmount = Number(item.interestAmount ?? 0);
    this.selectedNextPaymentDay = item.nextPaymentDay ?? null;
    this.selectedSurchargeAction = 'next_payment';
    this.applyScenarioCInterest = false;
    this.customScenarioCInterest = null;
    
    this.increaseResolved = false;
    this.increasePercentage = 0;
    this.showIncreaseOverlay = false;
    this.currentIncreaseFlow = 'none';
    this.isPreviousBalanceSelected = false;

    // Sugerencia inteligente de monto
    const suggestedAmount = this.selectedBalance < 0 
        ? Math.abs(this.selectedBalance) 
        : this.selectedCurrentRent;

    if (this.selectedPreviousBalance < 0 && suggestedAmount === Math.abs(Number(this.selectedPreviousBalance)) && suggestedAmount > 0) {
      this.isPreviousBalanceSelected = true;
    }

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

    if (this.isPreviousBalanceSelected) {
      this.paymentDto.concept = 'Pago de saldo anterior';
    } else {
      this.updateConceptFromDate(now);
    }
    this.dateString = now.toISOString().split('T')[0];
    this.showPaymentModal = true;
    this.manualDateEnabled = false;

    this.checkIncreaseLogic();
    this.onAmountChange(this.paymentDto.amount);
  }

  @HostListener('document:keydown.escape', ['$event'])
  onEscapeKey(event: KeyboardEvent): void {
    if (this.showIncreaseOverlay) {
      this.skipIncrease();
      return;
    }
    if (this.showReceiptModal) {
      this.closeReceiptModal();
      return;
    }
    if (this.showPaymentModal) {
      this.closePaymentModal();
      return;
    }
  }

  closePaymentModal() { 
    this.showPaymentModal = false;
    this.selectedPreviousBalance = 0;
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

      if (this.selectedPreviousBalance !== 0 && (newAmount === Math.abs(Number(this.selectedPreviousBalance)) || this.newAmount === Math.abs(Number(this.selectedPreviousBalance))) && newAmount > 0) {
          this.isPreviousBalanceSelected = true;
          this.paymentDto.concept = 'Pago de saldo anterior';
      } else if (this.isPreviousBalanceSelected && newAmount !== Math.abs(Number(this.selectedPreviousBalance)) && this.newAmount !== Math.abs(Number(this.selectedPreviousBalance))) {
          this.isPreviousBalanceSelected = false;
          let targetDate = this.manualDateEnabled && this.dateString ? new Date(this.dateString) : new Date();
          if (this.paymentDto.isAdvancePayment) {
              this.updateAdvanceConcept();
          } else {
              this.updateConceptFromDate(targetDate);
          }
      }
  }

  onDebtCancelChange(debtCancelled: number) {
      if (debtCancelled && debtCancelled > 0 && this.paymentDto.paymentMethodId) {
          const selectedCommission = this.getCommissionByMethodId(this.paymentDto.paymentMethodId);
          const includedCommission = this.getCommissionByMethodId(this.selectedPreferredPaymentId);

          if (selectedCommission === includedCommission) {
              this.paymentDto.amount = debtCancelled;
              this.commision = 0;
              this.newAmount = debtCancelled;
          } else {
              const rawBaseAmount = debtCancelled / (1 + (includedCommission / 100));
              const amountRequired = rawBaseAmount * (1 + (selectedCommission / 100));
              this.paymentDto.amount = Number(amountRequired.toFixed(2));
              this.commision = Number((this.paymentDto.amount - debtCancelled).toFixed(2));
              this.newAmount = debtCancelled;
          }
      } else {
          this.paymentDto.amount = 0;
          this.commision = 0;
          this.newAmount = 0;
      }

      if (this.selectedPreviousBalance < 0 && debtCancelled === Math.abs(Number(this.selectedPreviousBalance)) && debtCancelled > 0) {
          this.isPreviousBalanceSelected = true;
          this.paymentDto.concept = 'Pago de saldo anterior';
      } else if (this.isPreviousBalanceSelected && debtCancelled !== Math.abs(Number(this.selectedPreviousBalance))) {
          this.isPreviousBalanceSelected = false;
          let targetDate = this.manualDateEnabled && this.dateString ? new Date(this.dateString) : new Date();
          if (this.paymentDto.isAdvancePayment) {
              this.updateAdvanceConcept();
          } else {
              this.updateConceptFromDate(targetDate);
          }
      }
  }

  isPaymentMethodDifferent(): boolean {
    return Number(this.paymentDto?.paymentMethodId ?? 0) !== Number(this.selectedPreferredPaymentId ?? 0);
  }

  usePreviousBalance(): void {
    if (this.selectedPreviousBalance < 0) {
      this.isPreviousBalanceSelected = true;
      this.paymentDto.amount = Math.abs(Number(this.selectedPreviousBalance));
      this.paymentDto.concept = 'Pago de saldo anterior';
      this.onAmountChange(this.paymentDto.amount);
    }
  }

  getAbsolutePreviousBalance(): number {
    return Math.abs(Number(this.selectedPreviousBalance ?? 0));
  }

  getTotalDebtAmount(): number {
    return this.selectedBalance < 0 
        ? Math.abs(this.selectedBalance) 
        : this.selectedCurrentRent;
  }

  useTotalDebt(): void {
    const totalDebt = this.getTotalDebtAmount();
    if (totalDebt > 0) {
      this.isPreviousBalanceSelected = false;
      this.paymentDto.amount = totalDebt;
      this.onAmountChange(this.paymentDto.amount);
      const targetDate = this.manualDateEnabled && this.dateString ? new Date(this.dateString) : new Date();
      if (this.paymentDto.isAdvancePayment) {
        this.updateAdvanceConcept();
      } else {
        this.updateConceptFromDate(targetDate);
      }
    }
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
    if (this.isPreviousBalanceSelected) {
      this.paymentDto.concept = 'Pago de saldo anterior';
      return;
    }
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
    this.isPreviousBalanceSelected = false;
    const months = this.paymentDto.advanceMonths;
    if (months === null || months === undefined || months === 0) {
      this.paymentDto.concept = 'Pago adelantado';
      return;
    }
    this.paymentDto.concept = `Pago adelantado de ${months} mes${months === 1 ? '' : 'es'}`;
  }

  // =========================================================
  // --- LÓGICA DE AUMENTOS Y PAGOS ADELANTADOS (ACTUALIZADA) ---
  // =========================================================

  private roundRentAmount(amount: number, methodName: string): number {
    if (amount === 0) return 0;
    if (methodName.includes('efectivo')) {
      return Math.round(amount / 1000) * 1000;
    } else {
      return Math.round(amount / 100) * 100;
    }
  }

  private updateProjectedDate() {
    if (this.selectedIncreaseAnchorDate) {
      let currentAnchor = new Date(this.selectedIncreaseAnchorDate);
      this.projectedNextIncreaseDate = new Date(currentAnchor.getFullYear(), currentAnchor.getMonth() + 3, 1);
    }
  }

  private getSelectedPaymentMonth(): { year: number; month: number } {
    if (this.manualDateEnabled && this.dateString) {
      const [year, month] = this.dateString.split('-').map(Number);
      return { year, month };
    }
    const baseDate = this.paymentDto.date ? new Date(this.paymentDto.date) : new Date();
    return { year: baseDate.getFullYear(), month: baseDate.getMonth() + 1 };
  }

  private getCoverageStartMonth(): { year: number; month: number } {
    const selectedMonth = this.getSelectedPaymentMonth();
    const today = new Date();
    const currentMonth = { year: today.getFullYear(), month: today.getMonth() + 1 };

    if (this.selectedBalance < 0) {
      const selectedValue = selectedMonth.year * 100 + selectedMonth.month;
      const currentValue = currentMonth.year * 100 + currentMonth.month;
      if (selectedValue < currentValue) {
        return currentMonth;
      }
    }
    return selectedMonth;
  }

  private addMonths(year: number, month: number, delta: number): { year: number; month: number } {
    let resultYear = year;
    let resultMonth = month + delta;
    while (resultMonth > 12) {
      resultMonth -= 12;
      resultYear += 1;
    }
    while (resultMonth <= 0) {
      resultMonth += 12;
      resultYear -= 1;
    }
    return { year: resultYear, month: resultMonth };
  }

  calculateProjectedRent() {
    const rent = this.selectedCurrentRent || 0;
    const perc = this.increasePercentage || 0;
    let newRent = rent + (rent * (perc / 100));

    const methodName = this.getNamePaymentMethodById(this.selectedPreferredPaymentId).toLowerCase();
    newRent = this.roundRentAmount(newRent, methodName);

    if (rent > 0) {
      const exactPerc = ((newRent - rent) / rent) * 100;
      this.increasePercentage = parseFloat(exactPerc.toFixed(4)); 
    }

    this.projectedNewRent = newRent;
    this.updateProjectedDate();
  }

  calculatePercentageFromRent() {
    const rent = this.selectedCurrentRent || 0;
    
    if (rent === 0) {
      this.increasePercentage = 0;
    } else {
      let newRent = this.projectedNewRent || 0;
      let perc = ((newRent - rent) / rent) * 100;
      this.increasePercentage = parseFloat(perc.toFixed(4));
    }
    
    this.updateProjectedDate();
  }

  onIncreasePercentageBlur() {
    const rent = this.selectedCurrentRent || 0;
    const perc = this.increasePercentage || 0;
    let newRent = rent + (rent * (perc / 100));

    const methodName = this.getNamePaymentMethodById(this.selectedPreferredPaymentId).toLowerCase();
    newRent = this.roundRentAmount(newRent, methodName);
      
    if (rent > 0) {
      this.increasePercentage = parseFloat((((newRent - rent) / rent) * 100).toFixed(4));
    }

    this.projectedNewRent = newRent;
    this.updateProjectedDate();
  }

  onProjectedRentBlur() {
    const rent = this.selectedCurrentRent || 0;
    let targetRent = this.projectedNewRent || 0;

    const methodName = this.getNamePaymentMethodById(this.selectedPreferredPaymentId).toLowerCase();
    targetRent = this.roundRentAmount(targetRent, methodName);
    this.projectedNewRent = targetRent; 
    
    if (rent === 0) {
      this.increasePercentage = 0;
    } else {
      const perc = ((targetRent - rent) / rent) * 100;
      this.increasePercentage = parseFloat(perc.toFixed(4));
    }
    
    this.updateProjectedDate();
  }

  checkIncreaseLogic() {
    this.hasIncreaseInPeriod = false;
    this.isIncreaseNextMonth = false;

    if (this.isPreviousBalanceSelected) return;

    if (!this.selectedIncreaseAnchorDate) return;

    const anchorString = this.selectedIncreaseAnchorDate.split('T')[0];
    const [aYear, aMonth] = anchorString.split('-').map(Number);
    const anchorValue = aYear * 100 + aMonth;

    const coverageStart = this.getCoverageStartMonth();
    const currentMonthValue = coverageStart.year * 100 + coverageStart.month;

    const currentDebt = this.selectedBalance < 0 ? Math.abs(this.selectedBalance) : 0;
    let moneyInHand = this.paymentDto.amount || 0;
    
    let farthestYear = coverageStart.year;
    let farthestMonth = coverageStart.month;

    if (moneyInHand > currentDebt) {
      let extraMoney = moneyInHand - currentDebt;
      let rent = this.selectedCurrentRent || 1; 
      
      let futureMonthsCovered = Math.ceil(extraMoney / rent);
      
      if (this.paymentDto.isAdvancePayment && this.paymentDto.advanceMonths) {
          futureMonthsCovered = Math.max(futureMonthsCovered, this.paymentDto.advanceMonths);
      }

      const farthest = this.addMonths(coverageStart.year, coverageStart.month, futureMonthsCovered - 1);
      farthestYear = farthest.year;
      farthestMonth = farthest.month;
    } else if (this.paymentDto.isAdvancePayment && this.paymentDto.advanceMonths) {
       const farthest = this.addMonths(coverageStart.year, coverageStart.month, this.paymentDto.advanceMonths - 1);
       farthestYear = farthest.year;
       farthestMonth = farthest.month;
    }

    const farthestMonthValue = farthestYear * 100 + farthestMonth;
    const nextMonth = this.addMonths(farthestYear, farthestMonth, 1);
    const nextMonthValue = nextMonth.year * 100 + nextMonth.month;

    if (anchorValue >= currentMonthValue && anchorValue <= farthestMonthValue) {
      this.hasIncreaseInPeriod = true;
    } else if (anchorValue === nextMonthValue || (anchorValue > farthestMonthValue && anchorValue <= nextMonthValue)) {
      this.isIncreaseNextMonth = true;
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

    let anchorValue = 0;
    if (this.selectedIncreaseAnchorDate) {
      const parts = this.selectedIncreaseAnchorDate.split('T')[0].split('-').map(Number);
      anchorValue = parts[0] * 100 + parts[1];
    }
    
    const coverageStart = this.getCoverageStartMonth();
    const currentMethodName = this.getNamePaymentMethodById(this.paymentDto.paymentMethodId).toLowerCase();

    for (let i = 0; i < this.paymentDto.advanceMonths; i++) {
      if (i === 0) {
        totalToPay += suggestedAmount; 
      } 
      else {
        const coverageMonth = this.addMonths(coverageStart.year, coverageStart.month, i);
        const loopMonth = coverageMonth.month;
        const loopYear = coverageMonth.year;
        let currentLoopValue = loopYear * 100 + loopMonth;

        let rentForThisMonth = currentRent;

        if (i === 1 && this.selectedPendingSurcharge > 0) {
          rentForThisMonth += this.selectedPendingSurcharge;
        }

        if (anchorValue > 0 && currentLoopValue >= anchorValue) {
          if (this.paymentDto.increasePercentage && this.paymentDto.increasePercentage > 0) {
            rentForThisMonth += currentRent * (this.paymentDto.increasePercentage / 100);
          }
        }

        rentForThisMonth = this.roundRentAmount(rentForThisMonth, currentMethodName);
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
      this.currentIncreaseFlow = 'advance';
      this.increasePromptReason = 'Dentro de los meses que está pagando, el cliente tiene programado un aumento.';
      this.calculateProjectedRent();
      this.showIncreaseOverlay = true;
    } else {
      this.currentIncreaseFlow = (this.isIncreaseNextMonth && !isPriceLocked) ? 'normal' : 'none';
      this.showSummarySwal();
    }
  }

  confirmIncrease() {
    this.paymentDto.increasePercentage = this.increasePercentage;
    this.paymentDto.newRentAmount = this.projectedNewRent; 
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
    this.paymentDto.newRentAmount = 0; 
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
        commissionHtml = `<div class="flex justify-between items-center text-sm text-orange-700 font-semibold mt-3 pt-2.5 border-t border-gray-200"><span>Porción retenida por comisión / recargo</span><span class="font-bold">- ${this.formatARS(calc.difference)}</span></div>`;
    } else if (calc.isDiscount) {
        commissionHtml = `<div class="flex justify-between items-center text-sm text-emerald-700 font-semibold mt-3 pt-2.5 border-t border-gray-200"><span>Bonificación a favor aplicada</span><span class="font-bold">+ ${this.formatARS(calc.difference)}</span></div>`;
    } else {
        commissionHtml = `<div class="mt-3 pt-2.5 border-t border-gray-200 text-xs text-gray-500 font-medium flex items-center justify-between"><span>Método de pago habitual</span><span class="text-green-600 font-bold">Sin diferencias ($0.00)</span></div>`;
    }

    const clientName = this.selectedClientName || 'Cliente';
    const clientNumber = this.selectedPaymentIdentifier || '';
    const methodName = this.getNamePaymentMethodById(this.paymentDto.paymentMethodId);
    const dateStr = new Date(this.paymentDto.date).toLocaleDateString('es-AR');

    const scenario = this.getSurchargeScenario();
    const interestAmt = this.calculateInterestAmount();
    const isImmediate = (scenario === 'B' && this.selectedSurchargeAction === 'immediate') ||
                        (scenario === 'C' && this.applyScenarioCInterest && this.selectedSurchargeAction === 'immediate');

    const isPotentiallyDuplicate = this.isPotentiallyDuplicate();
    let warningBannerHtml = isPotentiallyDuplicate ? `
        <div class="bg-amber-50/90 border border-amber-200/80 px-3.5 py-2.5 mb-4 rounded-xl text-left flex items-center justify-between gap-3 shadow-2xs">
          <div class="flex items-center gap-2">
            <h4 class="text-xs font-bold text-amber-900 uppercase tracking-wider m-0">Posible réplica de pago / Al día</h4>
          </div>
          <span class="bg-amber-100/90 text-amber-800 border border-amber-300 px-2.5 py-0.5 rounded-lg text-[10px] font-semibold shrink-0">Al día</span>
        </div>` : '';

    let surchargeBannerHtml = '';
    if (scenario === 'A') {
      surchargeBannerHtml = `
        <div class="bg-emerald-50/90 border border-emerald-200/80 p-3.5 mb-4 rounded-xl text-left flex items-start justify-between gap-3 shadow-2xs">
          <div>
            <h4 class="text-xs font-bold text-emerald-900 uppercase tracking-wider flex items-center gap-2">
              <span>Recargo por mora condonado</span>
              <button type="button" id="swal-edit-interest-btn-a" class="inline-flex items-center gap-1 px-2 py-0.5 rounded bg-white border border-emerald-300 text-[10px] font-bold text-emerald-800 hover:bg-emerald-100 transition-colors shadow-2xs">Modificar monto</button>
            </h4>
            <p class="text-xs text-emerald-700 mt-0.5">El recargo pendiente de <span class="font-bold">${this.formatARS(this.selectedPendingSurcharge)}</span> será eliminado automáticamente porque el pago se registra dentro del plazo (día 10 o antes).</p>
          </div>
          <span class="bg-emerald-100/90 text-emerald-800 border border-emerald-300 px-2.5 py-1 rounded-lg text-[10px] font-semibold shrink-0">Día ≤ 10</span>
        </div>`;
    } else if (scenario === 'B') {
      surchargeBannerHtml = `
        <div class="bg-amber-50/90 border border-amber-200/80 p-4 rounded-xl mb-4 text-left shadow-2xs space-y-3">
          <div class="flex items-start justify-between gap-3 border-b border-amber-200/60 pb-2.5">
            <div>
              <h4 class="text-xs font-bold text-amber-900 uppercase tracking-wider flex items-center gap-2">
                <span>Recargo por mora pendiente (${this.formatARS(this.selectedPendingSurcharge)})</span>
                <button type="button" id="swal-edit-interest-btn-b" class="inline-flex items-center gap-1 px-2 py-0.5 rounded bg-white border border-amber-300 text-[10px] font-bold text-amber-800 hover:bg-amber-100 transition-colors shadow-2xs">Modificar monto</button>
              </h4>
              <p class="text-xs text-amber-700 mt-0.5">El cliente registra un recargo por pago fuera de término. Seleccioná cómo aplicarlo:</p>
            </div>
            <span class="bg-amber-100/90 text-amber-800 border border-amber-300 px-2.5 py-1 rounded-lg text-[10px] font-semibold shrink-0">Día > 10</span>
          </div>
          <div class="space-y-2 pt-1 text-xs font-semibold text-amber-900">
            <label class="flex items-center gap-2.5 cursor-pointer p-2 rounded-lg hover:bg-amber-100/60 transition-colors">
              <input type="radio" name="swal-surcharge-action" value="next_payment" ${this.selectedSurchargeAction === 'next_payment' ? 'checked' : ''} class="text-amber-600 focus:ring-amber-500 w-4 h-4">
              <span>Cobrar en próximo pago (por defecto)</span>
            </label>
            <label class="flex items-center gap-2.5 cursor-pointer p-2 rounded-lg hover:bg-amber-100/60 transition-colors">
              <input type="radio" name="swal-surcharge-action" value="immediate" ${this.selectedSurchargeAction === 'immediate' ? 'checked' : ''} class="text-amber-600 focus:ring-amber-500 w-4 h-4">
              <span>Cobrar ahora (se suma al pago actual)</span>
            </label>
            <label class="flex items-center gap-2.5 cursor-pointer p-2 rounded-lg hover:bg-amber-100/60 transition-colors">
              <input type="radio" name="swal-surcharge-action" value="forgive" ${this.selectedSurchargeAction === 'forgive' ? 'checked' : ''} class="text-amber-600 focus:ring-amber-500 w-4 h-4">
              <span>Condonar recargo (no cobrar)</span>
            </label>
          </div>
        </div>`;
    } else if (scenario === 'C') {
      const baseImp = (this.selectedInterestAmount || 0) + (this.selectedCurrentRent || 0);
      surchargeBannerHtml = `
        <div class="bg-amber-50/90 border border-amber-200/80 p-4 rounded-xl mb-4 text-left shadow-2xs space-y-3">
          <div class="flex items-start justify-between gap-3 border-b border-amber-200/60 pb-2.5">
            <div>
              <h4 class="text-xs font-bold text-amber-900 uppercase tracking-wider flex items-center gap-2">
                <span>Aplicar intereses por mora (${this.formatARS(interestAmt)})</span>
                <button type="button" id="swal-edit-interest-btn-c" class="inline-flex items-center gap-1 px-2 py-0.5 rounded bg-white border border-amber-300 text-[10px] font-bold text-amber-800 hover:bg-amber-100 transition-colors shadow-2xs">Modificar monto</button>
              </h4>
              <p class="text-xs text-amber-700 mt-0.5">Fecha posterior al día 10. Cálculo: 10% de base imponible (${this.formatARS(baseImp)}).</p>
            </div>
            <label class="flex items-center gap-2 cursor-pointer shrink-0 pt-1">
              <input type="checkbox" id="swal-apply-scenario-c" ${this.applyScenarioCInterest ? 'checked' : ''} class="rounded text-amber-600 focus:ring-amber-500 w-4 h-4">
              <span class="text-xs font-bold text-amber-900">Aplicar</span>
            </label>
          </div>
          <div id="swal-scenario-c-options" class="space-y-2 pt-1 text-xs font-semibold text-amber-900 ${!this.applyScenarioCInterest ? 'hidden' : ''}">
            <label class="flex items-center gap-2.5 cursor-pointer p-2 rounded-lg hover:bg-amber-100/60 transition-colors">
              <input type="radio" name="swal-surcharge-action-c" value="next_payment" ${this.selectedSurchargeAction === 'next_payment' ? 'checked' : ''} class="text-amber-600 focus:ring-amber-500 w-4 h-4">
              <span>Cobrar en próximo pago (por defecto)</span>
            </label>
            <label class="flex items-center gap-2.5 cursor-pointer p-2 rounded-lg hover:bg-amber-100/60 transition-colors">
              <input type="radio" name="swal-surcharge-action-c" value="immediate" ${this.selectedSurchargeAction === 'immediate' ? 'checked' : ''} class="text-amber-600 focus:ring-amber-500 w-4 h-4">
              <span>Cobrar ahora (se suma al pago actual)</span>
            </label>
          </div>
        </div>`;
    }

    let moneyBoxesHtml = '';
    if (this.paymentDto.isAdvancePayment || isImmediate) {
        const displayTotalEntered = this.paymentDto.amount + (isImmediate ? interestAmt : 0);
        moneyBoxesHtml = `
          <div class="bg-indigo-50/70 border border-indigo-200/80 p-4 rounded-xl mb-4 text-left shadow-2xs">
            <label class="block text-xs font-bold text-indigo-900 uppercase tracking-wider mb-2">
              ${isImmediate ? 'Dinero entregado por el cliente (Modificable si paga de más o menos)' : 'Dinero entregado por el cliente'}
            </label>
            <div class="relative">
              <span class="absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-500 font-bold text-lg">$</span>
              <input id="swal-custom-amount" type="text" value="${this.formatARSInput(displayTotalEntered)}" 
                     class="w-full pl-8 pr-4 py-2.5 rounded-xl border border-indigo-300 font-bold text-xl text-gray-900 focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white shadow-xs">
            </div>
            <p class="text-xs text-indigo-700/80 mt-1.5 font-medium">
              ${isImmediate ? 'Modificá este valor si el cliente entregó una suma diferente; la deuda neta cancelada se recalcula automáticamente.' : 'Modificá este valor si el cliente entregó una suma diferente.'}
            </p>
          </div>
        `;
    }

    const displayAmountEntered = calc.amountEntered + (isImmediate ? interestAmt : 0);
    const displayEquivalentDebtPaid = calc.equivalentDebtPaid + (isImmediate ? interestAmt : 0);

    Swal.fire({
      html: `
        <div class="text-left space-y-4">
          <!-- Cabecera limpia con estilo Figma/Notion -->
          <div class="flex items-center justify-between pb-4 border-b border-gray-100">
            <div class="flex items-center gap-3">
              <div class="w-11 h-11 rounded-xl bg-indigo-50 border border-indigo-100 flex items-center justify-center text-indigo-600 shadow-2xs shrink-0">
                <svg class="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
              </div>
              <div>
                <h3 class="text-lg font-bold text-gray-900 leading-tight">Resumen de Cobranza</h3>
                <p class="text-xs text-gray-500 font-medium mt-0.5">Confirmá los datos antes de registrar el ingreso</p>
              </div>
            </div>
          </div>

          ${warningBannerHtml}
          ${surchargeBannerHtml}
          ${moneyBoxesHtml}

          <!-- Tarjetas KPI (Dinero ingresado vs Deuda Cancelada) -->
          ${(!calc.isSurcharge && !calc.isDiscount) ? `
          <div class="my-4 p-3.5 rounded-xl bg-gradient-to-r from-slate-50 to-indigo-50/40 border border-slate-200/80 shadow-2xs flex items-center justify-between gap-3">
            <div class="flex items-center gap-2.5">
              <div class="w-8 h-8 rounded-lg bg-indigo-100/70 text-indigo-700 flex items-center justify-center shrink-0 shadow-2xs">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5"><path stroke-linecap="round" stroke-linejoin="round" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
              </div>
              <div>
                <span class="text-xs font-bold text-slate-800 tracking-wide block leading-tight">Total Cobrado & Acreditado</span>
                <span class="text-[10px] text-slate-500 font-medium block mt-0.5 leading-tight">Sin recargos ni descuentos del método</span>
              </div>
            </div>
            <span id="swal-cobrado-display" class="text-lg font-black text-slate-900 tabular-nums shrink-0">${this.formatARS(displayAmountEntered)}</span>
            <span id="swal-debt-display" class="hidden">${this.formatARS(displayEquivalentDebtPaid)}</span>
          </div>
          ` : `
          <div class="grid grid-cols-2 gap-3 my-4">
            <div class="p-3 rounded-xl bg-slate-50/90 border border-slate-200/80 shadow-2xs flex flex-col sm:flex-row sm:items-center justify-between gap-1.5">
              <div>
                <span class="text-[10px] font-bold text-slate-500 uppercase tracking-wider block leading-tight">Dinero Cobrado</span>
                <span class="text-[10px] text-slate-400 font-medium block leading-tight">Ingreso bruto a caja</span>
              </div>
              <span id="swal-cobrado-display" class="text-base sm:text-lg font-black text-slate-900 tabular-nums self-end sm:self-auto">${this.formatARS(displayAmountEntered)}</span>
            </div>
            <div class="p-3 rounded-xl ${calc.isDiscount ? 'bg-emerald-50/90 border-emerald-200/80' : 'bg-indigo-50/90 border-indigo-200/80'} shadow-2xs flex flex-col sm:flex-row sm:items-center justify-between gap-1.5">
              <div>
                <span class="text-[10px] font-bold ${calc.isDiscount ? 'text-emerald-700' : 'text-indigo-700'} uppercase tracking-wider block leading-tight">Deuda Cancelada</span>
                <span class="text-[10px] ${calc.isDiscount ? 'text-emerald-600' : 'text-indigo-600'} font-medium block leading-tight">Acreditación neta</span>
              </div>
              <span id="swal-debt-display" class="text-base sm:text-lg font-black ${calc.isDiscount ? 'text-emerald-900' : 'text-indigo-900'} tabular-nums self-end sm:self-auto">${this.formatARS(displayEquivalentDebtPaid)}</span>
            </div>
          </div>
          `}

          <!-- Desglose de Cuenta Corriente -->
          <div class="p-4 rounded-xl border border-gray-200/80 bg-white shadow-2xs">
            <div class="text-[11px] font-bold text-gray-400 mb-2 uppercase tracking-wider border-b border-gray-100 pb-2 flex items-center justify-between">
              <span>Desglose de Acreditación</span>
              <span>Monto</span>
            </div>
            <div class="flex justify-between items-center py-2 text-sm text-gray-800">
              <span class="font-bold">Acreditación base en cuenta</span>
              <span class="font-bold tabular-nums">${this.formatARS(calc.amountEntered)}</span>
            </div>
            ${isImmediate ? `<div class="flex justify-between items-center py-2 text-sm text-amber-800 font-medium border-t border-gray-100"><span>Interés por mora (cobrado en el acto)</span><span class="font-bold tabular-nums">+ ${this.formatARS(interestAmt)}</span></div>` : ''}
            ${commissionHtml}
          </div>
        </div>
      `,
      showCancelButton: true,
      confirmButtonText: 'Confirmar Transacción',
      cancelButtonText: 'Cancelar',
      buttonsStyling: false,
      width: '640px',
      customClass: { 
          confirmButton: 'bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-sm px-6 py-3 rounded-xl shadow-xs transition-all duration-150 mx-2 active:scale-95', 
          cancelButton: 'bg-gray-100 hover:bg-gray-200 text-gray-700 font-bold text-sm px-6 py-3 rounded-xl transition-all duration-150 active:scale-95', 
          popup: '!rounded-2xl !p-6 shadow-xl border border-gray-100 bg-white'
      },
      didOpen: () => {
          if (scenario === 'B') {
            const radios = document.querySelectorAll('input[name="swal-surcharge-action"]');
            radios.forEach(radio => {
              radio.addEventListener('change', (e: any) => {
                if (e.target.checked) {
                  this.selectedSurchargeAction = e.target.value;
                  Swal.close();
                  setTimeout(() => this.showSummarySwal(), 0);
                }
              });
            });
          } else if (scenario === 'C') {
            const chk = document.getElementById('swal-apply-scenario-c') as HTMLInputElement;
            if (chk) {
              chk.addEventListener('change', (e: any) => {
                this.applyScenarioCInterest = e.target.checked;
                Swal.close();
                setTimeout(() => this.showSummarySwal(), 0);
              });
            }
            const radiosC = document.querySelectorAll('input[name="swal-surcharge-action-c"]');
            radiosC.forEach(radio => {
              radio.addEventListener('change', (e: any) => {
                if (e.target.checked) {
                  this.selectedSurchargeAction = e.target.value;
                  Swal.close();
                  setTimeout(() => this.showSummarySwal(), 0);
                }
              });
            });
          }

          const editBtnA = document.getElementById('swal-edit-interest-btn-a');
          const editBtnB = document.getElementById('swal-edit-interest-btn-b');
          const editBtnC = document.getElementById('swal-edit-interest-btn-c');
          const handleEdit = () => { this.modifyInterestAmount(true); };
          if (editBtnA) editBtnA.addEventListener('click', handleEdit);
          if (editBtnB) editBtnB.addEventListener('click', handleEdit);
          if (editBtnC) editBtnC.addEventListener('click', handleEdit);

          const updateSummaryDisplays = (rawInputValue: number) => {
              const baseAmt = Math.max(0, rawInputValue - (isImmediate ? interestAmt : 0));
              const newCalc = this.getCalculatedAmounts(baseAmt, this.paymentDto.paymentMethodId, this.selectedPreferredPaymentId);
              const cobradoEl = document.getElementById('swal-cobrado-display');
              if (cobradoEl) cobradoEl.innerText = this.formatARS(newCalc.amountEntered + (isImmediate ? interestAmt : 0));
              const debtEl = document.getElementById('swal-debt-display');
              if (debtEl) debtEl.innerText = this.formatARS(newCalc.equivalentDebtPaid + (isImmediate ? interestAmt : 0));
          };

          const input = document.getElementById('swal-custom-amount') as HTMLInputElement;
          if (input) {
              this.attachCurrencyFormatListener(input, (newVal: number) => {
                  updateSummaryDisplays(newVal);
              });
          }
      },
      preConfirm: () => {
          const input = document.getElementById('swal-custom-amount') as HTMLInputElement;
          if (input && input.value) {
              const cleanVal = input.value.replace(/\./g, '').replace(',', '.');
              const parsed = parseFloat(cleanVal);
              if (!isNaN(parsed)) {
                  return Math.max(0, parsed - (isImmediate ? interestAmt : 0));
              }
          }
          return this.paymentDto.amount;
      }
    }).then((result) => {
      if (result.isConfirmed) {
        this.paymentDto.amount = result.value;
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

  executeBackendCall() {
    const calc = this.getCalculatedAmounts(this.paymentDto.amount, this.paymentDto.paymentMethodId, this.selectedPreferredPaymentId);
    
    // Blindaje de TimeZone igual que en Finances
    const localDate = this.paymentDto.date;
    const adjustedDate = new Date(localDate.getTime() - (localDate.getTimezoneOffset() * 60000));

    const scenario = this.getSurchargeScenario();
    let action: string | undefined = undefined;
    let amount: number | undefined = undefined;

    if (scenario === 'A') {
      action = 'forgive';
      amount = this.selectedPendingSurcharge;
    } else if (scenario === 'B') {
      action = this.selectedSurchargeAction;
      amount = this.selectedPendingSurcharge;
    } else if (scenario === 'C') {
      if (this.applyScenarioCInterest) {
        action = this.selectedSurchargeAction;
        amount = this.calculateInterestAmount();
      } else {
        action = 'forgive';
        amount = 0;
      }
    }

    const payloadToSave: CreatePaymentDTO = {
      ...this.paymentDto,
      date: adjustedDate,
      amount: calc.amountEntered + ((action === 'immediate') ? (amount ?? 0) : 0),
      commissionAmount: calc.isSurcharge ? calc.difference : (calc.isDiscount ? -calc.difference : 0),
      commissionConcept: calc.isSurcharge 
          ? `Recargo por pago en ${this.getNamePaymentMethodById(this.paymentDto.paymentMethodId)} (${calc.selectedCommission}%)` 
          : (calc.isDiscount ? `Bonificación por pago en ${this.getNamePaymentMethodById(this.paymentDto.paymentMethodId)} (${calc.selectedCommission}%)` : ''),
      surchargeAction: action,
      surchargeAmount: amount
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

  recalculateTotalAmount() {
    this.receiptTotalAmountCustom = this.receiptConcepts.reduce((acc, curr) => acc + (Number(curr.amount) || 0), 0);
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
    this.recalculateTotalAmount();
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
      clientNumber: this.receiptPaymentInfo.paymentIdentifier ?? 0,
      clientName: this.receiptPaymentInfo.clientName ?? "",
      concepts: this.receiptConcepts,
      totalAmount: this.receiptTotalAmountCustom
    });
    this.closeReceiptModal();
  }

  addReceiptConcept() {
    this.receiptConcepts.push({ description: '', amount: 0 });
    this.recalculateTotalAmount();
  }

  removeReceiptConcept(index: number) {
    if (this.receiptConcepts.length > 1) {
      this.receiptConcepts.splice(index, 1);
      this.recalculateTotalAmount();
    }
  }

  get receiptTotalAmount(): number {
    return this.receiptConcepts.reduce((acc, curr) => acc + (curr.amount || 0), 0);
  }
}