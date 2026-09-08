import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../../environments/environments';
import { BillingType } from '../../models/billing-type.model';
import { CreateBillingTypeDTO } from '../../dtos/billingType/create-billing-type.dto';
import { UpdateBillingTypeDTO } from '../../dtos/billingType/update-billing-type.dto';
import { DataRefreshService } from '../data-refresh-service/data-refresh.service';

@Injectable({
  providedIn: 'root'
})
export class BillingTypeService {

  private apiUrl = `${environment.apiUrl}/BillingType`;

  constructor(
    private http: HttpClient,
    private dataRefresh: DataRefreshService,
  ) { }

  getBillingTypes(): Observable<BillingType[]> {
    return this.http.get<BillingType[]>(this.apiUrl);
  }

  createBillingType(dto: CreateBillingTypeDTO): Observable<BillingType> {
    return this.http.post<BillingType>(this.apiUrl, dto).pipe(
      tap(() => this.dataRefresh.notify('catalog')),
    );
  }

  updateBillingType(id: number, dto: UpdateBillingTypeDTO): Observable<any> {
    return this.http.put(`${this.apiUrl}/${id}`, dto).pipe(
      tap(() => this.dataRefresh.notify('catalog')),
    );
  }

  deleteBillingType(id: number): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${id}`).pipe(
      tap(() => this.dataRefresh.notify('catalog')),
    );
  }
}
