import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ComunicacionDto, UpsertComunicacionRequest } from './../../dtos/communications/communicationDto';

@Injectable({
  providedIn: 'root'
})
export class CommunicationService {
  private http = inject(HttpClient);
  // Ajusta la URL base a tu API
  private apiUrl = '/api/comunicaciones'; 

  constructor() { }

  getComunicaciones(): Observable<ComunicacionDto[]> {
    return this.http.get<ComunicacionDto[]>(this.apiUrl);
  }

  createComunicacion(request: UpsertComunicacionRequest): Observable<ComunicacionDto> {
    return this.http.post<ComunicacionDto>(this.apiUrl, request);
  }

  updateComunicacion(id: number, request: UpsertComunicacionRequest): Observable<ComunicacionDto> {
    return this.http.put<ComunicacionDto>(`${this.apiUrl}/${id}`, request);
  }

  deleteComunicacion(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  sendDraftNow(id: number): Observable<ComunicacionDto> {
    // Endpoint especial para forzar el envío de un borrador
    return this.http.post<ComunicacionDto>(`${this.apiUrl}/${id}/send`, {});
  }
}