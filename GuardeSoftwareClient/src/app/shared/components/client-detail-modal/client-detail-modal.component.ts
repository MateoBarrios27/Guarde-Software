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
import { AccountMovementService } from '../../../core/services/accountMovement-service/account-movement.service';
import { CommunicationService } from '../../../core/services/communication-service/communication.service';
import { forkJoin } from 'rxjs';
import { AccountMovementDTO } from '../../../core/dtos/accountMovement/account-movement.dto';

// --- Interfaces de ejemplo para los nuevos historiales ---
export interface IClientMovement {
  id: number;
  date: Date;
  concept: string;
  amount: number;
  type: 'in' | 'out';
}

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
  imports: [CommonModule, IconComponent, CurrencyPipe, DatePipe],
  templateUrl: './client-detail-modal.component.html',
})
export class ClientDetailModalComponent implements OnChanges {
  @Input() client: ClientDetailDTO | null = null;
  @Output() closeModal = new EventEmitter<void>();

  public activeTab: 'movimientos' | 'comunicaciones' | 'detalles' =
    'movimientos';

  // --- Datos para las nuevas pestañas ---
  public historialMovimientos: AccountMovementDTO[] = [];
  public historialComunicaciones: IClientCommunication[] = [];
  public isLoadingHistory = false;
  public historyError: string | null = null;

  constructor(private accountMovementService: AccountMovementService,
    private communicationService: CommunicationService) {
    
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Si el input del cliente cambia (es decir, se abre el modal)
    if (changes['client'] && this.client) {
      this.loadHistoriales(this.client.id);
      this.activeTab = 'movimientos'; // Resetea a la pestaña principal
    }
  }

  /**
   * Carga los historiales del cliente desde el backend
   */
  loadHistoriales(clientId: number): void {
    this.isLoadingHistory = true;
    this.historyError = null; // Resetear error
    this.historialMovimientos = [];
    this.historialComunicaciones = [];

    // Usamos forkJoin para esperar ambas llamadas
    forkJoin({
      movements: this.accountMovementService.getMovementsByClientId(clientId),
      communications: this.communicationService.getCommunicationsByClientId(clientId)
    }).subscribe({
      next: (results) => {
        // Ordenamos los movimientos por fecha, más reciente primero
        this.historialMovimientos = results.movements.sort((a, b) => 
          new Date(b.movementDate).getTime() - new Date(a.movementDate).getTime()
        );
        
        // Ordenamos las comunicaciones por fecha, más reciente primero
        this.historialComunicaciones = results.communications.sort((a, b) => 
          new Date(b.date).getTime() - new Date(a.date).getTime()
        );
        
        this.isLoadingHistory = false;
      },
      error: (err) => {
        console.error('Error cargando historiales:', err);
        this.historyError = 'No se pudieron cargar los historiales. Intente más tarde.';
        this.isLoadingHistory = false;
      }
    });
  }

  // --- Métodos para la gestión de Movimientos ---

  openNewMovementModal(): void {
    // Aquí deberías abrir un nuevo modal para "Crear Movimiento"
    // Este modal probablemente necesitará el this.client.id
    console.log('Abrir modal para agregar nuevo movimiento al cliente:', this.client?.id);
    alert('FUNCIONALIDAD: Abrir modal de nuevo movimiento.');
    // Ejemplo de cómo podrías añadirlo tras una creación exitosa:
    // this.historialMovimientos.unshift(nuevoMovimiento);
  }

  deleteMovement(movementId: number): void {
    // Aquí deberías llamar a tu servicio para eliminar el movimiento
    console.log('Eliminar movimiento:', movementId);
    alert(`FUNCIONALIDAD: Eliminar movimiento ${movementId}.`);
    // Ejemplo de cómo actualizar la UI tras un borrado exitoso:
    // this.historialMovimientos = this.historialMovimientos.filter(m => m.id !== movementId);
  }

  // --- Style Helpers (Copiados de clients.component para consistencia) ---

  getEstadoBadgeColor(estado: string): string {
    const colors: Record<string, string> = {
      'Al día': 'bg-green-100 text-green-800',
      Moroso: 'bg-red-100 text-red-800',
      Pendiente: 'bg-yellow-100 text-yellow-800',
      Baja: 'bg-gray-200 text-gray-800',
    };
    return colors[estado] || 'bg-gray-100 text-gray-800';
  }

  getEstadoIcon(estado: string): string {
    const icons: Record<string, string> = {
      'Al día': 'check-circle',
      Moroso: 'alert-triangle',
      Pendiente: 'clock',
      Baja: 'user-x',
    };
    return icons[estado] || 'help-circle';
  }
}