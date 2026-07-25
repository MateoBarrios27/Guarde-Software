import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject } from 'rxjs';
import { environment } from '../../../../environments/environments';
import { HttpClient } from '@angular/common/http';

export interface SystemAlert {
  title: string;
  message: string;
  severity: 'warning' | 'danger' | 'info' | 'maintenance';
  senderName: string;
  createdAt: string;
}

export interface CreateAlertPayload {
  title: string;
  message: string;
  severity: 'warning' | 'danger' | 'info' | 'maintenance';
}

@Injectable({
  providedIn: 'root'
})
export class AlertService {
  private hubConnection!: signalR.HubConnection;
  private readonly hubUrl = environment.apiUrl.replace('/api', '') + '/hubs/alert';
  private readonly apiUrl = environment.apiUrl + '/Alert';

  /** Alerta activa del sistema. null = ninguna alerta en pantalla. */
  activeAlert$ = new BehaviorSubject<SystemAlert | null>(null);

  constructor(private http: HttpClient) {}

  /**
   * Inicia la conexión SignalR con el Hub de alertas.
   * Llamar desde AppComponent.ngOnInit() una vez que el usuario esté autenticado.
   */
  startConnection(): void {
    if (this.hubConnection && this.hubConnection.state !== signalR.HubConnectionState.Disconnected) {
      return; // Ya existe una conexión activa o en proceso
    }

    const token = localStorage.getItem('authToken');

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl, {
        accessTokenFactory: () => token ?? '',
        skipNegotiation: false,
        transport: signalR.HttpTransportType.WebSockets
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.hubConnection
      .start()
      .then(() => {
        console.log('[AlertService] Conexión SignalR establecida.');
        // Al conectarse, revisar si hay una alerta activa (ej: usuario recargó la página)
        this.fetchActiveAlert();
      })
      .catch(err => console.error('[AlertService] Error al conectar SignalR:', err));

    // Escuchar alertas entrantes
    this.hubConnection.on('ReceiveSystemAlert', (alert: SystemAlert) => {
      this.handleIncomingAlert(alert);
    });

    // Escuchar cuando la alerta es limpiada desde el servidor
    this.hubConnection.on('ClearSystemAlert', () => {
      this.handleIncomingAlert(null);
    });
  }

  /**
   * Detiene la conexión SignalR (al cerrar sesión).
   */
  stopConnection(): void {
    if (this.hubConnection) {
      this.hubConnection.stop().catch(err =>
        console.error('[AlertService] Error al detener SignalR:', err)
      );
    }
  }

  /**
   * Emite un cartel de advertencia a todos los usuarios conectados.
   */
  sendAlert(payload: CreateAlertPayload) {
    return this.http.post(`${this.apiUrl}/send`, payload);
  }

  /**
   * Limpia la alerta activa del sistema.
   */
  clearAlert() {
    return this.http.delete(`${this.apiUrl}/clear`);
  }

  /**
   * Descarta la alerta localmente (solo en la pantalla del usuario actual).
   */
  dismissAlert(): void {
    const currentAlert = this.activeAlert$.value;
    if (currentAlert) {
      localStorage.setItem('dismissedAlertAt', currentAlert.createdAt);
    }
    this.activeAlert$.next(null);
  }

  /**
   * Verifica si hay una alerta activa en el servidor al conectarse.
   */
  private fetchActiveAlert(): void {
    this.http.get<SystemAlert>(`${this.apiUrl}/active`, { observe: 'response' }).subscribe({
      next: (response) => {
        if (response.status === 200 && response.body) {
          this.handleIncomingAlert(response.body);
        }
      },
      error: () => {} // No hacer nada si falla (no es crítico)
    });
  }

  /**
   * Procesa la alerta recibida, validando si debe mostrarse al usuario actual.
   */
  private handleIncomingAlert(alert: SystemAlert | null): void {
    if (!alert) {
      this.activeAlert$.next(null);
      return;
    }

    const currentUserName = localStorage.getItem('userName');
    if (alert.senderName === currentUserName) {
      // Si el usuario actual es quien emitió la alerta, no se la mostramos a él mismo
      return;
    }

    const dismissedAt = localStorage.getItem('dismissedAlertAt');
    if (dismissedAt === alert.createdAt) {
      // Esta alerta ya fue cerrada por el usuario localmente (ej. tras un refresh F5)
      return;
    }

    this.activeAlert$.next(alert);
  }
}
