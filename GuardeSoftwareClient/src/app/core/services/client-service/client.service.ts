import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environmets';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Client } from '../../models/client';
import { ClientDetailDTO } from '../../dtos/client/ClientDetailDTO';
import { CreateClientDTO } from '../../dtos/client/CreateClientDTO';
import { CreateClientResponseDTO } from '../../dtos/client/CreateClientResponseDTO';

@Injectable({
  providedIn: 'root'
})
export class ClientService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getClients(): Observable<Client[]>{
    return this.httpCliente.get<Client[]>(`${this.url}/Client`);
  }

  public getClientById(id: number): Observable<Client>{
    return this.httpCliente.get<Client>(`${this.url}/Client/${id}`);
  }

  public getClientDetailById(id: number): Observable<ClientDetailDTO>{
    return this.httpCliente.get<ClientDetailDTO>(`${this.url}/Client/detail/${id}`);
  }

  public CreateClient(dto: CreateClientDTO): Observable<CreateClientResponseDTO> {
    return this.httpCliente.post<any>(`${this.url}/Client`, dto);
  }
}
