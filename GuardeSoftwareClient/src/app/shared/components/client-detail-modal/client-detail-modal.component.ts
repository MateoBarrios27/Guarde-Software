import {
  Component,
  Input,
  Output,
  EventEmitter,
  OnChanges,
  SimpleChanges,
  SimpleChange,
} from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { IconComponent } from '../icon/icon.component';
import { ClientDetailDTO } from '../../../core/dtos/client/ClientDetailDTO';

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

export interface IClientCommunication {
  id: number;
  date: Date;
  type: 'email' | 'sms' | 'system';
  subject: string;
  snippet: string;
}

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
    TimeDurationPipe
],
  templateUrl: './client-detail-modal.component.html',
})
export class ClientDetailModalComponent implements OnChanges {
  @Input() client: ClientDetailDTO | null = null;
  @Output() closeModal = new EventEmitter<void>();
  @Output() dataUpdated = new EventEmitter<number>();

  public activeTab: 'movimientos' | 'comunicaciones' | 'detalles' =
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

  constructor(
    private accountMovementService: AccountMovementService,
    private communicationService: CommunicationService
  ) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['client'] && this.client) {

      const previousClient = changes['client'].previousValue;
      
      if (!previousClient || previousClient.id !== this.client.id) {
        this.loadHistoriales(this.client.id);
        this.activeTab = 'movimientos';
        this.movementCurrentPage = 1;
        this.commCurrentPage = 1;
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
    })
      .pipe(
        finalize(() => {
          this.isLoadingHistory = false; 
        })
      )
      .subscribe({
        next: (results) => {
          // Ordenar por fecha (más reciente primero)
          this.historialMovimientos = results.movements.sort((a, b) => 
            new Date(b.movementDate).getTime() - new Date(a.movementDate).getTime()
          );
          this.historialComunicaciones = results.communications.sort((a, b) => 
            new Date(b.date).getTime() - new Date(a.date).getTime()
          );
        },
        error: (err) => {
          console.error('Error al cargar historiales:', err);
          this.historyError = 'No se pudieron cargar los historiales. Intente más tarde.';
        },
      });
  }

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
      'Moroso': 'bg-red-100 text-red-800',
      'Pendiente': 'bg-yellow-100 text-yellow-800',
      'Baja': 'bg-gray-200 text-gray-800',
    };
    return colors[estado] || 'bg-gray-100 text-gray-800';
  }

  getEstadoIcon(estado: string): string {
     const icons: Record<string, string> = {
      'Al día': 'check-circle',
      'Moroso': 'alert-triangle',
      'Pendiente': 'clock',
      'Baja': 'user-x',
    };
    return icons[estado] || 'help-circle';
  }
}