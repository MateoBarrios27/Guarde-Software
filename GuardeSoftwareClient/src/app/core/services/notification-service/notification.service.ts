import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environments';
import { NotificationInbox } from '../../models/notification';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly url = `${environment.apiUrl}/Notification`;

  constructor(private readonly http: HttpClient) {}

  getInbox(take = 50): Observable<NotificationInbox> {
    return this.http.get<NotificationInbox>(this.url, { params: { take } });
  }

  markAsRead(notificationId: number): Observable<void> {
    return this.http.put<void>(`${this.url}/${notificationId}/read`, {});
  }

  markAllAsRead(): Observable<{ updated: number }> {
    return this.http.put<{ updated: number }>(`${this.url}/read-all`, {});
  }
}
