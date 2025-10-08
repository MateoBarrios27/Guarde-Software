import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environmets';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AccountMovement } from '../../models/account-movement';

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
}
