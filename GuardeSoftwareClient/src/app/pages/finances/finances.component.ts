import { Component, OnInit, HostListener } from '@angular/core';
import { PaymentService } from '../../core/services/payment-service/payment.service';
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
import { CurrencyFormatDirective } from '../../shared/directives/currency-format.directive';
import { ActivatedRoute, Router } from '@angular/router';
import { PdfGeneratorService } from '../../core/services/pdfGenerator-service/pdf-generator.service';

export interface DetailedPaymentView extends DetailedPaymentDTO {
  groupPos?: 'start' | 'middle' | 'end' | 'none';
  isGrouped?: boolean;
}

interface PaymentMonthBreakdown {
  year: number;
  month: number;
  label: string;
  amount: number;
}

@Component({
  selector: 'app-finances',
  imports: [FormsModule, CommonModule, IconComponent, NgxPaginationModule, CurrencyFormatDirective],
  templateUrl: './finances.component.html',
  styleUrl: './finances.component.css'
})
export class FinancesComponent implements OnInit {

  constructor(
    private paymentService: PaymentService,
    private paymentMethodService: PaymentMethodService,
    private clientService: ClientService,
    private route: ActivatedRoute,
    private router: Router,
    private pdfGeneratorService: PdfGeneratorService
  ){}

  clients: Client[] = [];
  showClientModal = false;
  selectedClientId: number | 0 = 0;
  selectedClientName: string = '';
  selectedClientIdentifier: number | 0 = 0;
  selectedClientBalance: number | 0 = 0;
  selectedClientPreviousBalance: number | 0 = 0;
  selectedClientRentAmount: number | 0 = 0;
  selectedPreferredPaymentId: number = 1;
  

  pendingRentals: PendingRentalDTO[] = [];
  payments: DetailedPaymentView[] = [];
  paymentMethods: PaymentMethod[] = [];

  filteredPayments: DetailedPaymentView[] = [];
  totals = {
    count: 0,
    amount: 0
  };
  searchPayment: string = '';
  sortField: string = 'paymentDate';
  sortDirection: 'asc' | 'desc' = 'desc';

  page: number = 1;
  itemsPerPage: number = 100;

  manualDateEnabled = false;
  dateString: string = '';
  searchClient: string = '';
  amountOriginal = 0;
  selectedClientIncreaseAnchorDate: string | null = null;
  
  // Banderas de lÃ³gica
  hasIncreaseInPeriod: boolean = false;
  public isIncreaseNextMonth: boolean = false;

  // Bandera para saber en toda la clase si el cliente ya pagó su mes
  public isCurrentMonthPaidFlag: boolean = false;
  
  // Índice para saber en qué etapa del modal múltiple estamos
  public currentIncreaseStep: number = 0;

  // --- VARIABLES PARA EL MODAL DE AUMENTO ---
  public showIncreaseOverlay: boolean = false;
  public increaseResolved: boolean = false;
  public projectedNewRent: number = 0;
  public projectedNextIncreaseDate: Date | null = null;
  public increasePromptReason: string = '';
  public increasePercentage: number = 0;
  public currentIncreaseFlow: 'advance' | 'normal' | 'none' = 'none';
  public isPreviousBalanceSelected: boolean = false;
  public selectedPendingSurcharge: number = 0;
  public selectedInterestAmount: number = 0;
  public selectedSurchargeAction: string = 'next_payment';
  public applyScenarioCInterest: boolean = true;
  public selectedClientLastMonth: string = '';
  public paymentMonthBreakdown: PaymentMonthBreakdown[] = [];
  public selectedClientNextPaymentDay: Date | string | null = null;
  public selectedClientNextIncreaseDay: Date | string | null = null;
  public customScenarioCInterest: number | null = null;

  returnToUrl: string | null = null;

  autoOpenClientId: number | null = null;

  // --- VARIABLES PARA EL MODAL DE RECIBO ---
  showReceiptModal = false;
  receiptPaymentInfo: any | null = null;
  receiptConcepts: { description: string, amount: number }[] = [];
  receiptDateStr: string = '';
  receiptTotalAmountCustom: number = 0;
  pendingReturnUrl: string | null = null;

  paymentDto: CreatePaymentDTO = {
      clientId: 0,
      movementType: 'CREDITO',
      concept: 'Pago de alquiler',
      amount: 0,
      paymentMethodId: 1,
      date: new Date(),
      isAdvancePayment: false,
      advanceMonths: 0
    };

    // --- VARIABLES DE COLA DE AUMENTOS ---
  increaseQueue: any[] = [];
  confirmedIncreases: any[] = [];
  currentPendingIncrease: any = null;
  selectedClientFrequency: number = 4; // Frecuencia de aumento del cliente (ej: 4 meses)
  originalBaseRentCopy: number = 0; // Para no perder el precio original en la UI

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      if (params['autoOpenPayment']) {
        this.autoOpenClientId = Number(params['autoOpenPayment']);
      }
      if (params['returnTo']) {
        this.returnToUrl = params['returnTo']; // <-- Lo guardamos
      }
    });

    this.loadPayments();
    this.loadPaymentMethods();
    this.loadClients();
  }

  private sortPaymentsDesc(payments: any[]): any[] {
    return payments.sort((a, b) => {
      const getDayString = (dateVal: any): string => {
        if (!dateVal) return '';
        const d = new Date(dateVal);
        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, '0');
        const day = String(d.getDate()).padStart(2, '0');
        return `${y}-${m}-${day}`;
      };

      const dayA = getDayString(a.paymentDate);
      const dayB = getDayString(b.paymentDate);

      if (dayB !== dayA) {
        return dayB.localeCompare(dayA);
      }

      const idA = Number(a.movementId || a.paymentId || 0);
      const idB = Number(b.movementId || b.paymentId || 0);
      return idB - idA;
    });
  }

  loadPayments(): void {
    this.paymentService.getDetailedPayment().subscribe({
      next: (data) => {
        this.payments = this.sortPaymentsDesc(data) as DetailedPaymentView[];
        this.filterPayments();
      },
      error: (err) => console.error('Error al cargar payments:', err)
    });
  }

  loadPaymentMethods(): void {
    this.paymentMethodService.getPaymentMethods().subscribe({
      next: (data) => this.paymentMethods = data,
      error: (err) => console.error('error al cargar los metodos de pago',err)
    });
  }

  loadClients(): void {
    this.clientService.getClients().subscribe({
      next: (data) => {
        this.clients = data;
        
        // MAGIA: Si tenemos una orden de auto-apertura pendiente
        if (this.autoOpenClientId) {
          const clientToPay = this.clients.find(c => c.id === this.autoOpenClientId);
          
          if (clientToPay) {
            // 1. Abrimos el modal
            this.OpenPaymentModal();
            // 2. Llenamos el buscador visualmente (opcional pero queda bien)
            this.searchClient = clientToPay.fullName;
            // 3. Ejecutamos tu funciÃ³n exacta que carga toda la deuda y comisiones
            this.selectClient(clientToPay);
          }

          // Limpiamos la URL para que si el usuario aprieta F5, no se vuelva a abrir solo
          this.router.navigate([], { queryParams: { autoOpenPayment: null }, queryParamsHandling: 'merge' });
          this.autoOpenClientId = null;
        }
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
    return client ? client.fullName: ' ';
  }

  getClientIdentifierById(id: number): string {
    const client = this.clients.find(m => m.id === id);
    return client && client.paymentIdentifier !== null && client.paymentIdentifier !== undefined 
      ? String(client.paymentIdentifier) 
      : '';
  }

  selectedMethodFilter: string = '';
  selectedMonth: string = '';
  selectedDay: string = '';
  dateFilterType: string = 'none';

  resetFilters(): void {
    this.searchPayment = '';
    this.selectedMethodFilter = '';
    this.selectedMonth = '';
    this.selectedDay = '';
    this.dateFilterType = 'none';
    this.filterPayments();
  }

  onDateFilterTypeChange(): void {
    this.selectedMonth = '';
    this.selectedDay = '';
    this.filterPayments();
  }

  formatIdentifier(value: number | string | null | undefined): string {
    if (value === null || value === undefined) return '';
    const strVal = String(value).trim().replace(',', '.');
    if (strVal === '') return '';
    const num = Number(strVal);
    if (isNaN(num)) return String(value);
    return num.toFixed(2).replace('.', ',');
  }

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
    const termForId = term.replace(',', '.');
    
    const method = this.selectedMethodFilter.toLowerCase();

    let filtered = this.payments.filter(p => {
      const clientName = p.clientName?.toLowerCase() || '';
      const paymentIdentifier = (p.paymentIdentifier?.toString() || '').toLowerCase().replace(',', '.');
      const paymentMethodName = p.paymentMethodName?.toLowerCase() || '';

      const matchesSearch = clientName.includes(term) || paymentIdentifier.includes(termForId);
      const matchesMethod = !method || paymentMethodName === method;
      
      let matchesDate = true;
      if (p.paymentDate) {
        const d = new Date(p.paymentDate);
        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, '0');
        
        if (this.dateFilterType === 'month' && this.selectedMonth) {
          const localYearMonth = `${y}-${m}`;
          matchesDate = localYearMonth === this.selectedMonth;
        } else if (this.dateFilterType === 'day' && this.selectedDay) {
          const day = String(d.getDate()).padStart(2, '0');
          const localYearMonthDay = `${y}-${m}-${day}`;
          matchesDate = localYearMonthDay === this.selectedDay;
        }
      } else {
        if ((this.dateFilterType === 'month' && this.selectedMonth) || (this.dateFilterType === 'day' && this.selectedDay)) {
          matchesDate = false;
        }
      }

      return matchesSearch && matchesMethod && matchesDate;
    });

    this.sortFilteredPayments(filtered);
    
    this.filteredPayments = filtered;
    this.processGroups();
    this.calculateTotals();
  }

  handleSort(field: string): void {
    if (this.sortField === field) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortField = field;
      this.sortDirection = field === 'paymentDate' ? 'desc' : 'asc';
    }
    this.filterPayments();
  }

  getSortIcon(field: string): string {
    if (this.sortField !== field) {
      return 'arrow-up-down';
    }
    return this.sortDirection === 'asc' ? 'arrow-up' : 'arrow-down';
  }

  private sortFilteredPayments(payments: DetailedPaymentView[]): void {
    payments.sort((a, b) => {
      let comparison = 0;

      if (this.sortField === 'paymentDate') {
        const getDayString = (dateVal: any): string => {
          if (!dateVal) return '';
          const d = new Date(dateVal);
          const y = d.getFullYear();
          const m = String(d.getMonth() + 1).padStart(2, '0');
          const day = String(d.getDate()).padStart(2, '0');
          return `${y}-${m}-${day}`;
        };
        const dayA = getDayString(a.paymentDate);
        const dayB = getDayString(b.paymentDate);

        if (dayB !== dayA) {
          comparison = dayA.localeCompare(dayB);
        } else {
          const idA = Number(a.movementId || a.paymentId || 0);
          const idB = Number(b.movementId || b.paymentId || 0);
          comparison = idA - idB;
        }
      } else if (this.sortField === 'amount') {
        comparison = (Number(a.amount) || 0) - (Number(b.amount) || 0);
      } else if (this.sortField === 'paymentIdentifier') {
        const idA = Number((a.paymentIdentifier || '').toString().replace(',', '.')) || 0;
        const idB = Number((b.paymentIdentifier || '').toString().replace(',', '.')) || 0;
        comparison = idA - idB;
      } else if (this.sortField === 'clientName') {
        comparison = (a.clientName || '').localeCompare(b.clientName || '', 'es');
      } else if (this.sortField === 'paymentMethodName') {
        comparison = (a.paymentMethodName || '').localeCompare(b.paymentMethodName || '', 'es');
      } else if (this.sortField === 'concept') {
        comparison = (a.concept || '').localeCompare(b.concept || '', 'es');
      }

      if (comparison === 0 && this.sortField !== 'paymentDate') {
        const dateA = new Date(a.paymentDate).getTime();
        const dateB = new Date(b.paymentDate).getTime();
        if (dateB !== dateA) {
          return dateB - dateA;
        }
        const idA = Number(a.movementId || a.paymentId || 0);
        const idB = Number(b.movementId || b.paymentId || 0);
        return idB - idA;
      }

      return this.sortDirection === 'asc' ? comparison : -comparison;
    });
  }

  private processGroups(): void {
    for (let i = 0; i < this.filteredPayments.length; i++) {
      const p = this.filteredPayments[i];
      const prev = this.filteredPayments[i - 1];
      const next = this.filteredPayments[i + 1];

      if (p.paymentId && p.paymentId > 0) {
        const sameAsPrev = prev && prev.paymentId === p.paymentId;
        const sameAsNext = next && next.paymentId === p.paymentId;

        if (!sameAsPrev && sameAsNext) {
          p.groupPos = 'start';
        } else if (sameAsPrev && sameAsNext) {
          p.groupPos = 'middle';
        } else if (sameAsPrev && !sameAsNext) {
          p.groupPos = 'end';
        } else {
          p.groupPos = 'none';
        }
      } else {
        p.groupPos = 'none';
      }
      p.isGrouped = p.groupPos !== 'none';
    }
  }

  calculateTotals(): void {
    this.totals = {
      count: this.filteredPayments.length,
      amount: 0
    };

    this.filteredPayments.forEach(p => {
      this.totals.amount += Number(p.amount) || 0;
    });
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
    if (this.showClientModal) {
      this.closeClientModal();
      return;
    }
  }

  closeClientModal() {
    this.showClientModal = false;
    this.searchClient = '';
    this.selectedClientId = 0;
    this.selectedClientIdentifier = 0;
    this.selectedClientName = '';
    this.selectedClientBalance = 0;
    this.selectedClientPreviousBalance = 0;
    this.selectedClientRentAmount = 0;
    this.selectedClientIncreaseAnchorDate = null;
    this.selectedClientLastMonth = '';
    this.selectedClientNextPaymentDay = null;
    this.selectedClientNextIncreaseDay = null;
    this.paymentMonthBreakdown = [];
    this.manualDateEnabled = false;
    this.showIncreaseOverlay = false;
    this.currentIncreaseFlow = 'none';
    
    const now = new Date();
    this.dateString = now.toISOString().split('T')[0];

    this.paymentDto = {
      clientId: 0, movementType: 'CREDITO', concept: ` `, amount: 0, paymentMethodId: 1, date: now, isAdvancePayment: false, advanceMonths: 0,
      skipFutureProjection: false 
    };
    this.updateConceptFromDate(now);

    if (this.returnToUrl) {
      const url = this.returnToUrl;
      this.returnToUrl = null;
      this.router.navigate(['/' + url]);
    }
  }

  private updateProjectedDate() {
    // Calculamos el salto real en meses según la frecuencia del cliente (ej: 4 - 1 = 3)
    const stepMonths = (this.selectedClientFrequency || 4) - 1;

    if (this.currentIncreaseFlow === 'advance' && this.currentPendingIncrease) {
      // FIX CRÍTICO: Si estamos en la cola de aumentos, calculamos la proyección
      // partiendo desde el año y mes del elemento que se está resolviendo en este modal
      const baseYear = this.currentPendingIncrease.year;
      const baseMonth = this.currentPendingIncrease.month; // Formato 1 al 12

      const nextDate = this.addMonths(baseYear, baseMonth, stepMonths);
      
      // En JavaScript los meses van de 0 a 11, por eso restamos 1
      this.projectedNextIncreaseDate = new Date(nextDate.year, nextDate.month - 1, 1);
    } 
    else if (this.selectedClientIncreaseAnchorDate) {
      // Flujo normal o de un solo mes: mantenemos el cálculo base tradicional
      let currentAnchor = new Date(this.selectedClientIncreaseAnchorDate);
      this.projectedNextIncreaseDate = new Date(currentAnchor.getFullYear(), currentAnchor.getMonth() + stepMonths, 1);
    }
  }

  OpenPaymentModal() {
    this.showClientModal = true;
    this.searchClient = '';
    this.selectedClientId = 0;
    this.selectedClientIdentifier = 0;
    this.showIncreaseOverlay = false;
    this.currentIncreaseFlow = 'none';
    const now = new Date();
    this.manualDateEnabled = false;
    this.dateString = now.toISOString().split('T')[0];
    this.paymentDto = {
      clientId: 0, movementType: 'CREDITO', concept: ` `, amount: 0, paymentMethodId: 1, date: now, isAdvancePayment: false, advanceMonths: 0,
      skipFutureProjection: false 
    };
    this.updateConceptFromDate(now);
  }

  commision:number = 0;
  newAmount: number = 0;

  private getCommissionByMethodId(paymentMethodId: number): number {
    const id = Number(paymentMethodId);
    if (!id || Number.isNaN(id)) return 0;
    const method = this.paymentMethods.find(m => m.id === id);
    return method?.commission ?? 0;
  }

  private getMonthName(month: number): string {
    const monthNames = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'];
    return monthNames[Math.max(0, Math.min(11, month - 1))];
  }

  private formatMonthLabel(year: number, month: number): string {
    return `${this.getMonthName(month)} ${year}`;
  }

  private formatMonthYearLabel(year: number, month: number): string {
    return `${String(month).padStart(2, '0')}/${year}`;
  }

  private parseMonthYear(value: string | null | undefined): { year: number; month: number } | null {
    if (!value) return null;

    const normalized = value.includes('T') ? value.split('T')[0] : value;
    const separator = normalized.includes('/') ? '/' : '-';
    const parts = normalized.split(separator);
    if (parts.length < 2) return null;

    const first = Number(parts[0]);
    const second = Number(parts[1]);
    if (!Number.isFinite(first) || !Number.isFinite(second)) return null;

    if (parts[0].length === 4) {
      return { year: first, month: second };
    }

    return { year: second, month: first };
  }

  private toMonthComparable(year: number, month: number): number {
    return year * 100 + month;
  }

  private buildPaymentMonthBreakdown(): PaymentMonthBreakdown[] {
    if (!this.selectedClientId) return [];

    const monthsToCover = this.paymentDto.isAdvancePayment && this.paymentDto.advanceMonths
      ? Math.max(1, this.paymentDto.advanceMonths)
      : 1;
    const coverageStart = this.getCoverageStartMonth();
    
    // La base siempre debe ser el alquiler original intocable
    const baseRent = Number(this.originalBaseRentCopy || this.selectedClientRentAmount || 0);
    const currentDebt = this.selectedClientBalance < 0 ? Math.abs(Number(this.selectedClientBalance)) : 0;

    return Array.from({ length: monthsToCover }, (_, index) => {
      const month = this.addMonths(coverageStart.year, coverageStart.month, index);
      const monthComparable = month.year * 100 + month.month;
      
      let amount = (index === 0 && currentDebt > 0) ? currentDebt : baseRent;

      // FIX CRÍTICO: Buscamos si para este mes ya se confirmó algún escalón de aumento
      if (!(index === 0 && currentDebt > 0)) {
         // Leemos los aumentos aplicados finales, o la cola si estamos en medio del proceso
         const sourceArray = this.paymentDto.appliedIncreases && this.paymentDto.appliedIncreases.length > 0 
             ? this.paymentDto.appliedIncreases 
             : this.increaseQueue;

         if (sourceArray) {
             let activeIncrease = sourceArray
                 .filter(inc => inc.newRentAmount !== undefined && (inc.year * 100 + inc.month) <= monthComparable)
                 .sort((a,b) => (b.year * 100 + b.month) - (a.year * 100 + a.month))[0];

             if (activeIncrease && activeIncrease.newRentAmount) {
                 amount = activeIncrease.newRentAmount;
             }
         }
      }

      return {
        year: month.year,
        month: month.month,
        label: this.formatMonthLabel(month.year, month.month),
        amount
      };
    });
  }

  private syncPaymentPreview(): void {
    if (this.isPreviousBalanceSelected) {
      this.paymentDto.concept = 'Pago de saldo anterior';
      return;
    }
    this.paymentMonthBreakdown = this.buildPaymentMonthBreakdown();

    if (this.paymentDto.isAdvancePayment && this.paymentDto.advanceMonths) {
      const months = this.paymentDto.advanceMonths;
      if (this.paymentMonthBreakdown.length > 0) {
        const monthLabels = this.paymentMonthBreakdown.map(item => item.label);
        const joinedLabels = monthLabels.length === 1
          ? monthLabels[0]
          : `${monthLabels.slice(0, -1).join(', ')} y ${monthLabels[monthLabels.length - 1]}`;
        this.paymentDto.concept = `Pago de ${months} mes${months === 1 ? '' : 'es'}: ${joinedLabels}`;
        return;
      }

      this.paymentDto.concept = `Pago adelantado de ${months} mes${months === 1 ? '' : 'es'}`;
      return;
    }

    const firstMonth = this.paymentMonthBreakdown[0];
    if (firstMonth) {
      this.paymentDto.concept = `Pago alquiler ${firstMonth.label}`;
      return;
    }

    const selectedMonth = this.getSelectedPaymentMonth();
    this.paymentDto.concept = `Pago alquiler ${this.formatMonthLabel(selectedMonth.year, selectedMonth.month)}`;
  }

  getSummaryBreakdown(): PaymentMonthBreakdown[] {
    const breakdown = this.buildPaymentMonthBreakdown();
    if (breakdown.length > 0) {
      this.paymentMonthBreakdown = breakdown;
      return breakdown;
    }

    return this.paymentMonthBreakdown;
  }

  getNextPaymentLabel(): string {
    if (this.selectedClientNextPaymentDay) {
      const date = new Date(this.selectedClientNextPaymentDay);
      if (!Number.isNaN(date.getTime())) {
        return this.formatMonthYearLabel(date.getFullYear(), date.getMonth() + 1);
      }
    }

    const coverageStart = this.getCoverageStartMonth();
    return this.formatMonthYearLabel(coverageStart.year, coverageStart.month);
  }

  getNextIncreaseLabel(): string {
    if (this.selectedClientNextIncreaseDay) {
      const date = new Date(this.selectedClientNextIncreaseDay);
      if (!Number.isNaN(date.getTime())) {
        return this.formatMonthYearLabel(date.getFullYear(), date.getMonth() + 1);
      }
    }

    if (!this.selectedClientIncreaseAnchorDate) {
      return 'N/D';
    }

    const date = new Date(this.selectedClientIncreaseAnchorDate);
    if (Number.isNaN(date.getTime())) {
      return 'N/D';
    }

    return this.formatMonthYearLabel(date.getFullYear(), date.getMonth() + 1);
  }

  getPaidStatusLabel(): string {
    if (!this.selectedClientId) {
      return '';
    }

    const currentMonth = this.getSelectedPaymentMonth();
    const currentMonthLabel = this.formatMonthLabel(currentMonth.year, currentMonth.month);
    const lastPaidMonth = this.parseMonthYear(this.selectedClientLastMonth);

    if (this.selectedClientBalance < 0) {
      const coverageStart = this.getCoverageStartMonth();
      return `Pendiente de ${this.formatMonthLabel(coverageStart.year, coverageStart.month)}`;
    }

    if (lastPaidMonth && this.toMonthComparable(lastPaidMonth.year, lastPaidMonth.month) > this.toMonthComparable(currentMonth.year, currentMonth.month)) {
      return `Pagado hasta ${this.formatMonthLabel(lastPaidMonth.year, lastPaidMonth.month)}`;
    }

    return `El mes de ${currentMonthLabel} ya está pago`;
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

  selectClient(client: any) {
    this.selectedClientId = client.id;
    this.selectedClientIdentifier = client.paymentIdentifier;
    this.paymentDto.clientId = client.id;
    this.selectedClientBalance = Number(client.balance ?? 0);
    this.selectedClientPreviousBalance = Number(client.previousBalance ?? 0);
    this.selectedClientRentAmount = Number(client.currentRent ?? 0);
    this.selectedClientIncreaseAnchorDate = client.increaseAnchorDate;
    this.selectedClientNextPaymentDay = client.nextPaymentDay ?? null;
    this.selectedClientNextIncreaseDay = client.nextIncreaseDay ?? client.increaseAnchorDate ?? null;
    this.selectedPreferredPaymentId = Number(client.preferredPaymentMethodId ?? 1); 
    this.paymentDto.paymentMethodId = this.selectedPreferredPaymentId;
    this.selectedPendingSurcharge = Number(client.pendingSurcharge ?? 0);
    this.selectedInterestAmount = Number(client.interestAmount ?? 0);
    this.selectedSurchargeAction = 'next_payment';
    this.applyScenarioCInterest = true;
    this.customScenarioCInterest = null;
    
    this.selectedClientFrequency = client.increaseFrequencyMonths || 4; 
    
    this.increaseResolved = false;
    this.increasePercentage = 0;
    this.paymentDto.increasePercentage = 0;
    this.showIncreaseOverlay = false;
    this.currentIncreaseFlow = 'none';
    this.selectedClientLastMonth = client.lastGeneratedMonthYear || '';
    
    // --- LÓGICA INFALIBLE DE $0 ---
    this.isCurrentMonthPaidFlag = false;
    if (client.nextPaymentDay) {
      // Extraemos solo el YYYY-MM seguro, sin importar la hora
      const dateString = client.nextPaymentDay.toString().split('T')[0]; 
      const parts = dateString.split('-');
      const nextMonthValue = parseInt(parts[0]) * 100 + parseInt(parts[1]);
      
      const today = new Date();
      const currentMonthValue = today.getFullYear() * 100 + (today.getMonth() + 1);
      
      // Si su próximo pago es mayor a este mes, significa que ya pagó.
      this.isCurrentMonthPaidFlag = nextMonthValue > currentMonthValue;
    }

    this.isPreviousBalanceSelected = false;
    const suggestedAmount = this.isCurrentMonthPaidFlag 
        ? 0 
        : (this.selectedClientBalance < 0 ? Math.abs(this.selectedClientBalance) : this.selectedClientRentAmount);

    if (this.selectedClientPreviousBalance !== 0 && suggestedAmount === Math.abs(Number(this.selectedClientPreviousBalance)) && suggestedAmount > 0) {
      this.isPreviousBalanceSelected = true;
      this.paymentDto.concept = 'Pago de saldo anterior';
    }

    this.paymentDto.amount = suggestedAmount;

    this.checkIncreaseLogic();
    this.syncPaymentPreview();
    this.onAmountChange(this.paymentDto.amount);
  }

  getPaymentDay(): number {
    if (this.manualDateEnabled && this.dateString) {
      const parts = this.dateString.split('-').map(Number);
      if (parts.length === 3) return parts[2];
    }
    return new Date().getDate();
  }

  isNextPaymentInNextMonthOrLater(): boolean {
    const nextDate = this.selectedClientNextPaymentDay;
    if (!nextDate) return false;
    const d = new Date(nextDate);
    const payDate = new Date(this.paymentDto.date || new Date());
    const nextMonthIndex = d.getFullYear() * 12 + d.getMonth();
    const payMonthIndex = payDate.getFullYear() * 12 + payDate.getMonth();
    return nextMonthIndex > payMonthIndex;
  }

  isPotentiallyDuplicate(): boolean {
    if (this.paymentDto?.isAdvancePayment || this.isPreviousBalanceSelected) return false;
    return this.isCurrentMonthPaidFlag || this.selectedClientBalance >= 0;
  }

  getSurchargeScenario(): 'A' | 'B' | 'C' | 'D' {
    if (this.isPreviousBalanceSelected) return 'D';
    const day = this.getPaymentDay();
    const hasSurcharge = (this.selectedPendingSurcharge > 0);
    if (hasSurcharge && day <= 10) return 'A';
    if (hasSurcharge && day > 10) return 'B';
    if (!hasSurcharge && day > 10 && this.selectedClientRentAmount > 0 && !this.isCurrentMonthPaidFlag) return 'C';
    if (this.paymentDto.isAdvancePayment) return 'D';
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
      const baseImponible = (this.selectedInterestAmount || 0) + (this.selectedClientRentAmount || 0);
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

  getBaseSuggestedAmount(): number {
    if (this.isCurrentMonthPaidFlag) return 0;
    if (this.selectedClientPreviousBalance !== 0 && this.isPreviousBalanceSelected) {
      return Math.abs(Number(this.selectedClientPreviousBalance));
    }
    return this.selectedClientBalance < 0 ? Math.abs(this.selectedClientBalance) : this.selectedClientRentAmount;
  }

  calculateAdvancePayment(): void {
    const suggestedAmount = this.isCurrentMonthPaidFlag 
      ? 0 
      : (this.selectedClientBalance < 0 ? Math.abs(this.selectedClientBalance) : this.selectedClientRentAmount);

    // Si destildaron pago adelantado, volvemos al monto sugerido inteligente ($0 o deuda)
    if (!this.paymentDto.isAdvancePayment || !this.paymentDto.advanceMonths) {
      this.paymentDto.amount = suggestedAmount;
      this.onAmountChange(this.paymentDto.amount);
      return;
    }

    const breakdown = this.buildPaymentMonthBreakdown();
    const totalToPay = breakdown.reduce((sum, item) => sum + item.amount, 0);
    this.paymentMonthBreakdown = breakdown;

    this.paymentDto.amount = totalToPay;
    this.onAmountChange(totalToPay);
    this.syncPaymentPreview();
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
    this.syncPaymentPreview();
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

  get filteredClients(): Client[] {
    const term = this.searchClient?.toLowerCase().trim();
    if (!term) return this.clients;
    const termForId = term.replace(',', '.');

    return this.clients.filter(c => {
      const fullName = c.fullName.toLowerCase();
      const identifier = (c.paymentIdentifier ?? '').toString().toLowerCase().replace(',', '.');
      
      return fullName.includes(term) || identifier.includes(termForId);
    });
  }

  private updateAdvanceConcept() {
    this.isPreviousBalanceSelected = false;
    this.syncPaymentPreview();
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

  deletePayment(p: DetailedPaymentView): void {
      const isGroup = p.isGrouped;
      Swal.fire({
        title: isGroup ? 'Â¿Eliminar transacciÃ³n completa?' : 'Â¿Eliminar movimiento?',
        text: isGroup 
           ? 'Se borrarÃ¡ el pago principal y su bonificaciÃ³n/recargo asociado. Esta acciÃ³n no se puede deshacer.'
           : 'Esta acciÃ³n borrarÃ¡ este registro contable. No se puede deshacer.',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#d33', 
        cancelButtonColor: '#9ca3af', 
        confirmButtonText: 'SÃ­, eliminar',
        cancelButtonText: 'Cancelar'
      }).then((result) => {
        if (result.isConfirmed) {
          this.paymentService.deletePayment(p.movementId).subscribe({
            next: () => {
              Swal.fire({ title: 'Â¡Eliminado!', text: 'El registro ha sido borrado correctamente.', icon: 'success', confirmButtonColor: '#2563eb' });
              this.loadPayments(); 
              this.loadClients();
            },
            error: (err) => {
              console.error('Error al eliminar pago:', err);
              Swal.fire({ title: 'Error', text: 'Hubo un problema al intentar borrar el registro.', icon: 'error', confirmButtonColor: '#2563eb' });
            }
          });
        }
      });
  }

  blurInput(event: Event): void {
    (event.target as HTMLElement).blur();
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

      if (this.selectedClientPreviousBalance !== 0 && (newAmount === Math.abs(Number(this.selectedClientPreviousBalance)) || this.newAmount === Math.abs(Number(this.selectedClientPreviousBalance))) && newAmount > 0) {
          this.isPreviousBalanceSelected = true;
          this.paymentDto.concept = 'Pago de saldo anterior';
      } else if (this.isPreviousBalanceSelected && newAmount !== Math.abs(Number(this.selectedClientPreviousBalance)) && this.newAmount !== Math.abs(Number(this.selectedClientPreviousBalance))) {
          this.isPreviousBalanceSelected = false;
          this.syncPaymentPreview();
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

      if (this.selectedClientPreviousBalance < 0 && debtCancelled === Math.abs(Number(this.selectedClientPreviousBalance)) && debtCancelled > 0) {
          this.isPreviousBalanceSelected = true;
          this.paymentDto.concept = 'Pago de saldo anterior';
      } else if (this.isPreviousBalanceSelected && debtCancelled !== Math.abs(Number(this.selectedClientPreviousBalance))) {
          this.isPreviousBalanceSelected = false;
          this.syncPaymentPreview();
      }
  }

  isPaymentMethodDifferent(): boolean {
    return Number(this.paymentDto?.paymentMethodId ?? 0) !== Number(this.selectedPreferredPaymentId ?? 0);
  }

  usePreviousBalance(): void {
    if (this.selectedClientPreviousBalance < 0) {
      this.isPreviousBalanceSelected = true;
      this.paymentDto.amount = Math.abs(Number(this.selectedClientPreviousBalance));
      this.paymentDto.concept = 'Pago de saldo anterior';
      this.onAmountChange(this.paymentDto.amount);
    }
  }

  getAbsolutePreviousBalance(): number {
    return Math.abs(Number(this.selectedClientPreviousBalance ?? 0));
  }

  getTotalDebtAmount(): number {
    return this.isCurrentMonthPaidFlag 
        ? (this.selectedClientBalance < 0 ? Math.abs(this.selectedClientBalance) : 0)
        : (this.selectedClientBalance < 0 ? Math.abs(this.selectedClientBalance) : this.selectedClientRentAmount);
  }

  useTotalDebt(): void {
    const totalDebt = this.getTotalDebtAmount();
    if (totalDebt > 0) {
      this.isPreviousBalanceSelected = false;
      this.paymentDto.amount = totalDebt;
      this.onAmountChange(this.paymentDto.amount);
      if (this.paymentDto.isAdvancePayment) {
        this.updateAdvanceConcept();
      } else {
        this.syncPaymentPreview();
      }
    }
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

  // =========================================================
  // --- LÃ“GICA DEL CEREBRO DE AUMENTOS ---
  // =========================================================
  
  private getSelectedPaymentMonth(): { year: number; month: number } {
    if (this.manualDateEnabled && this.dateString) {
      const [year, month] = this.dateString.split('-').map(Number);
      return { year, month };
    }

    const baseDate = this.paymentDto.date ? new Date(this.paymentDto.date) : new Date();
    return { year: baseDate.getFullYear(), month: baseDate.getMonth() + 1 };
  }

  private getCoverageStartMonth(): { year: number; month: number } {
    // 1. Si no hay historial (cliente ultra nuevo), usamos la fecha base.
    if (!this.selectedClientLastMonth) {
      return this.getSelectedPaymentMonth();
    }

    // 2. Desarmamos el Ãºltimo mes generado por el backend (Ej: "05/2026")
    const [mStr, yStr] = this.selectedClientLastMonth.split('/');
    let lastMonth = parseInt(mStr, 10);
    let lastYear = parseInt(yStr, 10);

    // 3. Si el balance es >= 0, el Ãºltimo mes que figura en la base de datos ya no tiene deuda.
    // En ese caso, tomamos como base el mes del pago seleccionado para no correr un mes de mÃ¡s.
    if (this.selectedClientBalance >= 0) {
        return this.getSelectedPaymentMonth();
    } else {
        // Si DEBE PLATA (balance < 0), la plata que traiga ahora se va a aplicar 
        // a ese mismo Ãºltimo mes para tapar el agujero de deuda.
        return { year: lastYear, month: lastMonth };
    }
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

  checkIncreaseLogic() {
    this.increaseQueue = [];
    this.confirmedIncreases = [];
    this.hasIncreaseInPeriod = false;

    if (this.isPreviousBalanceSelected) return;

    if (!this.selectedClientIncreaseAnchorDate) return;

    const anchorString = this.selectedClientIncreaseAnchorDate.split('T')[0];
    const [aYear, aMonth] = anchorString.split('-').map(Number);
    let currentAnchorValue = aYear * 100 + aMonth;

    const coverageStart = this.getCoverageStartMonth();
    const monthsToCover = this.paymentDto.isAdvancePayment && this.paymentDto.advanceMonths ? Math.max(1, this.paymentDto.advanceMonths) : 1;

    let tempBaseRent = this.selectedClientRentAmount;
    this.originalBaseRentCopy = this.selectedClientRentAmount;

    // 1. RECORREMOS LOS MESES
    for (let i = 0; i < monthsToCover; i++) {
      const month = this.addMonths(coverageStart.year, coverageStart.month, i);
      const monthValue = month.year * 100 + month.month;

      if (monthValue >= currentAnchorValue) {
        this.hasIncreaseInPeriod = true;
        this.increaseQueue.push({
          year: month.year,
          month: month.month,
          dateValue: monthValue,
          label: this.formatMonthLabel(month.year, month.month),
          baseRent: tempBaseRent,
          isOnlyDebit: false 
        });

        const frequencyStep = (this.selectedClientFrequency || 4) - 1; 
        const advanced = this.addMonths(Math.floor(currentAnchorValue / 100), currentAnchorValue % 100, frequencyStep);
        currentAnchorValue = advanced.year * 100 + advanced.month;
      }
    }

    // 2. VERIFICAMOS SI EL PRÓXIMO MES A DEBITAR TIENE AUMENTO PROGRAMADO
    const nextDebitMonth = this.addMonths(coverageStart.year, coverageStart.month, monthsToCover);
    const nextDebitMonthValue = nextDebitMonth.year * 100 + nextDebitMonth.month;
    if (nextDebitMonthValue >= currentAnchorValue) {
      this.hasIncreaseInPeriod = true;
      this.increaseQueue.push({
        year: nextDebitMonth.year,
        month: nextDebitMonth.month,
        dateValue: nextDebitMonthValue,
        label: this.formatMonthLabel(nextDebitMonth.year, nextDebitMonth.month),
        baseRent: tempBaseRent,
        isOnlyDebit: true 
      });
    }

    this.currentIncreaseStep = 0;
  }

  savePaymentModal(dto: CreatePaymentDTO) {
    if (!this.paymentMethods?.length) { Swal.fire({ icon: 'warning', title: 'Cargando mÃ©todos', text: 'EsperÃ¡ a que carguen.' }); return; }
    if (!this.paymentDto.clientId || this.paymentDto.clientId <= 0) { Swal.fire({ icon: 'warning', title: 'Cliente requerido', text: 'SeleccionÃ¡ un cliente.' }); return; }
    if (!dto.amount || dto.amount <= 0) { Swal.fire({ icon: 'warning', title: 'Monto invÃ¡lido', text: 'IngresÃ¡ un monto vÃ¡lido.' }); return; }
    if (!dto.paymentMethodId) { Swal.fire({ icon: 'warning', title: 'MÃ©todo requerido', text: 'SeleccionÃ¡ mÃ©todo.' }); return; }
    if (dto.isAdvancePayment && (!dto.advanceMonths || dto.advanceMonths < 1)) { Swal.fire({ icon: 'warning', title: 'Meses invÃ¡lidos', text: 'MÃ­nimo 1 mes.' }); return; }

    if (this.manualDateEnabled && this.dateString) {
      const [year, month, day] = this.dateString.split('-').map(Number);
      const currentTime = new Date();
      this.paymentDto.date = new Date(year, month - 1, day, currentTime.getHours(), currentTime.getMinutes(), currentTime.getSeconds());
    }

    this.checkIncreaseLogic();
    const isPriceLocked = this.paymentDto.isAdvancePayment && this.paymentDto.advanceMonths && this.paymentDto.advanceMonths >= 6;

    if (this.hasIncreaseInPeriod && !isPriceLocked && !this.increaseResolved) {
      this.currentIncreaseFlow = 'advance';
      this.processNextIncrease();
    } else {
      this.currentIncreaseFlow = 'none';
      this.showSummarySwal();
    }
  }

  processNextIncrease() {
    // Verificamos si todavía hay etapas en la cola según nuestro índice
    if (this.currentIncreaseStep < this.increaseQueue.length) {
      this.currentPendingIncrease = this.increaseQueue[this.currentIncreaseStep];
      
      if (this.currentPendingIncrease.isOnlyDebit) {
        this.increasePromptReason = `Al registrar este pago, el sistema debitará el mes de ${this.currentPendingIncrease.label} como deuda pendiente, al cual le corresponde una actualización de abono.`;
      } else {
        this.increasePromptReason = `Dentro de los meses que está pagando, el cliente tiene programado un aumento para ${this.currentPendingIncrease.label}.`;
      }
      
      this.selectedClientRentAmount = this.currentPendingIncrease.baseRent;

      // Si el usuario navegó hacia atrás y ya había puesto datos, los recuperamos.
      if (this.currentPendingIncrease.percentage !== undefined) {
        this.increasePercentage = this.currentPendingIncrease.percentage;
        this.projectedNewRent = this.currentPendingIncrease.newRentAmount;
      } else {
        this.increasePercentage = 0;
        this.projectedNewRent = this.selectedClientRentAmount;
        this.calculateProjectedRent(); 
      }
      
      this.showIncreaseOverlay = true;
    } else {
      // SI TERMINARON TODAS LAS ETAPAS
      this.increaseResolved = true;
      this.showIncreaseOverlay = false;
      
      // Mapeamos el resultado final para enviárselo a C#
      this.paymentDto.appliedIncreases = this.increaseQueue.map(item => ({
        year: item.year,
        month: item.month,
        percentage: item.percentage,
        newRentAmount: item.newRentAmount
      }));

      const coverageStart = this.getCoverageStartMonth();
      if (this.paymentDto.isAdvancePayment) {
        this.calculateAdvancePayment();
      } else {
        const currentInc = this.paymentDto.appliedIncreases.find(inc => inc.year === coverageStart.year && inc.month === coverageStart.month);
        if (currentInc) {
          this.paymentDto.amount = currentInc.newRentAmount;
          this.onAmountChange(this.paymentDto.amount);
          this.syncPaymentPreview();
        }
      }
      this.showSummarySwal();
      this.selectedClientRentAmount = this.originalBaseRentCopy; 
    }
  }

  // --- NUEVA LÓGICA DE REDONDEO INTELIGENTE ---
  private roundRentAmount(targetAmount: number, methodName: string, originalRent: number, targetPercentage: number): number {
    if (targetAmount === 0) return 0;
    
    // FIX CRÍTICO: Si este mes no lleva aumento, NO aplicamos redondeos.
    if (targetPercentage <= 0) return targetAmount;
    
    const methodLower = methodName.toLowerCase();
    const isEfectivo = methodLower.includes('efectivo');
    const step = isEfectivo ? 1000 : 100; // 1000 para efectivo, 100 para banco

    let rounded = Math.ceil(targetAmount / step) * step;

    let currentPercentage = ((rounded - originalRent) / originalRent) * 100;

    while (currentPercentage < targetPercentage) {
      rounded += step;
      currentPercentage = ((rounded - originalRent) / originalRent) * 100;
    }

    return rounded;
  }

  calculateProjectedRent() {
    const rent = this.selectedClientRentAmount || 0;
    const perc = this.increasePercentage || 0;
    let newRent = rent + (rent * (perc / 100));

    const methodName = this.getNamePaymentMethodById(this.selectedPreferredPaymentId).toLowerCase();
    
    // Aplicamos redondeo segÃºn el mÃ©todo
    newRent = this.roundRentAmount(newRent, methodName, rent, perc);

    // RE-CALCULAMOS el porcentaje exacto tras el redondeo usando 4 decimales
    if (rent > 0) {
      const exactPerc = ((newRent - rent) / rent) * 100;
      this.increasePercentage = parseFloat(exactPerc.toFixed(4)); 
    }

    this.projectedNewRent = newRent;
    this.updateProjectedDate();
  }

  calculatePercentageFromRent() {
    const rent = this.selectedClientRentAmount || 0;
    
    if (rent === 0) {
      this.increasePercentage = 0;
    } else {
      let newRent = this.projectedNewRent || 0;
      let perc = ((newRent - rent) / rent) * 100;
      this.increasePercentage = parseFloat(perc.toFixed(4));
    }
    
    this.updateProjectedDate();
  }

  // --- ESCENARIO A: Al terminar de editar el PORCENTAJE ---
  onIncreasePercentageBlur() {
  const rent = this.selectedClientRentAmount || 0;
  const perc = this.increasePercentage || 0; // El % que escribiÃ³ el usuario
  
  let targetRent = rent + (rent * (perc / 100));
  const methodName = this.getNamePaymentMethodById(this.selectedPreferredPaymentId).toLowerCase();
  
  // AquÃ­ pasamos el 'perc' para que la funciÃ³n sepa quÃ© cumplir
  this.projectedNewRent = this.roundRentAmount(targetRent, methodName, rent, perc);
    
  // Recalculamos el % real final tras el redondeo forzado
  if (rent > 0) {
    this.increasePercentage = parseFloat((((this.projectedNewRent - rent) / rent) * 100).toFixed(4));
  }
}

  // --- ESCENARIO B: Al terminar de editar el MONTO ($) ---
  onProjectedRentBlur() {
    const rent = this.selectedClientRentAmount || 0;
    let targetRent = this.projectedNewRent || 0;

    const methodName = this.getNamePaymentMethodById(this.selectedPreferredPaymentId).toLowerCase();
    
    // Aplicamos redondeo a lo que escribiÃ³ el usuario segÃºn el mÃ©todo
    targetRent = this.roundRentAmount(targetRent, methodName, rent, this.increasePercentage);
    this.projectedNewRent = targetRent; 
    
    if (rent === 0) {
      this.increasePercentage = 0;
    } else {
      const perc = ((targetRent - rent) / rent) * 100;
      this.increasePercentage = parseFloat(perc.toFixed(4));
    }
    
    this.updateProjectedDate();
  }

  confirmIncrease() {
    if (this.currentPendingIncrease) {
      // Guardamos la decisión PROVISORIAMENTE en este paso de la cola
      this.currentPendingIncrease.percentage = this.increasePercentage;
      this.currentPendingIncrease.newRentAmount = this.projectedNewRent;

      // Si hay un paso siguiente, le dejamos preparado el precio base para que no empiece de cero
      if (this.currentIncreaseStep + 1 < this.increaseQueue.length) {
        this.increaseQueue[this.currentIncreaseStep + 1].baseRent = this.projectedNewRent;
      }
    }

    // Avanzamos al siguiente paso
    this.currentIncreaseStep++;
    this.processNextIncrease();
  }

  skipIncrease() {
    this.increaseQueue = []; // Vaciamos la cola para que no pregunte más
    this.confirmedIncreases = [];
    this.paymentDto.appliedIncreases = [];
    this.paymentDto.skipFutureProjection = true;
    
    this.increaseResolved = true;
    this.showIncreaseOverlay = false;
    this.selectedClientRentAmount = this.originalBaseRentCopy; // Restauramos
    
    if (this.currentIncreaseFlow === 'advance') {
      this.calculateAdvancePayment();
      this.showSummarySwal();
    } else {
      this.executeBackendCall();
    }
  }

  showSummarySwal() {
    const formatARS = (value: number) => {
      return new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
    };

    const scenario = this.getSurchargeScenario();
    const interestAmt = this.calculateInterestAmount();
    const isImmediate = (scenario === 'B' && this.selectedSurchargeAction === 'immediate') ||
                        (scenario === 'C' && this.applyScenarioCInterest && this.selectedSurchargeAction === 'immediate');

    let breakdown = [...this.getSummaryBreakdown()];
    if (isImmediate && interestAmt > 0) {
      breakdown.unshift({
        year: new Date().getFullYear(),
        month: new Date().getMonth() + 1,
        amount: interestAmt,
        label: 'Interés por mora (cobrado en el acto)'
      });
    }

    if (this.paymentDto.isAdvancePayment && (!this.paymentDto.amount || this.paymentDto.amount <= 0)) {
        this.paymentDto.amount = breakdown.reduce((sum, item) => sum + item.amount, 0);
    }

    let breakdownHtml = breakdown.map((item, i) => `
      <div class="flex justify-between items-center py-2.5 border-b border-gray-100 last:border-0 last:pb-0">
        <div class="flex flex-col text-left">
            <span class="text-sm font-bold text-gray-800">${item.label}</span>
            <span class="text-[11px] text-gray-500 font-medium">
                Valor del mes: ${formatARS(item.amount)}
            </span>
        </div>
        <div class="text-right">
            <span class="text-[10px] text-indigo-600 font-bold uppercase tracking-wider block leading-none mb-1">Total Saldado</span>
            <span id="swal-saldado-${i}" class="font-black text-base tabular-nums transition-colors duration-200">$0.00</span>
        </div>
      </div>
    `).join('');

    const calcObj = this.getCalculatedAmounts(this.paymentDto.amount, this.paymentDto.paymentMethodId, this.selectedPreferredPaymentId);
    let commissionHtml = '';
    if (calcObj.isSurcharge) {
        commissionHtml = `<div class="flex justify-between items-center text-sm text-orange-700 font-semibold mt-3 pt-3 border-t border-gray-200"><span>Recargo por método de pago</span><span class="font-bold">- ${formatARS(calcObj.difference)}</span></div>`;
    }

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
            <p class="text-xs text-emerald-700 mt-0.5">El recargo pendiente de <span class="font-bold">${formatARS(this.selectedPendingSurcharge)}</span> será eliminado automáticamente porque el pago se registra dentro del plazo (día 10 o antes).</p>
          </div>
          <span class="bg-emerald-100/90 text-emerald-800 border border-emerald-300 px-2.5 py-1 rounded-lg text-[10px] font-semibold shrink-0">Día ≤ 10</span>
        </div>`;
    } else if (scenario === 'B') {
      surchargeBannerHtml = `
        <div class="bg-amber-50/90 border border-amber-200/80 p-4 rounded-xl mb-4 text-left shadow-2xs space-y-3">
          <div class="flex items-start justify-between gap-3 border-b border-amber-200/60 pb-2.5">
            <div>
              <h4 class="text-xs font-bold text-amber-900 uppercase tracking-wider flex items-center gap-2">
                <span>Recargo por mora pendiente (${formatARS(this.selectedPendingSurcharge)})</span>
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
      const baseImp = (this.selectedInterestAmount || 0) + (this.selectedClientRentAmount || 0);
      surchargeBannerHtml = `
        <div class="bg-amber-50/90 border border-amber-200/80 p-4 rounded-xl mb-4 text-left shadow-2xs space-y-3">
          <div class="flex items-start justify-between gap-3 border-b border-amber-200/60 pb-2.5">
            <div>
              <h4 class="text-xs font-bold text-amber-900 uppercase tracking-wider flex items-center gap-2">
                <span>Aplicar intereses por mora (${formatARS(interestAmt)})</span>
                <button type="button" id="swal-edit-interest-btn-c" class="inline-flex items-center gap-1 px-2 py-0.5 rounded bg-white border border-amber-300 text-[10px] font-bold text-amber-800 hover:bg-amber-100 transition-colors shadow-2xs">Modificar monto</button>
              </h4>
              <p class="text-xs text-amber-700 mt-0.5">Fecha posterior al día 10. Cálculo: 10% de base imponible (${formatARS(baseImp)}).</p>
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
              ${isImmediate ? 'Modificá este valor si el cliente entregó una suma diferente; la deuda neta y la cascada de abonos se recalculan automáticamente.' : 'Modificá este valor si el cliente entregó una suma diferente; la cascada de abonos se recalcula automáticamente.'}
            </p>
          </div>
        `;
    }

    const clientName = this.selectedClientName || 'Cliente';
    const clientNumber = this.selectedClientIdentifier || '';
    const methodName = this.getNamePaymentMethodById(this.paymentDto.paymentMethodId);

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
          </div>

          ${warningBannerHtml}
          ${surchargeBannerHtml}
          ${moneyBoxesHtml}

          <!-- Tarjetas KPI (Dinero Cobrado vs Deuda Neta Cancelada) -->
          <div class="grid grid-cols-2 gap-3">
            <div class="p-4 rounded-xl bg-slate-50 border border-slate-200/80 shadow-2xs">
              <span class="text-[10px] font-bold text-slate-500 uppercase tracking-wider block mb-1">Dinero Cobrado</span>
              <span id="swal-cobrado-display" class="text-xl font-black text-slate-900 tabular-nums">${formatARS(calcObj.amountEntered + (isImmediate ? interestAmt : 0))}</span>
            </div>
            <div class="p-4 rounded-xl bg-gradient-to-r from-blue-50 to-indigo-50 border border-blue-200/80 shadow-2xs">
              <span class="text-[10px] font-bold text-blue-600 uppercase tracking-wider block mb-1">Deuda Neta Cancelada</span>
              <span id="swal-debt-display" class="text-xl font-black text-blue-800 tabular-nums">$0.00</span>
            </div>
          </div>

          <!-- Cascada de Pagos -->
          <div class="p-4 rounded-xl border border-gray-200/80 bg-white shadow-2xs">
            <div class="text-[11px] font-bold text-gray-400 mb-3 uppercase tracking-wider border-b border-gray-100 pb-2 flex items-center justify-between">
              <span>Desglose por período</span>
              <span>Acreditación</span>
            </div>
            <div class="space-y-2">
              ${breakdownHtml}
            </div>
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

          // --- FUNCIÓN DE CASCADA EN TIEMPO REAL ---
          const updateCascada = (rawInputValue: number) => {
              const calc = this.getCalculatedAmounts(rawInputValue, this.paymentDto.paymentMethodId, this.selectedPreferredPaymentId);

              // Cuando el interés se cobra en el acto (isImmediate), el primer ítem del breakdown
              // ES la fila de interés. Esa fila ya está cubierta por el recargo cobrado, por lo
              // que se muestra siempre como saldada en su totalidad (interestAmt). El dinero base
              // (equivalentDebtPaid) se distribuye SÓLO sobre las filas de meses de alquiler.
              let startIndex = 0;
              if (isImmediate && interestAmt > 0 && breakdown.length > 0) {
                  const el0 = document.getElementById('swal-saldado-0');
                  if (el0) {
                      el0.innerText = formatARS(interestAmt);
                      el0.classList.add('text-green-600');
                      el0.classList.remove('text-orange-500', 'text-gray-400');
                  }
                  startIndex = 1;
              }

              let moneyRemaining = calc.equivalentDebtPaid;

              // Actualizamos cada mes de alquiler (omitiendo la fila de interés ya saldada)
              breakdown.forEach((item, i) => {
                  if (i < startIndex) return; // ya renderizada arriba
                  let applied = Math.min(moneyRemaining, item.amount);
                  moneyRemaining -= applied;
                  moneyRemaining = Math.max(0, moneyRemaining);

                  const el = document.getElementById(`swal-saldado-${i}`);
                  if (el) {
                      el.innerText = formatARS(applied);
                      
                      // Cambio de color visual si queda debiendo o salda completo
                      if (applied < item.amount) {
                          el.classList.add('text-orange-500');
                          el.classList.remove('text-green-600', 'text-gray-400');
                      } else if (applied === 0) {
                          el.classList.add('text-gray-400');
                          el.classList.remove('text-green-600', 'text-orange-500');
                      } else {
                          el.classList.add('text-green-600');
                          el.classList.remove('text-orange-500', 'text-gray-400');
                      }
                  }
              });

              // Actualizamos los totales en tiempo real
              const cobradoDisplay = document.getElementById('swal-cobrado-display');
              if (cobradoDisplay) cobradoDisplay.innerText = formatARS(calc.amountEntered + (isImmediate ? interestAmt : 0));
              const debtDisplay = document.getElementById('swal-debt-display');
              if (debtDisplay) debtDisplay.innerText = formatARS(calc.equivalentDebtPaid + (isImmediate ? interestAmt : 0));
          };

          // Inicializamos la vista
          updateCascada(this.paymentDto.amount);

          // Escuchamos los cambios del usuario si existe el input
          const input = document.getElementById('swal-custom-amount') as HTMLInputElement;
          if (input) {
              this.attachCurrencyFormatListener(input, (newTotalValue: number) => {
                  const baseVal = Math.max(0, newTotalValue - (isImmediate ? interestAmt : 0));
                  updateCascada(baseVal);
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
        this.onAmountChange(this.paymentDto.amount);
        this.executeBackendCall();
      } else if ((result.dismiss as any) === Swal.DismissReason.cancel || result.dismiss === 'cancel') {
        // Solo limpiamos el estado si el usuario presionó CANCELAR explícitamente.
        // Si el modal se cerró programáticamente (Swal.close()) por cambio de radio/checkbox,
        // NO debemos limpiar los aumentos ya confirmados ni la cola.
        this.paymentDto.appliedIncreases = [];
        this.increaseQueue = [];
        this.confirmedIncreases = [];
        this.paymentDto.skipFutureProjection = false;
        this.increaseResolved = false;
        this.selectedClientRentAmount = this.originalBaseRentCopy;
        
        if(this.paymentDto.isAdvancePayment) this.calculateAdvancePayment();
      }
    });
  }

  executeBackendCall() {
    const calc = this.getCalculatedAmounts(this.paymentDto.amount, this.paymentDto.paymentMethodId, this.selectedPreferredPaymentId);
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

    const targetReturnUrl = this.returnToUrl;
    this.returnToUrl = null;

    this.paymentService.CreatePayment(payloadToSave).subscribe({
      next: () => {
        Swal.fire({
          title: '¡Pago registrado!',
          text: 'El pago se registró correctamente. ¿Deseas generar el recibo de pago?',
          icon: 'success',
          showCancelButton: true,
          confirmButtonColor: '#2563eb',
          cancelButtonColor: '#6B7280',
          confirmButtonText: 'Sí, generar recibo',
          cancelButtonText: 'No, cerrar'
        }).then((result) => {
          this.closeClientModal();
          setTimeout(() => {
            this.loadClients();
            this.loadPayments();
          }, 100);

          if (result.isConfirmed) {
            this.pendingReturnUrl = targetReturnUrl;
            this.openReceiptModalFromNewPayment({
              clientName: this.getClientNameById(payloadToSave.clientId),
              paymentIdentifier: this.getClientIdentifierById(payloadToSave.clientId),
              amount: payloadToSave.amount,
              paymentDate: payloadToSave.date,
              concept: payloadToSave.concept || 'SERVICIO DE BAULERAS'
            });
          } else {
            if (targetReturnUrl) {
              this.router.navigate(['/' + targetReturnUrl]);
            }
          }
        });
      },
      error: (err) => {
        console.error('Error al guardar payment:', err);
        Swal.fire({ title: 'Error', text: 'Hubo un problema al registrar la transacción en la base de datos.', icon: 'error', confirmButtonColor: '#2563eb' });
      }
    });
  }

  openReceiptModal(item: DetailedPaymentView) {
    this.openReceiptModalFromNewPayment({
      clientName: item.clientName ?? '',
      paymentIdentifier: String(item.paymentIdentifier),
      amount: item.amount,
      paymentDate: new Date(item.paymentDate),
      concept: item.concept ?? 'SERVICIO DE BAULERAS'
    });
  }

  recalculateTotalAmount() {
    this.receiptTotalAmountCustom = this.receiptConcepts.reduce((acc, curr) => acc + (Number(curr.amount) || 0), 0);
  }

  openReceiptModalFromNewPayment(info: { clientName: string, paymentIdentifier: string, amount: number, paymentDate: Date, concept: string }) {
    this.receiptPaymentInfo = {
      clientName: info.clientName,
      paymentIdentifier: info.paymentIdentifier,
      amount: info.amount,
      paymentDate: info.paymentDate
    };

    const dt = new Date(info.paymentDate);
    const y = dt.getFullYear();
    const m = (dt.getMonth() + 1).toString().padStart(2, '0');
    const d = dt.getDate().toString().padStart(2, '0');
    this.receiptDateStr = `${y}-${m}-${d}`;

    this.receiptConcepts = [
      { description: info.concept || 'SERVICIO DE BAULERAS', amount: info.amount }
    ];
    this.recalculateTotalAmount();
    this.showReceiptModal = true;
  }

  closeReceiptModal() {
    this.showReceiptModal = false;
    this.receiptPaymentInfo = null;
    this.receiptConcepts = [];
    this.receiptDateStr = '';
    
    if (this.pendingReturnUrl) {
      const url = this.pendingReturnUrl;
      this.pendingReturnUrl = null;
      this.router.navigate(['/' + url]);
    }
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

  goBackFromIncrease() {
    if (this.currentIncreaseStep > 0) {
      // Si estamos en la Etapa 2 o superior, retrocedemos una etapa
      this.currentIncreaseStep--;
      this.processNextIncrease();
    } else {
      // Si estamos en la Etapa 1, cerramos el modal cancelando el flujo
      this.showIncreaseOverlay = false;
      this.currentIncreaseFlow = 'none';
      this.increaseResolved = false;
      this.selectedClientRentAmount = this.originalBaseRentCopy;
    }
  }
}

