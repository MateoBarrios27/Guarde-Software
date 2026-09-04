import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subject } from 'rxjs';
import {
  ComunicacionDto,
  CommunicationExtensionMode,
  CommunicationExtensionPreview,
  CommunicationExtensionResult,
  ExtendCommunicationRequest,
  UpsertComunicacionRequest
} from './../../dtos/communications/communicationDto';
import { environment } from '../../../../environments/environments';
import { IClientCommunication } from '../../../shared/components/client-detail-modal/client-detail-modal.component';
import { SmtpConfig } from '../../models/smtp-config';
import * as signalR from '@microsoft/signalr';

@Injectable({
  providedIn: 'root'
})
export class CommunicationService {
  private http = inject(HttpClient);
  private url: string = environment.apiUrl;
  
  private hubConnection: signalR.HubConnection | undefined;
  private communicationUpdatedSource = new Subject<number>();
  public onCommunicationUpdated$ = this.communicationUpdatedSource.asObservable();

  constructor() { }

  public startSignalRConnection(): void {
    // Si url es http://localhost:5242/api, el hub estará en http://localhost:5242/hubs/communications
    const baseUrl = this.url.endsWith('/api') ? this.url.substring(0, this.url.length - 4) : this.url;
    const hubUrl = `${baseUrl}/hubs/communications`;
    
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.hubConnection
      .start()
      .then(() => console.log('[CommunicationService] Conexión SignalR establecida.'))
      .catch(err => console.error('[CommunicationService] Error al conectar SignalR:', err));

    this.hubConnection.on('CommunicationUpdated', (communicationId: number) => {
      console.log(`[CommunicationService] Evento CommunicationUpdated recibido para ID: ${communicationId}`);
      this.communicationUpdatedSource.next(communicationId);
    });
  }
  
  public stopSignalRConnection(): void {
    if (this.hubConnection) {
      this.hubConnection.stop()
        .then(() => console.log('[CommunicationService] Conexión SignalR detenida.'))
        .catch(err => console.error('[CommunicationService] Error al detener SignalR:', err));
    }
  }

  getCommunications(): Observable<ComunicacionDto[]> {
    return this.http.get<ComunicacionDto[]>(`${this.url}/Communications`);
  }

  getCommunicationsByClientId(clientId: number): Observable<IClientCommunication[]> {
    return this.http.get<IClientCommunication[]>(`${this.url}/Communications/client/${clientId}`);
  }

  createCommunication(request: any, files: File[]): Observable<ComunicacionDto> {
    const formData = new FormData();
    
    formData.append('title', request.title);
    formData.append('content', request.content);
    formData.append('type', request.type);
    formData.append('isAccountStatement', request.isAccountStatement ? 'true' : 'false');
    formData.append('isNextMonthStatement', request.isNextMonthStatement ? 'true' : 'false');
    formData.append('sendToAllEmails', request.sendToAllEmails ? 'true' : 'false');
    
    if (request.isTestMode !== undefined) formData.append('isTestMode', request.isTestMode ? 'true' : 'false');
    if (request.testEmailAddress) formData.append('testEmailAddress', request.testEmailAddress);
    
    if (request.sendDate) formData.append('sendDate', request.sendDate);
    if (request.sendTime) formData.append('sendTime', request.sendTime);
    if (request.smtpConfigId) formData.append('smtpConfigId', request.smtpConfigId.toString());

    // Arrays
    if (request.channels) {
        request.channels.forEach((c: string, index: number) => formData.append(`channels[${index}]`, c));
    }
    if (request.recipients) {
        request.recipients.forEach((r: string, index: number) => formData.append(`recipients[${index}]`, r));
    }
    if (request.externalRecipientIds) {
        request.externalRecipientIds.forEach((id: number, index: number) =>
          formData.append(`externalRecipientIds[${index}]`, id.toString()));
    }

    // Archivos
    if (files) {
        files.forEach(file => {
            formData.append('attachments', file, file.name);
        });
    }
    return this.http.post<ComunicacionDto>(`${this.url}/Communications`, formData);
  }

  // NOTA: Si el update también permite cambiar archivos, deberías convertirlo a FormData igual que el Create.
  // Por ahora lo dejo como JSON si solo editas texto/destinatarios.
  updateCommunication(id: number, request: UpsertComunicacionRequest): Observable<ComunicacionDto> {
    return this.http.put<ComunicacionDto>(`${this.url}/Communications/${id}`, request);
  }

  deleteCommunication(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/Communications/${id}`);
  }

  getCommunicationById(id: number): Observable<ComunicacionDto> {
    return this.http.get<ComunicacionDto>(`${this.url}/Communications/${id}`);
  }

  sendDraftNow(id: number): Observable<ComunicacionDto> {
    return this.http.post<ComunicacionDto>(`${this.url}/Communications/${id}/send`, {});
  }

  retryCommunication(id: number): Observable<ComunicacionDto> {
    return this.http.post<ComunicacionDto>(`${this.url}/Communications/${id}/retry`, {});
  }

  retrySelectedCommunication(
    id: number,
    selectedClientIds: number[],
    selectedExternalRecipientIds: number[] = []
  ): Observable<ComunicacionDto> {
    return this.http.post<ComunicacionDto>(
      this.url + '/Communications/' + id + '/retry-selected',
      { selectedClientIds, selectedExternalRecipientIds }
    );
  }

  getCommunicationExtensionPreview(
    id: number,
    recipientType: string,
    mode: CommunicationExtensionMode
  ): Observable<CommunicationExtensionPreview> {
    const params = {
      recipientType,
      mode
    };
    return this.http.get<CommunicationExtensionPreview>(
      `${this.url}/Communications/${id}/extension-preview`,
      { params }
    );
  }

  extendCommunication(
    id: number,
    request: ExtendCommunicationRequest
  ): Observable<CommunicationExtensionResult> {
    return this.http.post<CommunicationExtensionResult>(
      `${this.url}/Communications/${id}/extend`,
      request
    );
  }

  getAllSmtpConfigs(): Observable<SmtpConfig[]> {
    return this.http.get<SmtpConfig[]>(`${this.url}/SmtpConfigurations`);
  }

  createSmtpConfig(config: SmtpConfig): Observable<SmtpConfig> {
    return this.http.post<SmtpConfig>(`${this.url}/SmtpConfigurations`, config);
  }

  updateSmtpConfig(config: SmtpConfig): Observable<SmtpConfig> {
    return this.http.put<SmtpConfig>(`${this.url}/SmtpConfigurations/${config.id}`, config);
  }

  deleteSmtpConfig(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/SmtpConfigurations/${id}`);
  }

  getClientsForSelector(): Observable<any[]> {
    return this.http.get<any[]>(`${this.url}/Communications/recipients-list`);
  }

  getDispatchContent(dispatchId: number): Observable<{content: string}> {
    return this.http.get<{content: string}>(`${this.url}/Communications/dispatch/${dispatchId}/content`);
  }
}
