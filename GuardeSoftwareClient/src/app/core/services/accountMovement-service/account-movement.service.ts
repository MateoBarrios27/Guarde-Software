import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environments';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AccountMovement } from '../../models/account-movement';
import { AccountMovementDTO } from '../../dtos/accountMovement/account-movement.dto';

@Injectable({
  providedIn: 'root'
})
export class AccountMovementService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getAccountMovements(): Observable<AccountMovement[]>{
      return this.httpCliente.get<AccountMovement[]>(`${this.url}/AccountMovement`);
    }

  public getAccountMovementById(id: number): Observable<AccountMovement>{
      return this.httpCliente.get<AccountMovement>(`${this.url}/AccountMovement/${id}`);
    }  

  public getMovementsByClientId(clientId: number): Observable<AccountMovementDTO[]> {
    return this.httpCliente.get<AccountMovementDTO[]>(`${this.url}/AccountMovement/client/${clientId}`);
  }
}
