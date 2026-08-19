import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environments';
import {
  MassCommunicationRecipient,
  UpsertMassCommunicationRecipient
} from '../../models/mass-communication-recipient';

@Injectable({
  providedIn: 'root'
})
export class MassCommunicationRecipientService {
  private readonly apiUrl = environment.apiUrl + '/MassCommunicationRecipients';

  constructor(private http: HttpClient) {}

  getAll(): Observable<MassCommunicationRecipient[]> {
    return this.http.get<MassCommunicationRecipient[]>(this.apiUrl);
  }

  create(dto: UpsertMassCommunicationRecipient): Observable<MassCommunicationRecipient> {
    return this.http.post<MassCommunicationRecipient>(this.apiUrl, dto);
  }

  update(id: number, dto: UpsertMassCommunicationRecipient): Observable<MassCommunicationRecipient> {
    return this.http.put<MassCommunicationRecipient>(this.apiUrl + '/' + id, dto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(this.apiUrl + '/' + id);
  }
}
