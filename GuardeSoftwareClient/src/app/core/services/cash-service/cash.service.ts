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

  // --- BLOQUE 1: Items de Caja ---
  getItems(month: number, year: number): Observable<CashFlowItem[]> {
    const params = new HttpParams().set('month', month).set('year', year);
    return this.http.get<CashFlowItem[]>(`${this.apiUrl}/items`, { params });
  }

  upsertItem(item: CashFlowItem): Observable<number> { // Devuelve el ID
    return this.http.post<number>(`${this.apiUrl}/items`, item);
  }

  deleteItem(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/items/${id}`);
  }

  // --- BLOQUE 2: Resumen Automático ---
  getMonthlySummary(month: number, year: number): Observable<MonthlySummary> {
    const params = new HttpParams().set('month', month).set('year', year);
    return this.http.get<MonthlySummary>(`${this.apiUrl}/summary`, { params });
  }

  // --- BLOQUE 3: Cuentas ---
  getAccounts(): Observable<FinancialAccount[]> {
    return this.http.get<FinancialAccount[]>(`${this.apiUrl}/accounts`);
  }

  updateAccountBalance(id: number, newBalance: number): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/accounts/${id}`, { balance: newBalance });
  }
}