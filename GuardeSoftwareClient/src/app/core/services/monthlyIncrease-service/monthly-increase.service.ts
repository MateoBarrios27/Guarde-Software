import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map, tap } from 'rxjs';
import { environment } from '../../../../environments/environments';
import { MonthlyIncreaseSetting } from '../../models/monthly-increase-setting';
import { CreateMonthlyIncreaseDto } from '../../dtos/monthlyIncrease/CreateMonthlyIncreaseDto';
import { UpdateMonthlyIncreaseDto } from '../../dtos/monthlyIncrease/UpdateMonthlyIncreaseDto';
import { DataRefreshService } from '../data-refresh-service/data-refresh.service';

@Injectable({
  providedIn: 'root'
})
export class MonthlyIncreaseService {

  // Asumimos un nuevo controlador en el backend
  private apiUrl = `${environment.apiUrl}/MonthlyIncrease`; 

  constructor(
    private http: HttpClient,
    private dataRefresh: DataRefreshService,
  ) { }

  // Mapear snake_case a camelCase si es necesario
  private mapSetting(setting: any): MonthlyIncreaseSetting {
    return {
      id: setting.increase_setting_id || setting.id,
      effectiveDate: new Date(setting.effectiveDate),
      percentage: setting.percentage,
      createdAt: setting.createdAt ? new Date(setting.createdAt) : undefined,
    };
  }

  getSettings(): Observable<MonthlyIncreaseSetting[]> {
    return this.http.get<any[]>(this.apiUrl).pipe(
      map(response => response.map(this.mapSetting))
    );
  }

  getPercentageForMonth(year: number, month: number): Observable<number | null> {
    const monthKey = `${year}-${String(month).padStart(2, '0')}`;
    return this.http.get<{ effectiveDate: string; percentage: number }[]>(this.apiUrl).pipe(
      map(settings => {
        // A configured month is a calendar period, not a timestamp in the browser's timezone.
        const setting = settings.find(item => item.effectiveDate?.slice(0, 7) === monthKey);
        const percentage = Number(setting?.percentage);
        return setting && Number.isFinite(percentage) && percentage >= 0 ? percentage : null;
      })
    );
  }

  createSetting(dto: CreateMonthlyIncreaseDto): Observable<MonthlyIncreaseSetting> {
    return this.http.post<any>(this.apiUrl, dto).pipe(
      map(this.mapSetting),
      tap(() => this.dataRefresh.notify('catalog')),
    );
  }

  updateSetting(id: number, dto: UpdateMonthlyIncreaseDto): Observable<any> {
    return this.http.put(`${this.apiUrl}/${id}`, dto).pipe(
      tap(() => this.dataRefresh.notify('catalog')),
    );
  }

  deleteSetting(id: number): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${id}`).pipe(
      tap(() => this.dataRefresh.notify('catalog')),
    );
  }
}
