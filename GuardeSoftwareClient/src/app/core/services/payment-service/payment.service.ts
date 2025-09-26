import { Injectable } from '@angular/core';
import { Payment } from '../../models/payment';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environmets';

@Injectable({
  providedIn: 'root'
})
export class PaymentService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getPayment(): Observable<Payment[]>{
        return this.httpCliente.get<Payment[]>(`${this.url}/Payment`);
  }
}
