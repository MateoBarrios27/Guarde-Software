import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environments';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { AccountMovement } from '../../models/account-movement';
import { AccountMovementDTO } from '../../dtos/accountMovement/account-movement.dto';
import { CreateAccountMovementDTO } from '../../dtos/accountMovement/create-account-movement.dto';
import {
  PaymentPlanningContext,
  PlanClientPaymentRequest,
  PlannedPaymentResult
} from '../../dtos/accountMovement/payment-planning.dto';
import { DataRefreshService } from '../data-refresh-service/data-refresh.service';

@Injectable({
  providedIn: 'root'
})
export class AccountMovementService {

  private url: string = environment.apiUrl
  constructor(
    private httpCliente: HttpClient,
    private dataRefresh: DataRefreshService,
  ) { }

  public getAccountMovements(): Observable<AccountMovement[]>{
      return this.httpCliente.get<AccountMovement[]>(`${this.url}/AccountMovement`);
    }

  public getAccountMovementById(id: number): Observable<AccountMovement>{
      return this.httpCliente.get<AccountMovement>(`${this.url}/AccountMovement/${id}`);
    }  

  public getMovementsByClientId(clientId: number): Observable<AccountMovementDTO[]> {
    return this.httpCliente.get<AccountMovementDTO[]>(`${this.url}/AccountMovement/client/${clientId}`);
  }

  createMovement(dto: CreateAccountMovementDTO): Observable<AccountMovementDTO> {
    return this.httpCliente.post<AccountMovementDTO>(`${this.url}/AccountMovement`, dto).pipe(
      tap(() => this.dataRefresh.notify(['finances', 'clients'])),
    );
  }

  deleteMovement(movementId: number): Observable<void> {
    return this.httpCliente.delete<void>(`${this.url}/AccountMovement/${movementId}`).pipe(
      tap(() => this.dataRefresh.notify(['finances', 'clients'])),
    );
  }

  getPaymentPlanningContext(clientId: number, months: number): Observable<PaymentPlanningContext> {
    return this.httpCliente.get<PaymentPlanningContext>(
      `${this.url}/AccountMovement/payment-plan/context/${clientId}`,
      { params: { months: months.toString() } }
    );
  }

  planClientPayment(dto: PlanClientPaymentRequest): Observable<PlannedPaymentResult> {
    return this.httpCliente.post<PlannedPaymentResult>(`${this.url}/AccountMovement/payment-plan`, dto).pipe(
      tap(() => this.dataRefresh.notify(['finances', 'clients'])),
    );
  }
}
