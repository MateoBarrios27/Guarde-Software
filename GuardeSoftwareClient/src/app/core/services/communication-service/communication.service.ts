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

  createCommunication(request: UpsertComunicacionRequest): Observable<ComunicacionDto> {
    return this.http.post<ComunicacionDto>(`${this.url}/Communications`, request);
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
}