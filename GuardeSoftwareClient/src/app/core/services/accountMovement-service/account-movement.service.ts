import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environments';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AccountMovement } from '../../models/account-movement';
import { AccountMovementDTO } from '../../dtos/accountMovement/account-movement.dto';
import { CreateAccountMovementDTO } from '../../dtos/accountMovement/create-account-movement.dto';
import {
  PaymentPlanningContext,
  PlanClientPaymentRequest,
  PlannedPaymentResult
} from '../../dtos/accountMovement/payment-planning.dto';

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

  createMovement(dto: CreateAccountMovementDTO): Observable<AccountMovementDTO> {
    return this.httpCliente.post<AccountMovementDTO>(`${this.url}/AccountMovement`, dto);
  }

  deleteMovement(movementId: number): Observable<void> {
    return this.httpCliente.delete<void>(`${this.url}/AccountMovement/${movementId}`);
  }

  getPaymentPlanningContext(clientId: number, months: number): Observable<PaymentPlanningContext> {
    return this.httpCliente.get<PaymentPlanningContext>(
      `${this.url}/AccountMovement/payment-plan/context/${clientId}`,
      { params: { months: months.toString() } }
    );
  }

  planClientPayment(dto: PlanClientPaymentRequest): Observable<PlannedPaymentResult> {
    return this.httpCliente.post<PlannedPaymentResult>(`${this.url}/AccountMovement/payment-plan`, dto);
  }
}
