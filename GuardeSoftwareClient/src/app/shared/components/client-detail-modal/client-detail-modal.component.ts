import {
  Component,
  Input,
  Output,
  EventEmitter,
  OnChanges,
  SimpleChanges,
} from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { IconComponent } from '../icon/icon.component';
import { ClientDetailDTO } from '../../../core/dtos/client/ClientDetailDTO';
import { FormsModule } from '@angular/forms';

import { forkJoin } from 'rxjs';
import { finalize } from 'rxjs/operators';
import Swal from 'sweetalert2';
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
    CurrencyPipe,
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

  public activeTab: 'movimientos' | 'comunicaciones' | 'detalles' | 'bauleras' | 'abono' =
    'movimientos';

  public historialMovimientos: AccountMovementDTO[] = [];
  public historialComunicaciones: ClientCommunicationDTO[] = [];
  public isLoadingHistory = false;
  public historyError: string | null = null;

  public showNewMovementModal = false;

  public movementCurrentPage: number = 1;
  public movementItemsPerPage: number = 5;
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
    private authService: AuthService
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

  loadHistoriales(clientId: number): void {
    if (!clientId) return;

    this.isLoadingHistory = true;
    this.historyError = null; 

    forkJoin({
      movements: this.accountMovementService.getMovementsByClientId(clientId),
      communications: this.communicationService.getCommunicationsByClientId(clientId),
      lockers: this.clientService.getClientLockerHistory(clientId)
    })
      .pipe(finalize(() => { this.isLoadingHistory = false; }))
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
      },
      error: () => {
        this.isLoadingAbono = false;
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
            Swal.fire({ title: 'Eliminado', icon: 'success', timer: 1200, showConfirmButton: false });
          },
          error: (err) => {
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
          },
          error: (err) => {
            this.isLoadingHistory = false;
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