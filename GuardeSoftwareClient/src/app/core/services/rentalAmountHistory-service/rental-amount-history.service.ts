import { Injectable } from '@angular/core';
import { RentalAmountHistory } from '../../models/rental-amount-history';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environmets';

@Injectable({
  providedIn: 'root'
})
export class RentalAmountHistoryService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getRentalAmountHistory(): Observable<RentalAmountHistory[]>{
        return this.httpCliente.get<RentalAmountHistory[]>(`${this.url}/RentalAmountHistory`);
  }
}
