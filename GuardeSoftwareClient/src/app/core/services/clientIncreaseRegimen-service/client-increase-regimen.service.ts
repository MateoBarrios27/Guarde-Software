import { Injectable } from '@angular/core';
import { ClientIncreaseRegimen } from '../../models/client-increase-regimen';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environmets';

@Injectable({
  providedIn: 'root'
})
export class ClientIncreaseRegimenService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getClientIncreaseRegimens(): Observable<ClientIncreaseRegimen[]>{
        return this.httpCliente.get<ClientIncreaseRegimen[]>(`${this.url}/ClientIncreaseRegimen`);
  }

  public getClientIncreaseRegimenById(id: number): Observable<ClientIncreaseRegimen>{
        return this.httpCliente.get<ClientIncreaseRegimen>(`${this.url}/ClientIncreaseRegimen/${id}`);
  }
}
