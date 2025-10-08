import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environmets';
import { ActivityLog } from '../../models/activity-log';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class ActivityLogService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getActivityLogs(): Observable<ActivityLog[]>{
        return this.httpCliente.get<ActivityLog[]>(`${this.url}/ActivityLog`);
  }
  
  public getActivityLogById(id: number): Observable<ActivityLog>{
        return this.httpCliente.get<ActivityLog>(`${this.url}/ActivityLog/${id}`);
  }

  public deleteActivityLog(id: number): Observable<any>{
        return this.httpCliente.delete<any>(`${this.url}/ActivityLog/${id}`);
  }
}
