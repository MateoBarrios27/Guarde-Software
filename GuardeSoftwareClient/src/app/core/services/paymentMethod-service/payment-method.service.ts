import { Injectable } from '@angular/core';
import { PaymentMethod } from '../../models/payment-method';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environmets';

@Injectable({
  providedIn: 'root'
})
export class PaymentMethodService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getPaymentMethod(): Observable<PaymentMethod[]>{
        return this.httpCliente.get<PaymentMethod[]>(`${this.url}/PaymentMethod`);
  }

  public getPaymentMethodById(id: number): Observable<PaymentMethod>{
        return this.httpCliente.get<PaymentMethod>(`${this.url}/PaymentMethod/${id}`);
  }

  public deletePaymentMethod(id: number): Observable<any>{
        return this.httpCliente.delete<any>(`${this.url}/PaymentMethod/${id}`);
  }
}
