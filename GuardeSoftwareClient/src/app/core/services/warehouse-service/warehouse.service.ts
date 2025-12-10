import { Injectable } from '@angular/core';
import { Warehouse } from '../../models/warehouse';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environments';
import { CreateWarehouseDto } from '../../dtos/warehouse/CreateWarehouseDto';
import { UpdateWarehouseDto } from '../../dtos/warehouse/UpdateWarehouseDto';

@Injectable({
  providedIn: 'root'
})
export class WarehouseService {

  private url: string = environment.apiUrl
  constructor(private httpClient: HttpClient) { }

  getWarehouses(): Observable<Warehouse[]>{
        return this.httpClient.get<Warehouse[]>(`${this.url}/Warehouse`);
  }

   getWarehouseById(id: number): Observable<Warehouse>{
        return this.httpClient.get<Warehouse>(`${this.url}/Warehouse/${id}`);
  }

  createWarehouse(dto: CreateWarehouseDto): Observable<Warehouse> {
    return this.httpClient.post<Warehouse>(`${this.url}/Warehouse`, dto);
  }

  updateWarehouse(id: number, dto: UpdateWarehouseDto): Observable<any> {
    return this.httpClient.put(`${this.url}/Warehouse/${id}`, dto);
  }

  deleteWarehouse(id: number): Observable<any> {
    return this.httpClient.delete(`${this.url}/Warehouse/${id}`);
  }
  
}
