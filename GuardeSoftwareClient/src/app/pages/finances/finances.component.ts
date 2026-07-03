import { Component, OnInit } from '@angular/core';
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
    private route: ActivatedRoute, // NUEVO
    private router: Router         // NUEVO
  ){}

  clients: Client[] = [];
  showClientModal = false;
  selectedClientId: number | 0 = 0;
  selectedClientName: string = '';
  selectedClientIdentifier: number | 0 = 0;
  selectedClientBalance: number | 0 = 0;
  selectedClientRentAmount: number | 0 = 0;
  selectedPreferredPaymentId: number = 1;
  

  pendingRentals: PendingRentalDTO[] = [];
  payments: DetailedPaymentView[] = [];
  paymentMethods: PaymentMethod[] = [];

  filteredPayments: DetailedPaymentView[] = [];
  searchPayment: string = '';

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
  public selectedPendingSurcharge: number = 0;
  public selectedClientLastMonth: string = '';
  public paymentMonthBreakdown: PaymentMonthBreakdown[] = [];
  public selectedClientNextPaymentDay: Date | string | null = null;
  public selectedClientNextIncreaseDay: Date | string | null = null;

  returnToUrl: string | null = null;

  autoOpenClientId: number | null = null;

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

  loadPayments(): void {
    this.paymentService.getDetailedPayment().subscribe({
      next: (data) => {
        const sorted = data.sort((a, b) => {
          const dateA = new Date(a.paymentDate).getTime();
          const dateB = new Date(b.paymentDate).getTime();
          return dateB - dateA; 
        });
        this.payments = sorted as DetailedPaymentView[];
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

    filtered.sort((a, b) => new Date(b.paymentDate).getTime() - new Date(a.paymentDate).getTime());
    
    this.filteredPayments = filtered;
    this.processGroups();
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

  closeClientModal() {
    this.showClientModal = false;
    this.searchClient = '';
    this.selectedClientId = 0;
    this.selectedClientIdentifier = 0;
    this.selectedClientName = '';
    this.selectedClientBalance = 0;
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
    this.selectedClientRentAmount = Number(client.currentRent ?? 0);
    this.selectedClientIncreaseAnchorDate = client.increaseAnchorDate;
    this.selectedClientNextPaymentDay = client.nextPaymentDay ?? null;
    this.selectedClientNextIncreaseDay = client.nextIncreaseDay ?? client.increaseAnchorDate ?? null;
    this.selectedPreferredPaymentId = Number(client.preferredPaymentMethodId ?? 1); 
    this.paymentDto.paymentMethodId = this.selectedPreferredPaymentId;
    this.selectedPendingSurcharge = Number(client.pendingSurcharge ?? 0);
    
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

    const suggestedAmount = this.isCurrentMonthPaidFlag 
        ? 0 
        : (this.selectedClientBalance < 0 ? Math.abs(this.selectedClientBalance) : this.selectedClientRentAmount);

    this.paymentDto.amount = suggestedAmount;

    this.checkIncreaseLogic();
    this.syncPaymentPreview();
    this.onAmountChange(this.paymentDto.amount);
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

    // 2. REVISAMOS EL MES SIGUIENTE (DEUDA FUTURA)
    const isPriceLocked = this.paymentDto.isAdvancePayment && this.paymentDto.advanceMonths && this.paymentDto.advanceMonths >= 6;
    if (!isPriceLocked) {
      const nextMonth = this.addMonths(coverageStart.year, coverageStart.month, monthsToCover);
      const nextMonthValue = nextMonth.year * 100 + nextMonth.month;

      if (nextMonthValue >= currentAnchorValue) {
        this.hasIncreaseInPeriod = true; 
        this.increaseQueue.push({
          year: nextMonth.year,
          month: nextMonth.month,
          dateValue: nextMonthValue,
          label: this.formatMonthLabel(nextMonth.year, nextMonth.month),
          baseRent: tempBaseRent,
          isOnlyDebit: true 
        });
      }
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
    const breakdown = this.getSummaryBreakdown();
    
    if (this.paymentDto.isAdvancePayment) {
        // Precargamos la sumatoria perfecta
        this.paymentDto.amount = breakdown.reduce((sum, item) => sum + item.amount, 0);
    }

    const formatARS = (value: number) => {
      return new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
    };

    let breakdownHtml = breakdown.map((item, i) => `
      <div class="flex justify-between items-center border-b border-gray-100 pb-2 mb-2 last:border-0 last:pb-0 last:mb-0">
        <div class="flex flex-col">
            <span class="font-bold text-gray-800">${item.label}</span>
            <span class="text-[10px] text-gray-500 uppercase tracking-wide font-bold">
                Abono del Mes: ${formatARS(item.amount)}
            </span>
        </div>
        <div class="text-right">
            <span class="text-[9px] text-indigo-400 font-bold uppercase tracking-wider block leading-tight">Total Saldado</span>
            <span id="swal-saldado-${i}" class="font-bold text-lg transition-colors duration-200">$0.00</span>
        </div>
      </div>
    `).join('');

    const calcObj = this.getCalculatedAmounts(this.paymentDto.amount, this.paymentDto.paymentMethodId, this.selectedPreferredPaymentId);
    let commissionHtml = '';
    if (calcObj.isSurcharge) {
        commissionHtml = `<div class="flex justify-between text-sm text-orange-600 mt-2 pt-2 border-t border-gray-200"><span>Recargo (Comisión)</span><span class="font-bold">- ${formatARS(calcObj.difference)}</span></div>`;
    }

    const isPotentiallyDuplicate = this.selectedClientBalance >= 0 && !this.paymentDto.isAdvancePayment;
    let warningBannerHtml = isPotentiallyDuplicate ? `
        <div class="bg-yellow-50 border-l-4 border-yellow-500 p-3 mb-4 rounded-r-md text-left">
            <h3 class="text-sm font-bold text-yellow-800">⚠️ Posible pago duplicado</h3>
            <p class="text-xs text-yellow-700 mt-1">El cliente está al día. Verificá no estar ingresando un pago repetido.</p>
        </div>` : '';

    let moneyBoxesHtml = '';
    if (this.paymentDto.isAdvancePayment) {
        moneyBoxesHtml = `
          <div class="bg-indigo-50 border border-indigo-200 p-3 rounded-lg mb-3">
            <label class="block text-[11px] font-bold text-indigo-800 uppercase tracking-wider mb-2">Dinero entregado por el cliente</label>
            <div class="relative">
              <span class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-600 font-bold">$</span>
              <input id="swal-custom-amount" type="number" step="0.01" value="${this.paymentDto.amount}" 
                     class="w-full pl-7 pr-3 py-2 rounded border border-indigo-300 font-bold text-xl text-gray-900 focus:outline-none focus:ring-2 focus:ring-indigo-500">
            </div>
            <p class="text-[10px] text-indigo-600 mt-1">Modificá este valor si el cliente entregó menos dinero. La cascada se recalculará sola.</p>
          </div>
        `;
    }

    Swal.fire({
      title: isPotentiallyDuplicate ? 'Revisar Transacción' : 'Resumen Final',
      icon: isPotentiallyDuplicate ? 'warning' : 'info',
      html: `
        <div class="text-left space-y-3">
          ${warningBannerHtml}
          
          ${moneyBoxesHtml}
          
          <div class="flex justify-between items-center p-3 rounded-lg bg-gray-50 border border-gray-200">
              <span class="text-sm text-gray-600 font-medium">Deuda Neta Cancelada:</span>
              <span id="swal-debt-display" class="text-xl font-bold text-blue-700">$0.00</span>
          </div>

          <div class="p-4 rounded-lg border border-gray-200 bg-white shadow-sm mt-3">
            <div class="text-xs font-bold text-gray-400 mb-3 uppercase tracking-wider border-b pb-2">Cascada de Pagos</div>
            <div class="space-y-1">
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
      customClass: { 
          confirmButton: 'bg-blue-600 text-white px-5 py-2.5 rounded-md mx-2 hover:bg-blue-700 font-bold shadow-md', 
          cancelButton: 'bg-gray-100 text-gray-600 px-5 py-2.5 rounded-md hover:bg-gray-200 font-bold', 
      },
      didOpen: () => {
          // --- FUNCIÓN DE CASCADA EN TIEMPO REAL ---
          const updateCascada = (rawInputValue: number) => {
              const calc = this.getCalculatedAmounts(rawInputValue, this.paymentDto.paymentMethodId, this.selectedPreferredPaymentId);
              let moneyRemaining = calc.equivalentDebtPaid;

              // Actualizamos cada mes
              breakdown.forEach((item, i) => {
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

              // Actualizamos el total general
              const debtDisplay = document.getElementById('swal-debt-display');
              if (debtDisplay) debtDisplay.innerText = formatARS(calc.equivalentDebtPaid);
          };

          // Inicializamos la vista
          updateCascada(this.paymentDto.amount);

          // Escuchamos los cambios del usuario si existe el input
          const input = document.getElementById('swal-custom-amount') as HTMLInputElement;
          if (input) {
              input.addEventListener('input', () => {
                  updateCascada(parseFloat(input.value) || 0);
              });
          }
      },
      preConfirm: () => {
          if (this.paymentDto.isAdvancePayment) {
              const input = document.getElementById('swal-custom-amount') as HTMLInputElement;
              if (input && input.value) return parseFloat(input.value);
          }
          return this.paymentDto.amount;
      }
    }).then((result) => {
      if (result.isConfirmed) {
        this.paymentDto.amount = result.value;
        this.onAmountChange(this.paymentDto.amount);
        this.executeBackendCall();
      } else {
        // FIX: Si el usuario presiona Cancelar, borramos el estado temporal de Angular
        // para que si reabre el modal, arranque completamente limpio.
        this.paymentDto.appliedIncreases = [];
        this.increaseQueue = [];
        this.confirmedIncreases = [];
        this.paymentDto.skipFutureProjection = false;
        this.selectedClientRentAmount = this.originalBaseRentCopy;
        
        // Opcional: recalcular base para limpiar UI subyacente
        if(this.paymentDto.isAdvancePayment) this.calculateAdvancePayment();
      }
    });
  }

  executeBackendCall() {
    const calc = this.getCalculatedAmounts(this.paymentDto.amount, this.paymentDto.paymentMethodId, this.selectedPreferredPaymentId);
        const localDate = this.paymentDto.date;
    const adjustedDate = new Date(localDate.getTime() - (localDate.getTimezoneOffset() * 60000));

    const payloadToSave: CreatePaymentDTO = {
      ...this.paymentDto,
      date: adjustedDate, 
      amount: calc.amountEntered, 
      commissionAmount: calc.isSurcharge ? calc.difference : (calc.isDiscount ? -calc.difference : 0),
      commissionConcept: calc.isSurcharge 
          ? `Recargo por pago en ${this.getNamePaymentMethodById(this.paymentDto.paymentMethodId)} (${calc.selectedCommission}%)`
          : (calc.isDiscount ? `BonificaciÃ³n por pago en ${this.getNamePaymentMethodById(this.paymentDto.paymentMethodId)} (${calc.selectedCommission}%)` : '')
    };

    this.paymentService.CreatePayment(payloadToSave).subscribe({
      next: () => {
        Swal.fire({ title: 'Pago registrado', text: 'El pago se registrÃ³ correctamente.', icon: 'success', confirmButtonColor: '#2563eb' });
        this.closeClientModal();
        setTimeout(() => {
          this.loadClients();
          this.loadPayments()
        }
          , 100); 
      },
      error: (err) => {
        console.error('Error al guardar payment:', err);
        Swal.fire({ title: 'Error', text: 'Hubo un problema al registrar la transacciÃ³n en la base de datos.', icon: 'error', confirmButtonColor: '#2563eb' });
      }
    });
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

