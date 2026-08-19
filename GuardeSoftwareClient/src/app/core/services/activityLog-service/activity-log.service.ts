import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environments';
import { ActivityLog, ActivityLogFilter, ActivityLogPage, ActivityLogUser } from '../../models/activity-log';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class ActivityLogService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getActivityLogs(filter: ActivityLogFilter): Observable<ActivityLogPage>{
        let params = new HttpParams()
          .set('pageNumber', String(filter.pageNumber))
          .set('pageSize', String(filter.pageSize));

        if (filter.area) params = params.set('area', filter.area);
        if (filter.action) params = params.set('action', filter.action);
        if (filter.userId) params = params.set('userId', String(filter.userId));
        if (filter.fromDate) params = params.set('fromDate', filter.fromDate);
        if (filter.toDate) params = params.set('toDate', filter.toDate);
        if (filter.search?.trim()) params = params.set('search', filter.search.trim());

        return this.httpCliente.get<ActivityLogPage>(`${this.url}/ActivityLog`, { params });
  }

  public getActivityLogUsers(): Observable<ActivityLogUser[]> {
        return this.httpCliente.get<ActivityLogUser[]>(`${this.url}/ActivityLog/users`);
  }

  public getActivityLogById(id: number): Observable<ActivityLog[]>{
        return this.httpCliente.get<ActivityLog[]>(`${this.url}/ActivityLog/${id}`);
  }

  public deleteActivityLog(id: number): Observable<any>{
        return this.httpCliente.delete<any>(`${this.url}/ActivityLog/${id}`);
  }
}
