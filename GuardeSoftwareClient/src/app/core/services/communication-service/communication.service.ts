import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ComunicacionDto, UpsertComunicacionRequest } from './../../dtos/communications/communicationDto';
import { environment } from '../../../../environments/environments';
import { IClientCommunication } from '../../../shared/components/client-detail-modal/client-detail-modal.component';

@Injectable({
  providedIn: 'root'
})
export class CommunicationService {
  private http = inject(HttpClient);
  private url: string = environment.apiUrl

  constructor() { }

  getCommunications(): Observable<ComunicacionDto[]> {
    return this.http.get<ComunicacionDto[]>(`${this.url}/Communications`);
  }

  public getCommunicationsByClientId(clientId: number): Observable<IClientCommunication[]> {
    return this.http.get<IClientCommunication[]>(`${this.url}/Communications/client/${clientId}`);
  }

  createCommunication(request: any, files: File[]): Observable<ComunicacionDto> {
    const formData = new FormData();
    
    // Agregar campos simples
    formData.append('title', request.title);
    formData.append('content', request.content);
    formData.append('type', request.type);
    if(request.sendDate) formData.append('sendDate', request.sendDate);
    if(request.sendTime) formData.append('sendTime', request.sendTime);
    if(request.smtpConfigId) formData.append('smtpConfigId', request.smtpConfigId.toString());

    // Agregar Arrays (ASP.NET espera formato 'channels[0]', 'channels[1]' o claves repetidas 'channels')
    request.channels.forEach((c: string) => formData.append('channels', c));
    request.recipients.forEach((r: string) => formData.append('recipients', r));

    // Agregar Archivos
    files.forEach(file => {
      formData.append('attachments', file, file.name);
    });

    return this.http.post<ComunicacionDto>(`${this.url}/Communications`, formData);
  }

  updateCommunication(id: number, request: UpsertComunicacionRequest): Observable<ComunicacionDto> {
    return this.http.put<ComunicacionDto>(`${this.url}/Communications/${id}`, request);
  }

  deleteCommunication(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/Communications/${id}`);
  }

  sendDraftNow(id: number): Observable<ComunicacionDto> {
    // Endpoint especial para forzar el envío de un borrador
    return this.http.post<ComunicacionDto>(`${this.url}/Communications/${id}/send`, {});
  }

  getSmtpConfigs(): Observable<any[]> {
  return this.http.get<any[]>(`${this.url}/Communications/smtp-configs`);
}
}