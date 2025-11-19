import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environments';
import { MonthlyStatisticsDTO } from '../../dtos/statistics/MonthlyStatisticsDTO';

@Injectable({
  providedIn: 'root'
})
export class StatisticsService {
  private apiUrl = `${environment.apiUrl}/Statistics`;

  constructor(private http: HttpClient) {}

  /**
   * Obtiene las estadísticas para un mes y año específicos.
   */
  getMonthlyStatistics(year: number, month: number): Observable<MonthlyStatisticsDTO> {
    const params = new HttpParams()
      .set('year', year.toString())
      .set('month', month.toString());

    return this.http.get<MonthlyStatisticsDTO>(`${this.apiUrl}/monthly`, { params });
  }
}