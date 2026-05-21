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

export interface DetailedPaymentView extends DetailedPaymentDTO {
  groupPos?: 'start' | 'middle' | 'end' | 'none';
  isGrouped?: boolean;
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
    private clientService: ClientService
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
  itemsPerPage: number = 10;

  manualDateEnabled = false;
  dateString: string = '';
  searchClient: string = '';
  amountOriginal = 0;
  selectedClientIncreaseAnchorDate: string | null = null;
  
  // Banderas de lógica
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

  ngOnInit(): void {
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
      next: (data) => this.clients = data,
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
    this.manualDateEnabled = false;
    this.showIncreaseOverlay = false;
    this.currentIncreaseFlow = 'none';
    
    const now = new Date();
    this.dateString = now.toISOString().split('T')[0];

    this.paymentDto = {
      clientId: 0, movementType: 'CREDITO', concept: ` `, amount: 0, paymentMethodId: 1, date: now, isAdvancePayment: false, advanceMonths: 0
    };
    this.updateConceptFromDate(now);
  }

  private updateProjectedDate() {
    if (this.selectedClientIncreaseAnchorDate) {
      let currentAnchor = new Date(this.selectedClientIncreaseAnchorDate);
      this.projectedNextIncreaseDate = new Date(currentAnchor.getFullYear(), currentAnchor.getMonth() + 3, 1);
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
      clientId: 0, movementType: 'CREDITO', concept: ` `, amount: 0, paymentMethodId: 1, date: now, isAdvancePayment: false, advanceMonths: 0
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
    this.selectedPreferredPaymentId = Number(client.preferredPaymentMethodId ?? 1); 
    this.paymentDto.paymentMethodId = this.selectedPreferredPaymentId;
    this.selectedPendingSurcharge = Number(client.pendingSurcharge ?? 0);
    this.increaseResolved = false;
    this.increasePercentage = 0;
    this.paymentDto.increasePercentage = 0;
    this.showIncreaseOverlay = false;
    this.currentIncreaseFlow = 'none';
    
    // Sugerencia de saldo a cobrar
    const suggestedAmount = this.selectedClientBalance < 0 
        ? Math.abs(this.selectedClientBalance) 
        : this.selectedClientRentAmount;

    this.paymentDto.amount = suggestedAmount;

    this.checkIncreaseLogic();
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
    const months = this.paymentDto.advanceMonths;
    if (months === null || months === undefined || months === 0) {
      this.paymentDto.concept = 'Pago adelantado';
      return;
    }
    this.paymentDto.concept = `Pago de ${months} mes${months === 1 ? '' : 'es'}`;
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
        title: isGroup ? '¿Eliminar transacción completa?' : '¿Eliminar movimiento?',
        text: isGroup 
           ? 'Se borrará el pago principal y su bonificación/recargo asociado. Esta acción no se puede deshacer.'
           : 'Esta acción borrará este registro contable. No se puede deshacer.',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#d33', 
        cancelButtonColor: '#9ca3af', 
        confirmButtonText: 'Sí, eliminar',
        cancelButtonText: 'Cancelar'
      }).then((result) => {
        if (result.isConfirmed) {
          this.paymentService.deletePayment(p.movementId).subscribe({
            next: () => {
              Swal.fire({ title: '¡Eliminado!', text: 'El registro ha sido borrado correctamente.', icon: 'success', confirmButtonColor: '#2563eb' });
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
  // --- LÓGICA DEL CEREBRO DE AUMENTOS ---
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
    const selectedMonth = this.getSelectedPaymentMonth();
    const today = new Date();
    const currentMonth = { year: today.getFullYear(), month: today.getMonth() + 1 };

    if (this.selectedClientBalance < 0) {
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

  checkIncreaseLogic() {
    this.hasIncreaseInPeriod = false;
    this.isIncreaseNextMonth = false;

    if (!this.selectedClientIncreaseAnchorDate) return;

    // 1. Extraemos la Fecha Ancla en formato numérico (ej: 202607) para comparar fácil
    const anchorString = this.selectedClientIncreaseAnchorDate.split('T')[0];
    const [aYear, aMonth] = anchorString.split('-').map(Number);
    const anchorValue = aYear * 100 + aMonth;

    // 2. Extraemos el Mes Actual del Pago
    const coverageStart = this.getCoverageStartMonth();
    const currentMonthValue = coverageStart.year * 100 + coverageStart.month;

    // 3. ¿HASTA DÓNDE LLEGA LA PLATA?
    // Deuda actual (si el balance es negativo, lo pasamos a positivo para la matemática)
    const currentDebt = this.selectedClientBalance < 0 ? Math.abs(this.selectedClientBalance) : 0;
    let moneyInHand = this.paymentDto.amount || 0;
    
    let farthestYear = coverageStart.year;
    let farthestMonth = coverageStart.month;

    // Si entregó más plata que la deuda, calculamos cuántos meses a futuro cubre
    if (moneyInHand > currentDebt) {
      let extraMoney = moneyInHand - currentDebt;
      let rent = this.selectedClientRentAmount || 1; // Evita dividir por 0
      
      let futureMonthsCovered = Math.ceil(extraMoney / rent);
      
      // Si el usuario usó explícitamente el selector de meses, tomamos el mayor
      if (this.paymentDto.isAdvancePayment && this.paymentDto.advanceMonths) {
          futureMonthsCovered = Math.max(futureMonthsCovered, this.paymentDto.advanceMonths);
      }

      const farthest = this.addMonths(coverageStart.year, coverageStart.month, futureMonthsCovered - 1);
      farthestYear = farthest.year;
      farthestMonth = farthest.month;
    } else if (this.paymentDto.isAdvancePayment && this.paymentDto.advanceMonths) {
       // Caso donde no cubre la deuda pero forzó el selector manual
       const farthest = this.addMonths(coverageStart.year, coverageStart.month, this.paymentDto.advanceMonths - 1);
       farthestYear = farthest.year;
       farthestMonth = farthest.month;
    }

    const farthestMonthValue = farthestYear * 100 + farthestMonth;

    // VERIFICACIÓN A: El aumento cae DENTRO del dinero que puso
    if (anchorValue >= currentMonthValue && anchorValue <= farthestMonthValue) {
      this.hasIncreaseInPeriod = true;
    }

    // VERIFICACIÓN B: Si no cae dentro, ¿cae exactamente el mes que viene después de la plata ingresada?
    if (!this.hasIncreaseInPeriod) {
      const nextCoverageMonth = this.addMonths(farthestYear, farthestMonth, 1);
      const nextMonth = nextCoverageMonth.month;
      const nextYear = nextCoverageMonth.year;
      const nextMonthValue = nextYear * 100 + nextMonth;

      if (anchorValue === nextMonthValue) {
          this.isIncreaseNextMonth = true;
      }
    }
  }

  // Se ejecuta al hacer clic en GUARDAR PAGO
  savePaymentModal(dto: CreatePaymentDTO) {
    if (!this.paymentMethods?.length) { Swal.fire({ icon: 'warning', title: 'Cargando métodos', text: 'Esperá a que carguen.' }); return; }
    if (!this.paymentDto.clientId || this.paymentDto.clientId <= 0) { Swal.fire({ icon: 'warning', title: 'Cliente requerido', text: 'Seleccioná un cliente.' }); return; }
    if (!dto.amount || dto.amount <= 0) { Swal.fire({ icon: 'warning', title: 'Monto inválido', text: 'Ingresá un monto válido.' }); return; }
    if (!dto.paymentMethodId) { Swal.fire({ icon: 'warning', title: 'Método requerido', text: 'Seleccioná método.' }); return; }
    if (dto.isAdvancePayment && (!dto.advanceMonths || dto.advanceMonths < 1)) { Swal.fire({ icon: 'warning', title: 'Meses inválidos', text: 'Mínimo 1 mes.' }); return; }

    if (this.manualDateEnabled && this.dateString) {
      const [year, month, day] = this.dateString.split('-').map(Number);
      const currentTime = new Date();
      this.paymentDto.date = new Date(year, month - 1, day, currentTime.getHours(), currentTime.getMinutes(), currentTime.getSeconds());
    }

    this.checkIncreaseLogic();

    // Verificamos si el precio se congela por regla de 6 meses
    const isPriceLocked = this.paymentDto.isAdvancePayment && this.paymentDto.advanceMonths && this.paymentDto.advanceMonths >= 6;
    // Evalúa si el aumento recae directamente sobre la plata que están pagando hoy
    const affectsCurrentTotal = this.hasIncreaseInPeriod;

    if (affectsCurrentTotal && !isPriceLocked && !this.increaseResolved) {
      // FLUJO A: Aumento en el medio del pago (ANTES del resumen)
      this.currentIncreaseFlow = 'advance';
      this.increasePromptReason = 'Dentro de los meses que está pagando, el cliente tiene programado un aumento.';
      this.calculateProjectedRent();
      this.showIncreaseOverlay = true;
    } else {
      // FLUJO B: Pago normal. Si hay aumento mes que viene, salta el modal DESPUÉS del resumen.
      this.currentIncreaseFlow = (this.isIncreaseNextMonth && !isPriceLocked) ? 'normal' : 'none';
      this.showSummarySwal();
    }
  }

  calculateProjectedRent() {
    const rent = this.selectedClientRentAmount || 0;
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

  calculatePercentageFromRent() {
    const rent = this.selectedClientRentAmount || 0;
    
    if (rent === 0) {
      this.increasePercentage = 0;
    } else {
      let newRent = this.projectedNewRent || 0;
      // Calculamos la diferencia porcentual
      let perc = ((newRent - rent) / rent) * 100;
      
      // Lo redondeamos a 2 decimales para que el input del % no quede con números infinitos (ej: 33.33)
      this.increasePercentage = parseFloat(perc.toFixed(2));
    }
    
    this.updateProjectedDate();
  }

  // --- ESCENARIO A: Al terminar de editar el PORCENTAJE ---
  onIncreasePercentageBlur() {
    const rent = this.selectedClientRentAmount || 0;
    const perc = this.increasePercentage || 0;
    
    // Calculamos el monto bruto
    let newRent = rent + (rent * (perc / 100));

    // Aplicamos redondeo si el método es Efectivo
    const methodName = this.getNamePaymentMethodById(this.selectedPreferredPaymentId).toLowerCase();
    if (methodName.includes('efectivo')) {
      newRent = this.roundToNearest1000(newRent);
      
      // Re-ajustamos el porcentaje para que sea exacto al monto redondeado
      if (rent > 0) {
        this.increasePercentage = parseFloat((((newRent - rent) / rent) * 100).toFixed(2));
      }
    }

    this.projectedNewRent = newRent;
    this.updateProjectedDate();
  }

  // --- ESCENARIO B: Al terminar de editar el MONTO ($) ---
  onProjectedRentBlur() {
    const rent = this.selectedClientRentAmount || 0;
    let targetRent = this.projectedNewRent || 0;

    const methodName = this.getNamePaymentMethodById(this.selectedPreferredPaymentId).toLowerCase();
    
    // Si es efectivo, redondeamos lo que escribió el usuario antes de calcular el %
    if (methodName.includes('efectivo')) {
      targetRent = this.roundToNearest1000(targetRent);
      this.projectedNewRent = targetRent; 
    }
    
    if (rent === 0) {
      this.increasePercentage = 0;
    } else {
      // Calculamos el porcentaje que representa ese nuevo monto
      const perc = ((targetRent - rent) / rent) * 100;
      this.increasePercentage = parseFloat(perc.toFixed(2));
    }
    
    this.updateProjectedDate();
  }

  confirmIncrease() {
    this.paymentDto.increasePercentage = this.increasePercentage;
    this.increaseResolved = true;
    this.showIncreaseOverlay = false;
    
    if (this.currentIncreaseFlow === 'advance') {
      // SOLO recalculamos si el usuario usó explícitamente el switch de meses
      if (this.paymentDto.isAdvancePayment) {
          this.calculateAdvancePayment(); 
      }
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
      // SOLO recalculamos si el usuario usó explícitamente el switch de meses
      if (this.paymentDto.isAdvancePayment) {
          this.calculateAdvancePayment();
      }
      this.showSummarySwal();
    } else if (this.currentIncreaseFlow === 'normal') {
      this.executeBackendCall();
    }
  }

  showSummarySwal() {
    const calc = this.getCalculatedAmounts(this.paymentDto.amount, this.paymentDto.paymentMethodId, this.selectedPreferredPaymentId);
    
    const formatARS = (value: number) => {
      return new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
    };

    let commissionHtml = '';
    if (calc.isSurcharge) {
        commissionHtml = `<div class="flex justify-between text-sm text-orange-600 mt-1 pt-2 border-t border-gray-200"><span>Porción retenida por comisión</span><span class="font-bold">- ${formatARS(calc.difference)}</span></div>`;
    } else if (calc.isDiscount) {
        commissionHtml = `<div class="flex justify-between text-sm text-green-600 mt-1 pt-2 border-t border-gray-200"><span>Bonificación a favor</span><span class="font-bold">+ ${formatARS(calc.difference)}</span></div>`;
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
              <span>Acreditación base</span><span class="font-semibold">${formatARS(calc.amountEntered)}</span>
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
        // FLUJO B (NORMAL): Mostramos el modal de aumento DESPUÉS del resumen
        if (this.currentIncreaseFlow === 'normal' && !this.increaseResolved) {
          this.increasePromptReason = 'El próximo mes le corresponde una actualización de abono.';
          this.calculateProjectedRent();
          this.showIncreaseOverlay = true;
        } else {
          // Si no hay modal o ya se resolvió, directo a BD
          this.executeBackendCall();
        }
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
          : (calc.isDiscount ? `Bonificación por pago en ${this.getNamePaymentMethodById(this.paymentDto.paymentMethodId)} (${calc.selectedCommission}%)` : '')
    };

    this.paymentService.CreatePayment(payloadToSave).subscribe({
      next: () => {
        Swal.fire({ title: 'Pago registrado', text: 'El pago se registró correctamente.', icon: 'success', confirmButtonColor: '#2563eb' });
        this.closeClientModal();
        setTimeout(() => {
          this.loadClients();
          this.loadPayments()
        }
          , 100); 
      },
      error: (err) => {
        console.error('Error al guardar payment:', err);
        Swal.fire({ title: 'Error', text: 'Hubo un problema al registrar la transacción en la base de datos.', icon: 'error', confirmButtonColor: '#2563eb' });
      }
    });
  }

  private roundToNearest1000(amount: number): number {
    if (amount === 0) return 0;
    return Math.round(amount / 1000) * 1000;
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

    let baseRent = this.selectedClientRentAmount;
    let totalToPay = 0;

    let anchorValue = 0;
    if (this.selectedClientIncreaseAnchorDate) {
      const parts = this.selectedClientIncreaseAnchorDate.split('T')[0].split('-').map(Number);
      anchorValue = parts[0] * 100 + parts[1];
    }
    
    // EXTRACCIÓN SEGURA DE FECHA BASE PAGA
    const coverageStart = this.getCoverageStartMonth();

    // EXTRACCIÓN SEGURA DE FECHA ANCLA
    let aYear: number | null = null, aMonth: number | null = null;
    if (this.selectedClientIncreaseAnchorDate) {
      const parts = this.selectedClientIncreaseAnchorDate.split('T')[0].split('-').map(Number);
      aYear = parts[0];
      aMonth = parts[1];
    }

    const currentMethodName = this.getNamePaymentMethodById(this.paymentDto.paymentMethodId).toLowerCase();
    const isEfectivo = currentMethodName.includes('efectivo');

    for (let i = 0; i < this.paymentDto.advanceMonths; i++) {
      if (i === 0) {
        totalToPay += suggestedAmount; // Mes actual + Deuda
      } 
      else {
        // Calculamos qué mes estamos procesando en el bucle
        const coverageMonth = this.addMonths(coverageStart.year, coverageStart.month, i);
        const loopMonth = coverageMonth.month;
        const loopYear = coverageMonth.year;
        let currentLoopValue = loopYear * 100 + loopMonth;

        let rentForThisMonth = baseRent;

        // ¿Corresponde aumento para este mes específico del bucle?
        if (anchorValue > 0 && currentLoopValue >= anchorValue) {
          if (this.paymentDto.increasePercentage && this.paymentDto.increasePercentage > 0) {
            rentForThisMonth += baseRent * (this.paymentDto.increasePercentage / 100);
          }
        }

        if (isEfectivo) rentForThisMonth = this.roundToNearest1000(rentForThisMonth);
        totalToPay += rentForThisMonth;
      }
    }

    this.paymentDto.amount = totalToPay;
    this.onAmountChange(totalToPay);
  }
}
