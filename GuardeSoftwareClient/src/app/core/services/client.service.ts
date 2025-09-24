import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environmets';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Client } from '../models/client';

@Injectable({
  providedIn: 'root'
})
export class ClientService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getClients(): Observable<Client[]>{
    return this.httpCliente.get<Client[]>(`${this.url}/Client`);
  }
}
