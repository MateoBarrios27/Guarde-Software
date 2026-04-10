import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environments';
import { CashFlowItem, FinancialAccount, MonthlySummary } from '../../models/cash';

@Injectable({
  providedIn: 'root'
})
export class CashService {
  private apiUrl = `${environment.apiUrl}/cashflow`;

  constructor(private http: HttpClient) {}

  getItems(month: number, year: number): Observable<CashFlowItem[]> {
    const params = new HttpParams().set('month', month).set('year', year);
    return this.http.get<CashFlowItem[]>(`${this.apiUrl}/items`, { params });
  }

  upsertItem(item: CashFlowItem, month: number, year: number): Observable<number> {
    const params = new HttpParams().set('month', month).set('year', year);
    return this.http.post<number>(`${this.apiUrl}/items`, item, { params });
  }

  deleteItem(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/items/${id}`);
  }

  getMonthlySummary(month: number, year: number): Observable<MonthlySummary> {
    const params = new HttpParams().set('month', month).set('year', year);
    return this.http.get<MonthlySummary>(`${this.apiUrl}/summary`, { params });
  }

  getAccounts(month: number, year: number): Observable<FinancialAccount[]> {
    const params = new HttpParams().set('month', month).set('year', year);
    return this.http.get<FinancialAccount[]>(`${this.apiUrl}/accounts`, { params });
  }

  updateAccountBalance(id: number, newBalance: number, month: number, year: number): Observable<void> {
    const params = new HttpParams().set('month', month).set('year', year);
    return this.http.put<void>(`${this.apiUrl}/accounts/${id}`, { balance: newBalance }, { params });
  }

  createAccount(account: FinancialAccount, month: number, year: number): Observable<number> {
    const params = new HttpParams().set('month', month).set('year', year);
    return this.http.post<number>(`${this.apiUrl}/accounts`, account, { params });
  }

  deleteAccount(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/accounts/${id}`);
  }

  updateItemsOrder(itemsOrder: { id?: number, displayOrder: number }[]) {
    return this.http.post(`${this.apiUrl}/update-order`, itemsOrder);
  }

   updateAccountsOrder(accountsOrder: { id: number, displayOrder: number }[]) {
    return this.http.post(`${this.apiUrl}/update-accounts-order`, accountsOrder);
  }

  getUsdRate(month: number, year: number): Observable<number> {
    const params = new HttpParams().set('month', month).set('year', year);
    return this.http.get<number>(`${this.apiUrl}/usd-rate`, { params });
  }

  updateUsdRate(rate: number, month: number, year: number): Observable<void> {
    const params = new HttpParams().set('month', month).set('year', year);
    return this.http.post<void>(`${this.apiUrl}/usd-rate`, { balance: rate }, { params });
  }

}