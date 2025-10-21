import { Injectable } from '@angular/core';
import { IncreaseRegimen } from '../../models/increase-regimen';
import { Observable } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environments';

@Injectable({
  providedIn: 'root'
})
export class IncreaseRegimenService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getIncreaseRegimens(): Observable<IncreaseRegimen[]>{
        return this.httpCliente.get<IncreaseRegimen[]>(`${this.url}/IncreaseRegimen`);
  }

  public getIncreaseRegimenById(id: number): Observable<IncreaseRegimen>{
        return this.httpCliente.get<IncreaseRegimen>(`${this.url}/IncreaseRegimen/${id}`);
  }
}
