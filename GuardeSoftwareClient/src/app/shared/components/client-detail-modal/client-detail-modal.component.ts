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
  public historialMovimientos: IClientMovement[] = [];
  public historialComunicaciones: IClientCommunication[] = [];
  public isLoadingHistory = false;

  constructor() {
    // Aquí deberías inyectar tus nuevos servicios, por ejemplo:
    // private paymentService: PaymentService,
    // private communicationService: CommunicationService
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

    // --- EJEMPLO: Simulación de llamadas a servicios ---
    // Deberías reemplazar esto con tus llamadas reales
    // 1. Cargar Movimientos
    // this.paymentService.getMovementHistory(clientId).subscribe(data => {
    //   this.historialMovimientos = data;
    // });
    this.historialMovimientos = [
      {
        id: 1,
        date: new Date('2025-10-01'),
        concept: 'Pago mensual Alquiler',
        amount: 25000,
        type: 'in',
      },
      {
        id: 2,
        date: new Date('2025-10-05'),
        concept: 'Ajuste por mora',
        amount: -1500,
        type: 'out',
      },
      {
        id: 3,
        date: new Date('2025-09-01'),
        concept: 'Pago mensual Alquiler',
        amount: 25000,
        type: 'in',
      },
    ];

    // 2. Cargar Comunicaciones
    // this.communicationService.getCommunicationHistory(clientId).subscribe(data => {
    //   this.historialComunicaciones = data;
    // });
    this.historialComunicaciones = [
      {
        id: 1,
        date: new Date('2025-10-02'),
        type: 'email',
        subject: 'Confirmación de Pago',
        snippet: 'Hemos recibido tu pago de $25.000...',
      },
      {
        id: 2,
        date: new Date('2025-09-25'),
        type: 'email',
        subject: 'Recordatorio de Vencimiento',
        snippet: 'Te recordamos que tu factura vence pronto...',
      },
      {
        id: 3,
        date: new Date('2025-09-15'),
        type: 'system',
        subject: 'Actualización de Datos',
        snippet: 'Se actualizó el número de teléfono.',
      },
    ];
    // --- Fin de la simulación ---

    this.isLoadingHistory = false;
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