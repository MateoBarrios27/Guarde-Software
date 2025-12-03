import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ComunicacionDto, UpsertComunicacionRequest } from './../../dtos/communications/communicationDto';
import { environment } from '../../../../environments/environments';
import { IClientCommunication } from '../../../shared/components/client-detail-modal/client-detail-modal.component';
import { SmtpConfig } from '../../models/smtp-config';

@Injectable({
  providedIn: 'root'
})
export class CommunicationService {
  private http = inject(HttpClient);
  private url: string = environment.apiUrl;

  constructor() { }

  // --- COMUNICACIONES ---

  getCommunications(): Observable<ComunicacionDto[]> {
    return this.http.get<ComunicacionDto[]>(`${this.url}/Communications`);
  }

  getCommunicationsByClientId(clientId: number): Observable<IClientCommunication[]> {
    return this.http.get<IClientCommunication[]>(`${this.url}/Communications/client/${clientId}`);
  }

  // Ahora acepta 'files' para el Multipart/Form-Data
  createCommunication(request: any, files: File[]): Observable<ComunicacionDto> {
    const formData = new FormData();
    
    formData.append('title', request.title);
    formData.append('content', request.content);
    formData.append('type', request.type);
    
    // Manejo de nulos seguro
    if (request.sendDate) formData.append('sendDate', request.sendDate);
    if (request.sendTime) formData.append('sendTime', request.sendTime);
    if (request.smtpConfigId) formData.append('smtpConfigId', request.smtpConfigId.toString());

    // Arrays
    if (request.channels) {
        request.channels.forEach((c: string) => formData.append('channels', c));
    }
    if (request.recipients) {
        request.recipients.forEach((r: string) => formData.append('recipients', r));
    }

    // Archivos
    if (files) {
        files.forEach(file => {
            formData.append('attachments', file, file.name);
        });
    }
    console.log('FormData to be sent:', request);
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

  sendDraftNow(id: number): Observable<ComunicacionDto> {
    return this.http.post<ComunicacionDto>(`${this.url}/Communications/${id}/send`, {});
  }

  retryCommunication(id: number): Observable<ComunicacionDto> {
    return this.http.post<ComunicacionDto>(`${this.url}/Communications/${id}/retry`, {});
  }

  // --- GESTIÓN DE SERVIDORES SMTP (NUEVO) ---

  getAllSmtpConfigs(): Observable<SmtpConfig[]> {
    // Asumiendo que creaste el controller SmtpConfigurationsController
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
}