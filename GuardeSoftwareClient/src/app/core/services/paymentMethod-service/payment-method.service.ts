import { Injectable } from '@angular/core';
import { PaymentMethod } from '../../models/payment-method';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../../environments/environments';
import { CreatePaymentMethodDTO } from '../../dtos/paymentMethod/CreatePaymentMethodDTO';
import { UpdatePaymentMethodDTO } from '../../dtos/paymentMethod/UpdatePaymentMethodDTO';
import { DataRefreshService } from '../data-refresh-service/data-refresh.service';

@Injectable({
  providedIn: 'root'
})
export class PaymentMethodService {

  private url: string = environment.apiUrl
  constructor(
    private httpCliente: HttpClient,
    private dataRefresh: DataRefreshService,
  ) { }

  public getPaymentMethods(): Observable<PaymentMethod[]>{
        return this.httpCliente.get<PaymentMethod[]>(`${this.url}/PaymentMethod`);
  }

  public getPaymentMethodById(id: number): Observable<PaymentMethod>{
        return this.httpCliente.get<PaymentMethod>(`${this.url}/PaymentMethod/${id}`);
  }

  public deletePaymentMethod(id: number): Observable<any>{
        return this.httpCliente.delete<any>(`${this.url}/PaymentMethod/${id}`).pipe(
          tap(() => this.dataRefresh.notify('catalog')),
        );
  }

  public createPaymentMethod(dto: CreatePaymentMethodDTO):Observable<PaymentMethod>{
      return this.httpCliente.post<PaymentMethod>(`${this.url}/PaymentMethod`, dto).pipe(
        tap(() => this.dataRefresh.notify('catalog')),
      );
  }

  public UpdatePaymentMethod(id:number, dto: UpdatePaymentMethodDTO): Observable<any>{
      return this.httpCliente.patch<any>(`${this.url}/PaymentMethod/${id}`, dto).pipe(
        tap(() => this.dataRefresh.notify('catalog')),
      );
  }

}
