import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../../../environments/environments';

@Injectable({
  providedIn: 'root'
})
export class CashSignalrService {
  private hubConnection: signalR.HubConnection | undefined;
  
  // Observable que emite cuando la caja fue actualizada por cualquier cliente
  private cashUpdatedSource = new Subject<void>();
  public onCashUpdated$ = this.cashUpdatedSource.asObservable();

  constructor() { }

  /**
   * Inicia la conexión SignalR con el Hub de Caja.
   */
  public startConnection(): void {
    if (this.hubConnection && this.hubConnection.state !== signalR.HubConnectionState.Disconnected) {
      return; // Ya está conectado o conectando
    }

    const token = localStorage.getItem('authToken');
    const hubUrl = environment.apiUrl.replace('/api', '') + '/hubs/cash';

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => token ?? '',
        skipNegotiation: false,
        transport: signalR.HttpTransportType.WebSockets
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    // Escuchar el evento que emite el backend
    this.hubConnection.on('CashUpdated', () => {
      this.cashUpdatedSource.next();
    });

    this.hubConnection
      .start()
      .then(() => console.log('[CashSignalrService] Conexión SignalR establecida con la Caja.'))
      .catch(err => console.error('[CashSignalrService] Error al conectar SignalR:', err));
  }

  /**
   * Detiene la conexión SignalR.
   */
  public stopConnection(): void {
    if (this.hubConnection) {
      this.hubConnection.stop()
        .then(() => console.log('[CashSignalrService] Conexión SignalR de Caja detenida.'))
        .catch(err => console.error('[CashSignalrService] Error al detener SignalR:', err));
    }
  }
}
