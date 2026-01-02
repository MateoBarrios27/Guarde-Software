import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environments';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { Client } from '../../models/client';
import { ClientDetailDTO } from '../../dtos/client/ClientDetailDTO';
import { CreateClientDTO } from '../../dtos/client/CreateClientDTO';
import { CreateClientResponseDTO } from '../../dtos/client/CreateClientResponseDTO';
import { TableClient } from '../../dtos/client/TableClientDto';
import { GetClientsRequest } from '../../dtos/client/GetClientsRequest';
import { PaginatedResult } from '../../dtos/common/PaginatedResultDto';

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

  public updateClient(id: number, dto: CreateClientDTO): Observable<any> {
    return this.httpCliente.put<any>(`${this.url}/Client/${id}`, dto);
  }

  /**
   * Obtains a paginated list of clients from the backend.
   * @param request - An object containing pagination, sorting, and filtering parameters.
   * @returns An observable with the paginated result of clients.
   */
  public getTableClients(request: GetClientsRequest): Observable<PaginatedResult<TableClient>> {
    // We use HttpParams to build the query string parameters 
    let params = new HttpParams();

    if (request.pageNumber) {
      params = params.append('pageNumber', request.pageNumber.toString());
    }
    if (request.pageSize) {
      params = params.append('pageSize', request.pageSize.toString());
    }
    if (request.sortField) {
      params = params.append('sortField', request.sortField);
    }
    if (request.sortDirection) {
      params = params.append('sortDirection', request.sortDirection);
    }
    if (request.searchTerm) {
      params = params.append('searchTerm', request.searchTerm);
    }
    if (request.active !== undefined && request.active !== null) {
      params = params.append('active', request.active.toString());
    }

    return this.httpCliente.get<PaginatedResult<TableClient>>(`${this.url}/Client/table`, { params });
  }

  public getRecipientOptions(): Observable<string[]> {
    return this.httpCliente.get<string[]>(`${this.url}/Client/recipient-options`);
  }

  searchClients(query: string): Observable<string[]> {
    if (!query.trim() || query.length < 2) {
      // No busques si la consulta es muy corta
      return of([]);
    }
    return this.httpCliente.get<string[]>(`${this.url}/Client/search`, {
      params: { query }
    })
  };

  public deactivateClient(id: number): Observable<void> {
    return this.httpCliente.delete<void>(`${this.url}/Client/${id}`);
  }

  reactivateClient(clientId: number): Observable<any> {
    return this.httpCliente.put(`${this.url}/Client/${clientId}/reactivate`, {});
  }
}
