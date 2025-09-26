import { Injectable } from '@angular/core';
import { Warehouse } from '../../models/warehouse';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environmets';

@Injectable({
  providedIn: 'root'
})
export class WarehouseService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getWarehouse(): Observable<Warehouse[]>{
        return this.httpCliente.get<Warehouse[]>(`${this.url}/Warehouse`);
  }
}
