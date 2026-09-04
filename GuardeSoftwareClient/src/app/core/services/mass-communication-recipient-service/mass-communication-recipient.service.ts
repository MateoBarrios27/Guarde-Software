import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environments';
import {
  MassCommunicationRecipient,
  MassCommunicationRecipientImportResult,
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

  import(
    file: File,
    type: string,
    reactivateInactive: boolean,
    dryRun: boolean
  ): Observable<MassCommunicationRecipientImportResult> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    formData.append('type', type);
    formData.append('reactivateInactive', reactivateInactive ? 'true' : 'false');
    formData.append('dryRun', dryRun ? 'true' : 'false');

    return this.http.post<MassCommunicationRecipientImportResult>(
      this.apiUrl + '/import',
      formData
    );
  }
}
