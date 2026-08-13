import {
  Component,
  Input,
  Output,
  EventEmitter,
  HostListener,
  OnChanges,
  SimpleChanges,
  ChangeDetectorRef
} from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { IconComponent } from '../icon/icon.component';
import { PaymentIncreaseModalComponent } from '../payment-increase-modal/payment-increase-modal.component';
import { ClientDetailDTO } from '../../../core/dtos/client/ClientDetailDTO';
import { FormsModule } from '@angular/forms';

import { forkJoin } from 'rxjs';
import { finalize } from 'rxjs/operators';
import Swal from '../../services/ui-alert.service';
import { AccountMovementService } from '../../../core/services/accountMovement-service/account-movement.service';
import { CommunicationService } from '../../../core/services/communication-service/communication.service';
import { AccountMovementDTO } from '../../../core/dtos/accountMovement/account-movement.dto';
import { ClientCommunicationDTO } from '../../../core/dtos/communications/client-comunication.dto';
import { CreateMovementModalComponent } from '../create-movement-modal/create-movement-modal.component';

import { NgxPaginationModule } from 'ngx-pagination';
import { TimeDurationPipe } from '../../pipes/time-duration.pipe';
import { ClientLockerHistory } from '../../../core/models/client-locker-history';
import { ClientService, RentalAmountHistoryItem } from '../../../core/services/client-service/client.service';
import { AuthService } from '../../../core/services/auth-service/auth.service';
import {
  AppliedPaymentPlanningIncrease,
  PaymentPlanningContext,
  PaymentPlanningMonth
} from '../../../core/dtos/accountMovement/payment-planning.dto';
import { buildPaymentPlanningBreakdown, roundPlannedRent } from '../../utils/payment-planning.util';

export interface IClientCommunication {
  id: number;
  date: Date;
  type: 'email' | 'sms' | 'system';
  subject: string;
  snippet: string;
}

const SPANISH_MONTHS = [
  'enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio',
  'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'
];

@Component({
  selector: 'app-client-detail-modal',
  standalone: true,
  imports: [
    CommonModule,
    IconComponent,
    PaymentIncreaseModalComponent,
    DatePipe,
    CreateMovementModalComponent, 
    NgxPaginationModule,
    TimeDurationPipe,
    FormsModule
],
  templateUrl: './client-detail-modal.component.html',
})
export class ClientDetailModalComponent implements OnChanges {
  @Input() client: ClientDetailDTO | null = null;
  @Output() closeModal = new EventEmitter<void>();
  @Output() dataUpdated = new EventEmitter<number>();

  previewContent: string | null = null;
  previewClientName: string = '';

  public activeTab: 'movimientos' | 'comunicaciones' | 'detalles' | 'bauleras' | 'abono' =
    'movimientos';

  public historialMovimientos: AccountMovementDTO[] = [];
  public historialComunicaciones: ClientCommunicationDTO[] = [];
  public isLoadingHistory = false;
  public historyError: string | null = null;

  public showNewMovementModal = false;
  public showPaymentPlanningModal = false;
  public paymentPlanningStep: 'setup' | 'increase' | 'summary' = 'setup';
  public paymentPlanningMonths = 1;
  public chargeHalfSixthMonth = true;
  public paymentPlanningContext: PaymentPlanningContext | null = null;
  public paymentPlanningIncreases: AppliedPaymentPlanningIncrease[] = [];
  public paymentPlanningBreakdown: PaymentPlanningMonth[] = [];
  public currentPlanningIncreaseIndex = 0;
  public planningIncreasePercentage = 0;
  public planningProjectedRent = 0;
  public isLoadingPaymentPlan = false;
  public isSavingPaymentPlan = false;

  public movementCurrentPage: number = 1;
  public movementItemsPerPage: number = 10;
  public commCurrentPage: number = 1;
  public commItemsPerPage: number = 5; 

  public historialBauleras: ClientLockerHistory[] = [];
  public lockerCurrentPage: number = 1;
  public lockerItemsPerPage: number = 5;

  // ── Abono ────────────────────────────────────────────────────────────────
  public rentalAmountHistory: RentalAmountHistoryItem[] = [];
  public isLoadingAbono = false;
  public isAdmin = false;

  // Form for add/edit
  public showAbonoForm = false;
  public editingHistId: number | null = null;
  public abonoFormAmount: number | null = null;
  public abonoFormMonth: number = new Date().getMonth() + 1;
  public abonoFormYear: number = new Date().getFullYear();
  public isSavingAbono = false;

  public readonly months = SPANISH_MONTHS;
  public readonly currentYear = new Date().getFullYear();
  public readonly years = Array.from({ length: 10 }, (_, i) => this.currentYear - 5 + i);

  constructor(
    private accountMovementService: AccountMovementService,
    private communicationService: CommunicationService,
    private clientService: ClientService,
    private authService: AuthService,
    private cdr: ChangeDetectorRef
  ) {
    this.isAdmin = this.authService.isAdmin();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['client'] && this.client) {
      const previousClient = changes['client'].previousValue;
      if (!previousClient || previousClient.id !== this.client.id) {
        this.loadHistoriales(this.client.id);
        this.activeTab = 'movimientos';
        this.movementCurrentPage = 1;
        this.commCurrentPage = 1;
        this.lockerCurrentPage = 1;
        this.rentalAmountHistory = [];
        this.showAbonoForm = false;
        this.editingHistId = null;
      }
    }
  }

  viewDispatchContent(dispatchId: number): void {
    this.communicationService.getDispatchContent(dispatchId).subscribe({
      next: (res) => {
        this.previewContent = res.content;
        this.cdr.markForCheck();
      },
      error: () => {
        this.previewContent = null;
        this.cdr.markForCheck();
      }
    });
  }

  closeContentPreview(): void {
    this.previewContent = null;
  }

  loadHistoriales(clientId: number): void {
    if (!clientId) return;

    this.isLoadingHistory = true;
    this.historyError = null; 

    forkJoin({
      movements: this.accountMovementService.getMovementsByClientId(clientId),
      communications: this.communicationService.getCommunicationsByClientId(clientId),
      lockers: this.clientService.getClientLockerHistory(clientId)
    })
      .pipe(finalize(() => { 
        this.isLoadingHistory = false; 
        this.cdr.markForCheck();
      }))
      .subscribe({
        next: (results) => {
          this.historialMovimientos = results.movements.sort((a, b) => {
            const getDayString = (dateVal: any): string => {
              if (!dateVal) return '';
              const d = new Date(dateVal);
              const y = d.getFullYear();
              const m = String(d.getMonth() + 1).padStart(2, '0');
              const day = String(d.getDate()).padStart(2, '0');
              return `${y}-${m}-${day}`;
            };
            const dayA = getDayString(a.movementDate);
            const dayB = getDayString(b.movementDate);
            if (dayB !== dayA) return dayB.localeCompare(dayA);
            return Number(b.id || 0) - Number(a.id || 0);
          });
          this.historialComunicaciones = results.communications.sort((a, b) => 
            new Date(b.date).getTime() - new Date(a.date).getTime()
          );
          this.historialBauleras = results.lockers;
        },
        error: (err) => {
          console.error('Error al cargar historiales:', err);
          this.historyError = 'No se pudieron cargar los historiales. Intente más tarde.';
        },
      });
  }

  // ── Tab "Bauleras" ──────────────────────────────────────────────────────────
  deleteLockerHistory(histId: number): void {
    if (!this.client || !this.isAdmin) return;
    Swal.fire({
      title: '¿Eliminar historial?',
      text: 'Esta acción borrará este registro del historial de la baulera. No se puede deshacer.',
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#d33',
      cancelButtonColor: '#3085d6',
      confirmButtonText: 'Sí, eliminar',
      cancelButtonText: 'Cancelar'
    }).then((result) => {
      if (result.isConfirmed) {
        this.clientService.deleteLockerHistory(this.client!.id, histId).subscribe({
          next: () => {
            this.historialBauleras = this.historialBauleras.filter(h => h.id !== histId);
            this.cdr.markForCheck();
            Swal.fire('Eliminado', 'El historial fue eliminado correctamente.', 'success');
          },
          error: (err) => {
            console.error('Error al eliminar historial de baulera:', err);
            Swal.fire('Error', 'No se pudo eliminar el historial. Es posible que no tenga permisos suficientes o haya ocurrido un error.', 'error');
          }
        });
      }
    });
  }

  // ── Tab "Abono" ──────────────────────────────────────────────────────────
  onTabAbono(): void {
    this.activeTab = 'abono';
    if (this.rentalAmountHistory.length === 0 && this.client) {
      this.loadRentalAmountHistory();
    }
  }

  loadRentalAmountHistory(): void {
    if (!this.client) return;
    this.isLoadingAbono = true;
    this.clientService.getRentalAmountHistory(this.client.id).subscribe({
      next: (data) => {
        this.rentalAmountHistory = data;
        this.isLoadingAbono = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.isLoadingAbono = false;
        this.cdr.markForCheck();
        Swal.fire('Error', 'No se pudo cargar el historial de abonos.', 'error');
      }
    });
  }

  openAddAbonoForm(): void {
    this.editingHistId = null;
    this.abonoFormAmount = null;
    const now = new Date();
    this.abonoFormMonth = now.getMonth() + 1;
    this.abonoFormYear = now.getFullYear();
    this.showAbonoForm = true;
  }

  openEditAbonoForm(item: RentalAmountHistoryItem): void {
    if (item.status === 'past' && !this.isAdmin) {
      Swal.fire({
        icon: 'warning',
        title: 'Permiso requerido',
        text: 'Solo los administradores pueden editar tramos finalizados.',
        confirmButtonColor: '#2563eb'
      });
      return;
    }
    this.editingHistId = item.id;
    this.abonoFormAmount = item.amount;
    const d = new Date(item.startDate);
    this.abonoFormMonth = d.getMonth() + 1;
    this.abonoFormYear = d.getFullYear();
    this.showAbonoForm = true;
  }

  cancelAbonoForm(): void {
    this.showAbonoForm = false;
    this.editingHistId = null;
  }

  saveAbono(): void {
    if (!this.client || !this.abonoFormAmount || this.abonoFormAmount < 0) return;
    this.isSavingAbono = true;

    const payload = {
      amount: this.abonoFormAmount,
      year: this.abonoFormYear,
      month: this.abonoFormMonth
    };

    const request$ = this.editingHistId !== null
      ? this.clientService.updateRentalAmountEntry(this.client.id, this.editingHistId, payload)
      : this.clientService.addRentalAmountEntry(this.client.id, payload);

    request$.subscribe({
      next: () => {
        this.isSavingAbono = false;
        this.showAbonoForm = false;
        this.editingHistId = null;
        this.rentalAmountHistory = [];
        this.loadRentalAmountHistory();
        this.dataUpdated.emit(this.client!.id);
        this.cdr.markForCheck();
        Swal.fire({
          icon: 'success',
          title: 'Guardado',
          text: 'El tramo de abono fue guardado correctamente.',
          timer: 1500,
          showConfirmButton: false
        });
      },
      error: (err) => {
        this.isSavingAbono = false;
        this.cdr.markForCheck();
        Swal.fire('Error', err.error?.message || 'No se pudo guardar el tramo.', 'error');
      }
    });
  }

  deleteAbono(item: RentalAmountHistoryItem): void {
    if (!this.client) return;

    if (item.status === 'past' && !this.isAdmin) {
      Swal.fire({
        icon: 'warning',
        title: 'Permiso requerido',
        text: 'Solo los administradores pueden eliminar tramos finalizados.',
        confirmButtonColor: '#2563eb'
      });
      return;
    }

    if (item.status === 'active') {
      Swal.fire({
        icon: 'warning',
        title: 'No se puede eliminar',
        text: 'El tramo activo no se puede eliminar. Podés editarlo o agregar un nuevo tramo.',
        confirmButtonColor: '#2563eb'
      });
      return;
    }

    const clientId = this.client.id;
    Swal.fire({
      title: '¿Eliminar tramo?',
      text: `Se eliminará el tramo de $${item.amount.toLocaleString('es-AR')} y se recalcularán los balances.`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#d33',
      cancelButtonColor: '#6B7280',
      confirmButtonText: 'Sí, eliminar',
      cancelButtonText: 'Cancelar'
    }).then((result) => {
      if (result.isConfirmed) {
        this.clientService.deleteRentalAmountEntry(clientId, item.id).subscribe({
          next: () => {
            this.rentalAmountHistory = [];
            this.loadRentalAmountHistory();
            this.dataUpdated.emit(clientId);
            this.cdr.markForCheck();
            Swal.fire({ title: 'Eliminado', icon: 'success', timer: 1200, showConfirmButton: false });
          },
          error: (err) => {
            this.cdr.markForCheck();
            Swal.fire('Error', err.error?.message || 'No se pudo eliminar el tramo.', 'error');
          }
        });
      }
    });
  }

  formatAbonoDate(item: RentalAmountHistoryItem): string {
    const start = new Date(item.startDate);
    const startStr = `${SPANISH_MONTHS[start.getMonth()]} ${start.getFullYear()}`;
    if (!item.endDate) return `Desde ${startStr}`;
    const end = new Date(item.endDate);
    // end_date is set to 1 second before next start, so add 1 second to show the real boundary
    const endAdj = new Date(end.getTime() + 1000);
    const endStr = `${SPANISH_MONTHS[endAdj.getMonth()]} ${endAdj.getFullYear()}`;
    if (startStr === endStr) return `${startStr}`;
    return `${startStr} → ${endStr}`;
  }

  getAbonoStatusLabel(status: string): string {
    switch (status) {
      case 'active': return 'Activo';
      case 'planned': return 'Planificado';
      case 'past': return 'Finalizado';
      default: return status;
    }
  }

  getAbonoStatusClasses(status: string): string {
    switch (status) {
      case 'active': return 'bg-emerald-100 text-emerald-700 border-emerald-200';
      case 'planned': return 'bg-blue-100 text-blue-700 border-blue-200';
      case 'past': return 'bg-gray-100 text-gray-500 border-gray-200';
      default: return 'bg-gray-100 text-gray-600 border-gray-200';
    }
  }

  getAbonoDotClass(status: string): string {
    switch (status) {
      case 'active': return 'bg-emerald-500';
      case 'planned': return 'bg-blue-500';
      case 'past': return 'bg-gray-400';
      default: return 'bg-gray-300';
    }
  }

  // ── Movimientos ──────────────────────────────────────────────────────────
  openNewMovementModal(): void {
    this.showNewMovementModal = true;
  }

  closeNewMovementModal(): void {
    this.showNewMovementModal = false;
  }

  openPaymentPlanningModal(): void {
    this.paymentPlanningMonths = 1;
    this.chargeHalfSixthMonth = true;
    this.paymentPlanningStep = 'setup';
    this.paymentPlanningContext = null;
    this.paymentPlanningIncreases = [];
    this.paymentPlanningBreakdown = [];
    this.currentPlanningIncreaseIndex = 0;
    this.showPaymentPlanningModal = true;
  }

  closePaymentPlanningModal(): void {
    if (this.isSavingPaymentPlan) return;
    this.showPaymentPlanningModal = false;
  }

  @HostListener('document:keydown.escape')
  onPaymentPlanningEscape(): void {
    if (!this.showPaymentPlanningModal) return;
    if (this.paymentPlanningStep === 'increase') {
      this.skipPlanningIncrease();
      return;
    }
    this.closePaymentPlanningModal();
  }

  preparePaymentPlan(): void {
    if (!this.client || this.paymentPlanningMonths < 1 || this.paymentPlanningMonths > 24) return;
    this.isLoadingPaymentPlan = true;
    this.accountMovementService
      .getPaymentPlanningContext(this.client.id, this.paymentPlanningMonths)
      .pipe(finalize(() => {
        this.isLoadingPaymentPlan = false;
        this.cdr.markForCheck();
      }))
      .subscribe({
        next: context => {
          this.paymentPlanningContext = context;
          this.paymentPlanningIncreases = [];
          this.currentPlanningIncreaseIndex = 0;
          if (context.increases.length > 0) {
            this.startCurrentPlanningIncrease();
            this.paymentPlanningStep = 'increase';
          } else {
            this.updatePaymentPlanningBreakdown();
            this.paymentPlanningStep = 'summary';
          }
        },
        error: err => Swal.fire('No se pudo preparar', err.error?.message || 'No se pudo obtener la vista previa del pago.', 'error')
      });
  }

  get currentPlanningIncrease() {
    return this.paymentPlanningContext?.increases[this.currentPlanningIncreaseIndex] ?? null;
  }

  get currentPlanningBaseRent(): number {
    if (this.paymentPlanningIncreases.length > 0) {
      return this.paymentPlanningIncreases[this.paymentPlanningIncreases.length - 1].newRentAmount;
    }
    return Number(this.paymentPlanningContext?.baseRent || 0);
  }

  get planningProjectedNextIncreaseDate(): Date | null {
    const increase = this.currentPlanningIncrease;
    const frequency = Number(this.paymentPlanningContext?.increaseFrequencyMonths || 0);
    if (!increase || frequency <= 0) return null;
    return new Date(increase.year, increase.month - 1 + Math.max(1, frequency - 1), 1);
  }

  startCurrentPlanningIncrease(): void {
    this.planningIncreasePercentage = 0;
    this.planningProjectedRent = this.currentPlanningBaseRent;
  }

  calculatePlanningProjectedRent(): void {
    this.planningProjectedRent = roundPlannedRent(
      this.currentPlanningBaseRent,
      Number(this.planningIncreasePercentage || 0),
      this.client?.preferredPaymentMethod || ''
    );
    if (this.currentPlanningBaseRent > 0 && this.planningIncreasePercentage > 0) {
      this.planningIncreasePercentage = Number(
        (((this.planningProjectedRent - this.currentPlanningBaseRent) / this.currentPlanningBaseRent) * 100).toFixed(4)
      );
    }
  }

  onPlanningProjectedRentBlur(): void {
    const baseRent = this.currentPlanningBaseRent;
    const targetRent = Number(this.planningProjectedRent || 0);

    if (baseRent <= 0 || targetRent <= baseRent) {
      this.planningIncreasePercentage = 0;
      this.planningProjectedRent = baseRent;
      return;
    }

    this.planningIncreasePercentage = Number(
      (((targetRent - baseRent) / baseRent) * 100).toFixed(4)
    );
    this.calculatePlanningProjectedRent();
  }

  confirmPlanningIncrease(): void {
    const increase = this.currentPlanningIncrease;
    if (!increase || this.planningProjectedRent <= 0) return;
    this.paymentPlanningIncreases.push({
      year: increase.year,
      month: increase.month,
      percentage: Number(this.planningIncreasePercentage || 0),
      newRentAmount: this.planningProjectedRent
    });
    this.currentPlanningIncreaseIndex++;
    if (this.paymentPlanningContext && this.currentPlanningIncreaseIndex < this.paymentPlanningContext.increases.length) {
      this.startCurrentPlanningIncrease();
      return;
    }
    this.updatePaymentPlanningBreakdown();
    this.paymentPlanningStep = 'summary';
  }

  skipPlanningIncrease(): void {
    if (!this.currentPlanningIncrease) return;
    this.planningIncreasePercentage = 0;
    this.planningProjectedRent = this.currentPlanningBaseRent;
    this.confirmPlanningIncrease();
  }

  backPaymentPlanningStep(): void {
    if (this.paymentPlanningStep === 'summary' && this.paymentPlanningContext?.increases.length) {
      this.paymentPlanningStep = 'increase';
      this.currentPlanningIncreaseIndex = Math.max(0, this.paymentPlanningIncreases.length - 1);
      const previous = this.paymentPlanningIncreases.pop();
      this.planningIncreasePercentage = previous?.percentage ?? 0;
      this.planningProjectedRent = previous?.newRentAmount ?? this.currentPlanningBaseRent;
      return;
    }
    if (this.paymentPlanningStep === 'increase' && this.currentPlanningIncreaseIndex > 0) {
      this.currentPlanningIncreaseIndex--;
      const previous = this.paymentPlanningIncreases.pop();
      this.planningIncreasePercentage = previous?.percentage ?? 0;
      this.planningProjectedRent = previous?.newRentAmount ?? this.currentPlanningBaseRent;
      return;
    }
    this.paymentPlanningStep = 'setup';
    this.paymentPlanningContext = null;
    this.paymentPlanningIncreases = [];
  }

  updatePaymentPlanningBreakdown(): void {
    if (!this.paymentPlanningContext) return;
    this.paymentPlanningBreakdown = buildPaymentPlanningBreakdown(
      this.paymentPlanningContext,
      this.paymentPlanningIncreases,
      this.chargeHalfSixthMonth
    );
  }

  get paymentPlanningTotal(): number {
    return this.paymentPlanningBreakdown.reduce((total, month) => total + month.amount, 0);
  }

  savePaymentPlan(): void {
    if (!this.client || !this.paymentPlanningContext || this.isSavingPaymentPlan) return;
    this.isSavingPaymentPlan = true;
    this.accountMovementService.planClientPayment({
      clientId: this.client.id,
      months: this.paymentPlanningMonths,
      chargeHalfSixthMonth: this.chargeHalfSixthMonth,
      appliedIncreases: this.paymentPlanningIncreases
    }).pipe(finalize(() => {
      this.isSavingPaymentPlan = false;
      this.cdr.markForCheck();
    })).subscribe({
      next: result => {
        this.showPaymentPlanningModal = false;
        this.movementCurrentPage = 1;
        this.loadHistoriales(this.client!.id);
        this.dataUpdated.emit(this.client!.id);
        Swal.fire({
          icon: 'success',
          title: 'Pago planificado',
          text: `Se generaron ${result.createdDebits} débitos por $${result.totalAmount.toLocaleString('es-AR', { minimumFractionDigits: 2 })}.`,
          confirmButtonColor: '#2563eb'
        });
      },
      error: err => Swal.fire('No se pudo planificar', err.error?.message || 'Ocurrió un error al generar los débitos.', 'error')
    });
  }

  onMovementSaveSuccess(): void {
    this.closeNewMovementModal();
    this.movementCurrentPage = 1; 
    if (this.client) {
      this.loadHistoriales(this.client.id); 
      this.dataUpdated.emit(this.client.id);
    }
  }

  deleteMovement(movementId: number): void {
    if (!this.client) return;
    const clientId = this.client.id; 

    Swal.fire({
      title: '¿Estás seguro?',
      text: "Esta acción no se puede revertir. ¿Deseas eliminar este movimiento?",
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#d33',
      cancelButtonColor: '#6B7280',
      confirmButtonText: 'Sí, eliminar',
      cancelButtonText: 'Cancelar',
    }).then((result) => {
      if (result.isConfirmed) {
        this.isLoadingHistory = true; 
        this.accountMovementService.deleteMovement(movementId).subscribe({
          next: () => {
            Swal.fire({ title: 'Eliminado', text: 'El movimiento ha sido eliminado.', icon: 'success', confirmButtonColor: '#2563eb' });
            this.movementCurrentPage = 1;
            this.loadHistoriales(clientId); 
            this.dataUpdated.emit(clientId);
            this.cdr.markForCheck();
          },
          error: (err) => {
            this.isLoadingHistory = false;
            this.cdr.markForCheck();
            Swal.fire({ title: 'Error', text: 'No se pudo eliminar el movimiento. ' + (err.error?.message || ''), icon: 'error', confirmButtonColor: '#2563eb' });
          },
        });
      }
    });
  }

  getEstadoBadgeColor(estado: string): string {
     const colors: Record<string, string> = {
      'Al día': 'bg-green-100 text-green-800',
      'Moroso Nivel 1': 'bg-red-100 text-red-800',
      'Moroso Nivel 2': 'bg-red-100 text-red-800',
      'Moroso Nivel 3': 'bg-red-100 text-red-800',
      'Pendiente': 'bg-yellow-100 text-yellow-800',
      'Baja': 'bg-gray-200 text-gray-800',
    };
    return colors[estado] || 'bg-gray-100 text-gray-800';
  }

  getEstadoIcon(estado: string): string {
     const icons: Record<string, string> = {
      'Al día': 'check-circle',
      'Moroso Nivel 1': 'alert-triangle',
      'Moroso Nivel 2': 'alert-triangle',
      'Moroso Nivel 3': 'alert-triangle',
      'Pendiente': 'clock',
      'Baja': 'user-x',
    };
    return icons[estado] || 'help-circle';
  }

  onClientColorChange(client: ClientDetailDTO): void {
    if (!client || !client.id) return;
    if (client.color && client.color.toLowerCase() === '#ffffff') {
      client.color = null as any;
    }
    this.clientService.updateClientColor(client.id, client.color).subscribe({
      next: () => this.dataUpdated.emit(client.id),
      error: () => Swal.fire('Error', 'No se pudo actualizar el color del cliente', 'error')
    });
  }

  onClientCommentChange(client: ClientDetailDTO): void {
    if (!client || !client.id) return;
    client.commentUpdatedAt = new Date();
    this.clientService.updateClientComment(client.id, client.comment).subscribe({
      next: () => this.dataUpdated.emit(client.id),
      error: () => Swal.fire('Error', 'No se pudo actualizar el comentario del cliente', 'error')
    });
  }

  onClientNotesChange(client: ClientDetailDTO): void {
    if (!client || !client.id) return;
    this.clientService.updateClientNotes(client.id, client.notes).subscribe({
      next: () => this.dataUpdated.emit(client.id),
      error: () => Swal.fire('Error', 'No se pudieron actualizar las observaciones del cliente', 'error')
    });
  }

  resetClientColor(client: ClientDetailDTO): void {
    client.color = null as any;
    this.onClientColorChange(client);
  }

  deleteClientComment(client: ClientDetailDTO): void {
    client.comment = '';
    client.commentUpdatedAt = new Date();
    this.onClientCommentChange(client);
  }

  getFormattedUpdatedDate(date?: Date | string | null): string {
    if (!date) return '';
    const d = new Date(date);
    if (isNaN(d.getTime())) return '';
    const day = String(d.getDate()).padStart(2, '0');
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const year = d.getFullYear();
    const hours = String(d.getHours()).padStart(2, '0');
    const minutes = String(d.getMinutes()).padStart(2, '0');
    return `Últ. modificación: ${day}/${month}/${year} ${hours}:${minutes}`;
  }

  isFutureMonthOrLater(date?: Date | string | null): boolean {
    if (!date) return false;
    const d = new Date(date);
    if (isNaN(d.getTime())) return false;
    const now = new Date();
    const dVal = d.getFullYear() * 12 + d.getMonth();
    const nowVal = now.getFullYear() * 12 + now.getMonth();
    return dVal > nowVal;
  }
}
