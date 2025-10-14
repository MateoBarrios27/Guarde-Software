import { Injectable } from '@angular/core';
import { Client } from '../../models/client';
import { Observable, of } from 'rxjs';
import { Cliente, CreateCommunicationRequest } from '../../models/communications';
import { environment } from '../../../../environments/environmets';
import { HttpClient } from '@angular/common/http';

@Injectable({
  providedIn: 'root'
})
export class CommunicationService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  createCommunication(comunicado: CreateCommunicationRequest): Observable<any> {
    return this.httpCliente.post(this.url, comunicado);
  }

  // Método para obtener clientes (simulado)
  getClients(): Observable<Cliente[]> {
    const mockClientes: Cliente[] = [
      { id: 101, name: 'Cliente Fiel S.A.' },
      { id: 102, name: 'Negocios Rápidos SRL' },
      { id: 103, name: 'Innovaciones Tecnológicas' },
      { id: 104, name: 'Consultora Global' }
    ];
    // En un caso real, esto sería una llamada HTTP:
    // return this.http.get<Cliente[]>('/api/clientes');
    return of(mockClientes);
  }
}
