import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environments';
import { MonthlyIncreaseSetting } from '../../models/monthly-increase-setting';
import { CreateMonthlyIncreaseDto } from '../../dtos/monthlyIncrease/CreateMonthlyIncreaseDto';
import { UpdateMonthlyIncreaseDto } from '../../dtos/monthlyIncrease/UpdateMonthlyIncreaseDto';

@Injectable({
  providedIn: 'root'
})
export class MonthlyIncreaseService {

  // Asumimos un nuevo controlador en el backend
  private apiUrl = `${environment.apiUrl}/MonthlyIncrease`; 

  constructor(private http: HttpClient) { }

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

  createSetting(dto: CreateMonthlyIncreaseDto): Observable<MonthlyIncreaseSetting> {
    return this.http.post<any>(this.apiUrl, dto).pipe(
      map(this.mapSetting)
    );
  }

  updateSetting(id: number, dto: UpdateMonthlyIncreaseDto): Observable<any> {
    return this.http.put(`${this.apiUrl}/${id}`, dto);
  }

  deleteSetting(id: number): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${id}`);
  }
}