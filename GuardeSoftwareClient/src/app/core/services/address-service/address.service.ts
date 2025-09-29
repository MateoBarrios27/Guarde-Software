import { Injectable } from '@angular/core';
import { Address } from '../../models/address';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environmets';

@Injectable({
  providedIn: 'root'
})
export class AddressService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getAddress(): Observable<Address[]>{
        return this.httpCliente.get<Address[]>(`${this.url}/Address`);
  }

   public getAddressById(id: number): Observable<Address>{
        return this.httpCliente.get<Address>(`${this.url}/Address/${id}`);
  }
}
