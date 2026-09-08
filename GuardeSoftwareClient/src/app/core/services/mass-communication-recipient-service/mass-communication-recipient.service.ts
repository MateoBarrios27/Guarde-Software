import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../../environments/environments';
import {
  MassCommunicationRecipient,
  MassCommunicationRecipientImportResult,
  UpsertMassCommunicationRecipient
} from '../../models/mass-communication-recipient';
import { DataRefreshService } from '../data-refresh-service/data-refresh.service';

@Injectable({
  providedIn: 'root'
})
export class MassCommunicationRecipientService {
  private readonly apiUrl = environment.apiUrl + '/MassCommunicationRecipients';

  constructor(
    private http: HttpClient,
    private dataRefresh: DataRefreshService,
  ) {}

  getAll(): Observable<MassCommunicationRecipient[]> {
    return this.http.get<MassCommunicationRecipient[]>(this.apiUrl);
  }

  create(dto: UpsertMassCommunicationRecipient): Observable<MassCommunicationRecipient> {
    return this.http.post<MassCommunicationRecipient>(this.apiUrl, dto).pipe(
      tap(() => this.dataRefresh.notify(['communications', 'catalog'])),
    );
  }

  update(id: number, dto: UpsertMassCommunicationRecipient): Observable<MassCommunicationRecipient> {
    return this.http.put<MassCommunicationRecipient>(this.apiUrl + '/' + id, dto).pipe(
      tap(() => this.dataRefresh.notify(['communications', 'catalog'])),
    );
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(this.apiUrl + '/' + id).pipe(
      tap(() => this.dataRefresh.notify(['communications', 'catalog'])),
    );
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
    ).pipe(
      tap(result => {
        if (!dryRun && result) {
          this.dataRefresh.notify(['communications', 'catalog']);
        }
      }),
    );
  }
}
