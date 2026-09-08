import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environments';

export interface ReceivableInput { description: string; date: string; totalAmount: number; notes: string; }
export interface ReceivablePayment { id: number; date: string; amount: number; comment: string; }
export interface Receivable extends ReceivableInput {
  id: number;
  paidAmount: number;
  remainingAmount: number;
  payments: ReceivablePayment[];
}

@Injectable({ providedIn: 'root' })
export class CashReceivablesService {
  private readonly url = `${environment.apiUrl}/cashflow/receivables`;
  constructor(private http: HttpClient) {}
  getAll() { return this.http.get<Receivable[]>(this.url); }
  create(input: ReceivableInput) { return this.http.post<number>(this.url, input); }
  update(id: number, input: ReceivableInput) { return this.http.put<number>(`${this.url}/${id}`, input); }
  delete(id: number) { return this.http.delete<number>(`${this.url}/${id}`); }
  addPayment(id: number, input: Omit<ReceivablePayment, 'id'> & { requestId: string }) { return this.http.post<number>(`${this.url}/${id}/payments`, input); }
  deletePayment(id: number, paymentId: number) { return this.http.delete<number>(`${this.url}/${id}/payments/${paymentId}`); }
}
