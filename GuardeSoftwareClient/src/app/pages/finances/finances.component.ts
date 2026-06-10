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

  resetFilters(): void {
    this.searchPayment = '';
    this.selectedMethodFilter = '';
    this.selectedMonth = '';
    this.filterPayments();
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
    const month = this.selectedMonth;

    let filtered = this.payments.filter(p => {
      const clientName = p.clientName?.toLowerCase() || '';
      const paymentIdentifier = (p.paymentIdentifier?.toString() || '').toLowerCase().replace(',', '.');
      const paymentMethodName = p.paymentMethodName?.toLowerCase() || '';

      const matchesSearch = clientName.includes(term) || paymentIdentifier.includes(termForId);
      const matchesMethod = !method || paymentMethodName === method;
      const matchesMonth = !month || new Date(p.paymentDate).toISOString().startsWith(month);

      return matchesSearch && matchesMethod && matchesMonth;
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
    const baseRent = Number(this.selectedClientRentAmount || 0);
    const currentDebt = this.selectedClientBalance < 0 ? Math.abs(Number(this.selectedClientBalance)) : 0;
    
    // FIX: Parseo correcto del AnchorValue a YYYYMM (Ej: 202608)
    let anchorValue = 0;
    if (this.selectedClientIncreaseAnchorDate) {
      const parts = this.selectedClientIncreaseAnchorDate.split('T')[0].split('-').map(Number);
      anchorValue = parts[0] * 100 + parts[1];
    }
    
    const methodName = this.getNamePaymentMethodById(this.paymentDto.paymentMethodId).toLowerCase();

    return Array.from({ length: monthsToCover }, (_, index) => {
      const month = this.addMonths(coverageStart.year, coverageStart.month, index);
      const monthComparable = this.toMonthComparable(month.year, month.month);
      const monthBaseAmount = index === 0 && currentDebt > 0 ? currentDebt : baseRent;

      let amount = monthBaseAmount;

      if (!(index === 0 && currentDebt > 0)) {
         // Buscamos si para este mes (o anteriores) existe un aumento ya confirmado en el array
         let activeIncrease = this.confirmedIncreases
             .filter(inc => (inc.year * 100 + inc.month) <= monthComparable)
             .sort((a,b) => (b.year*100+b.month) - (a.year*100+a.month))[0];

         if (activeIncrease) {
             amount = activeIncrease.newRentAmount; // Usamos el precio escalonado final
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
    this.increaseResolved = false;
    this.increasePercentage = 0;
    this.paymentDto.increasePercentage = 0;
    this.showIncreaseOverlay = false;
    this.currentIncreaseFlow = 'none';
    this.selectedClientLastMonth = client.lastGeneratedMonthYear || '';
    this.selectedClientFrequency = client.increaseFrequencyMonths || 4;
    
    // Sugerencia de saldo a cobrar
    const suggestedAmount = this.selectedClientBalance < 0 
        ? Math.abs(this.selectedClientBalance) 
        : this.selectedClientRentAmount;

    this.paymentDto.amount = suggestedAmount;

    this.checkIncreaseLogic();
    this.syncPaymentPreview();
    this.onAmountChange(this.paymentDto.amount);
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

    // 1. RECORREMOS LOS MESES QUE SE VAN A PAGAR
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
          isOnlyDebit: false // Este mes SÍ se está pagando ahora
        });

        const frequencyStep = (this.selectedClientFrequency || 4) - 1; 
        const advanced = this.addMonths(Math.floor(currentAnchorValue / 100), currentAnchorValue % 100, frequencyStep);
        currentAnchorValue = advanced.year * 100 + advanced.month;
      }
    }

    // 2. FIX CRÍTICO: REVISAMOS EL MES INMEDIATAMENTE SIGUIENTE (El que quedará debiendo)
    const isPriceLocked = this.paymentDto.isAdvancePayment && this.paymentDto.advanceMonths && this.paymentDto.advanceMonths >= 6;
    if (!isPriceLocked) {
      const nextMonth = this.addMonths(coverageStart.year, coverageStart.month, monthsToCover);
      const nextMonthValue = nextMonth.year * 100 + nextMonth.month;

      if (nextMonthValue >= currentAnchorValue) {
        this.hasIncreaseInPeriod = true; // Activamos los modales secuenciales
        this.increaseQueue.push({
          year: nextMonth.year,
          month: nextMonth.month,
          dateValue: nextMonthValue,
          label: this.formatMonthLabel(nextMonth.year, nextMonth.month),
          baseRent: tempBaseRent,
          isOnlyDebit: true // Flag clave: NO afecta el efectivo que pone hoy
        });
      }
    }
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
    if (this.increaseQueue.length > 0) {
      this.currentPendingIncrease = this.increaseQueue.shift();
      
      // Ajustamos el texto dinámicamente según si se paga o solo se debita en cuenta corriente
      if (this.currentPendingIncrease.isOnlyDebit) {
        this.increasePromptReason = `Al registrar este pago, el sistema debitará el mes de ${this.currentPendingIncrease.label} como deuda pendiente, al cual le corresponde una actualización de abono.`;
      } else {
        this.increasePromptReason = `Dentro de los meses que está pagando, el cliente tiene programado un aumento para ${this.currentPendingIncrease.label}.`;
      }
      
      this.selectedClientRentAmount = this.currentPendingIncrease.baseRent;
      this.increasePercentage = 0;
      this.projectedNewRent = this.selectedClientRentAmount;
      this.calculateProjectedRent(); 
      this.showIncreaseOverlay = true;
    } else {
      this.increaseResolved = true;
      this.showIncreaseOverlay = false;
      this.paymentDto.appliedIncreases = this.confirmedIncreases; 

      const coverageStart = this.getCoverageStartMonth();
      if (this.paymentDto.isAdvancePayment) {
        this.calculateAdvancePayment();
      } else {
        // Si es pago normal de 1 mes, solo modificamos el monto a cobrar si el aumento afectaba a este mes específico
        const hasCurrentMonthIncrease = this.confirmedIncreases.some(inc => inc.year === coverageStart.year && inc.month === coverageStart.month);
        if (hasCurrentMonthIncrease) {
          const currentInc = this.confirmedIncreases.find(inc => inc.year === coverageStart.year && inc.month === coverageStart.month);
          this.paymentDto.amount = currentInc.newRentAmount;
          this.onAmountChange(this.paymentDto.amount);
          this.syncPaymentPreview();
        }
      }
      this.showSummarySwal();
      this.selectedClientRentAmount = this.originalBaseRentCopy; // Restauramos valor base
    }
  }     

  // --- NUEVA LÃ“GICA DE REDONDEO INTELIGENTE ---
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
      // 1. Guardamos la confirmación del bloque actual (sea mes a pagar o débito futuro)
      this.confirmedIncreases.push({
        year: this.currentPendingIncrease.year,
        month: this.currentPendingIncrease.month,
        percentage: this.increasePercentage,
        newRentAmount: this.projectedNewRent
      });

      // 2. Si hay más aumentos en la fila esperando, actualizamos su base con este nuevo precio escalonado
      this.increaseQueue.forEach(item => {
        item.baseRent = this.projectedNewRent;
      });
    }

    // 3. Avanzamos de forma recursiva al siguiente elemento de la cola.
    // Si la cola se vacía, processNextIncrease() cerrará el overlay y abrirá el resumen automáticamente.
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
    const calc = this.getCalculatedAmounts(this.paymentDto.amount, this.paymentDto.paymentMethodId, this.selectedPreferredPaymentId);
    const breakdown = this.getSummaryBreakdown();
    
    const formatARS = (value: number) => {
      return new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
    };

    let commissionHtml = '';
    if (calc.isSurcharge) {
        commissionHtml = `<div class="flex justify-between text-sm text-orange-600 mt-1 pt-2 border-t border-gray-200"><span>PorciÃ³n retenida por comisiÃ³n</span><span class="font-bold">- ${formatARS(calc.difference)}</span></div>`;
    } else if (calc.isDiscount) {
        commissionHtml = `<div class="flex justify-between text-sm text-green-600 mt-1 pt-2 border-t border-gray-200"><span>BonificaciÃ³n a favor</span><span class="font-bold">+ ${formatARS(calc.difference)}</span></div>`;
    }

    Swal.fire({
      title: 'Resumen de TransacciÃ³n',
      html: `
        <div class="text-left space-y-3">
          <div class="pb-3 border-b border-gray-200">
            <div class="text-sm text-gray-500">MÃ©todo de pago utilizado</div>
            <div class="text-base font-semibold text-gray-800">${this.getNamePaymentMethodById(this.paymentDto.paymentMethodId)}</div>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <div class="p-3 rounded-lg bg-gray-50 border border-gray-200">
              <div class="text-sm text-gray-500">Dinero ingresado</div>
              <div class="text-lg font-semibold text-gray-900">${formatARS(calc.amountEntered)}</div>
            </div>
            <div class="p-3 rounded-lg ${calc.isDiscount ? 'bg-green-50 border-green-200' : (calc.isSurcharge ? 'bg-orange-50 border-orange-200' : 'bg-blue-50 border-blue-200')}">
              <div class="text-sm ${calc.isDiscount ? 'text-green-800' : (calc.isSurcharge ? 'text-orange-800' : 'text-blue-800')}">Deuda cancelada</div>
              <div class="text-lg font-bold ${calc.isDiscount ? 'text-green-900' : (calc.isSurcharge ? 'text-orange-900' : 'text-blue-900')}">${formatARS(calc.equivalentDebtPaid)}</div>
            </div>
          </div>
          <div class="p-4 rounded-lg border border-gray-200 bg-white">
            <div class="text-base font-semibold text-gray-800 mb-2">Desglose de cuenta corriente</div>
            <div class="flex justify-between text-sm text-gray-700">
              <span>AcreditaciÃ³n base</span><span class="font-semibold">${formatARS(calc.amountEntered)}</span>
            </div>
            <div class="mt-3 space-y-2">
              ${breakdown.map(item => `
                <div class="flex justify-between text-sm text-gray-700">
                  <span>${item.label}</span>
                  <span class="font-semibold">${formatARS(item.amount)}</span>
                </div>
              `).join('')}
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
      customClass: { confirmButton: 'bg-blue-600 text-white px-4 py-2 rounded-md mx-2', cancelButton: 'bg-gray-200 text-gray-800 px-4 py-2 rounded-md', actions: 'mt-4' }
    }).then((result) => {
      if (result.isConfirmed) {
        this.executeBackendCall(); // Directo al backend, el array de aumentos ya está cargado
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

  calculateAdvancePayment(): void {
    const suggestedAmount = this.selectedClientBalance < 0
      ? Math.abs(this.selectedClientBalance)
      : this.selectedClientRentAmount;

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
  goBackFromIncrease() {
    this.showIncreaseOverlay = false;
    this.currentIncreaseFlow = 'none';
    this.increaseResolved = false;
  }
}

