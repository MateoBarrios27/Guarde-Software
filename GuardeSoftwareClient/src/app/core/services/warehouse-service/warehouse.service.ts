import { Injectable } from '@angular/core';
import { Warehouse } from '../../models/warehouse';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environments';
import { CreateWarehouseDTO } from '../../dtos/warehouse/CreateWarehouseDTO';

@Injectable({
  providedIn: 'root'
})
export class WarehouseService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getWarehouses(): Observable<Warehouse[]>{
        return this.httpCliente.get<Warehouse[]>(`${this.url}/Warehouse`);
  }

   public getWarehouseById(id: number): Observable<Warehouse>{
        return this.httpCliente.get<Warehouse>(`${this.url}/Warehouse/${id}`);
  }

  public deleteWarehouse(id: number): Observable<any>{
        return this.httpCliente.delete<any>(`${this.url}/Warehouse/${id}`);
  }

  public createWarehouse(createWarehouseDto: CreateWarehouseDTO): Observable<Warehouse>{
        return this.httpCliente.post<Warehouse>(`${this.url}/Warehouse`, createWarehouseDto);
  }
  
}
