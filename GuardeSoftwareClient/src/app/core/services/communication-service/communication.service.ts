import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ComunicacionDto } from './../../dtos/communications/communicationDto';
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

  // --- MÉTODO ACTUALIZADO ---
  // Ahora acepta FormData (DTO en JSON + Archivos)
  createCommunication(data: FormData): Observable<ComunicacionDto> {
    return this.http.post<ComunicacionDto>(`${this.url}/Communications`, data);
  }

  // --- MÉTODO ACTUALIZADO ---
  // Ahora acepta FormData (DTO en JSON + Archivos)
  updateCommunication(id: number, data: FormData): Observable<ComunicacionDto> {
    return this.http.put<ComunicacionDto>(`${this.url}/Communications/${id}`, data);
  }

  deleteCommunication(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/Communications/${id}`);
  }

  // --- MÉTODO ACTUALIZADO ---
  // Vuelve a ser simple. Solo envía el ID.
  // El backend buscará los archivos en el VPS.
  sendDraftNow(id: number): Observable<ComunicacionDto> {
    return this.http.post<ComunicacionDto>(`${this.url}/Communications/${id}/send`, {});
  }

  // --- NUEVO: Eliminar un adjunto existente ---
  // (Opcional, pero necesario si permites borrarlos desde "Editar")
  deleteAttachment(comunicacionId: number, fileName: string): Observable<void> {
    // El backend debe saber qué archivo borrar del VPS y de la DB
    return this.http.delete<void>(`${this.url}/Communications/${comunicacionId}/attachments/${fileName}`);
  }

  // --- MÉTODO NUEVO (Tu requerimiento de reintento) ---
  retryFailedSends(id: number, mailServerId: string): Observable<ComunicacionDto> {
    const body = { mailServerId: mailServerId };
    return this.http.post<ComunicacionDto>(`${this.url}/Communications/${id}/retry`, body);
  }
}