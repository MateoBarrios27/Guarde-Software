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

  public getActivityLog(): Observable<ActivityLog[]>{
        return this.httpCliente.get<ActivityLog[]>(`${this.url}/ActivityLog`);
  }
}
