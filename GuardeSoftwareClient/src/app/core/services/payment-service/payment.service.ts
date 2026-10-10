import { Injectable } from '@angular/core';
import { Payment } from '../../models/payment';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, catchError, defer, from, switchMap, throwError } from 'rxjs';
import { requestPaymentDecision } from './payment-decision-dialog';
import { environment } from '../../../../environments/environments';
import { CreatePaymentDTO } from '../../dtos/payment/CreatePaymentDTO';
import { DetailedPaymentDTO } from '../../dtos/payment/DetailedPaymentDTO';
import { DataRefreshService } from '../data-refresh-service/data-refresh.service';

@Injectable({
  providedIn: 'root'
})
export class PaymentService {

  private url: string = environment.apiUrl
  constructor(
    private httpCliente: HttpClient,
    private dataRefresh: DataRefreshService,
  ) { }

  public getPayments(): Observable<Payment[]>{
        return this.httpCliente.get<Payment[]>(`${this.url}/Payment`);
  }

  public getPaymentById(id: number): Observable<Payment>{
    return this.httpCliente.get<Payment>(`${this.url}/Payment/${id}`);
  }

  public getPaymentByClientId(id: number): Observable<Payment[]> {
    return this.httpCliente.get<Payment[]>(`${this.url}/Payment/ByClientId${id}`);
  }

  public CreatePayment(dto: CreatePaymentDTO): Observable<any>{
    return this.createWithDecision({ ...dto }).pipe(
      tap(() => this.dataRefresh.notify(['finances', 'clients'])),
    );
  }

  private createWithDecision(dto: CreatePaymentDTO, attempts = 0): Observable<any> {
    return defer(() => this.httpCliente.post<any>(`${this.url}/Payment`, dto)).pipe(
      catchError(error => {
        if (error?.status !== 422 || error?.error?.code !== 'PAYMENT_DECISION_REQUIRED' || attempts >= 3) {
          return throwError(() => error);
        }
        return from(requestPaymentDecision(error.error)).pipe(
          switchMap(action => action
            ? this.createWithDecision({ ...dto, futureDebitAction: action,
                paymentDecisionToken: error.error.decisionToken,
                expectedPaymentStateToken: error.error.expectedPaymentStateToken }, attempts + 1)
            : throwError(() => ({ paymentDecisionCancelled: true })))
        );
      })
    );
  }

  public getDetailedPayment(): Observable<DetailedPaymentDTO[]>{
      return this.httpCliente.get<DetailedPaymentDTO[]>(`${this.url}/Payment/detailed`);
  }

  deletePayment(paymentId: number): Observable<any> {
    return this.httpCliente.delete(`${this.url}/Payment/${paymentId}`).pipe(
      tap(() => this.dataRefresh.notify(['finances', 'clients'])),
    );
  }
  
}
