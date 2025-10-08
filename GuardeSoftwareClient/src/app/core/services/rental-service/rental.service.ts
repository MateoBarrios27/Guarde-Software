import { Injectable } from '@angular/core';
import { rental } from '../../models/rental';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environmets';
import { PendingRentalDTO } from '../../dtos/rental/PendingRentalDTO';

@Injectable({
  providedIn: 'root'
})
export class RentalService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getRentals(): Observable<rental[]>{
        return this.httpCliente.get<rental[]>(`${this.url}/Rental`);
  }

   public getRentalById(id: number): Observable<rental>{
        return this.httpCliente.get<rental>(`${this.url}/Rental/${id}`);
  }

  public getPendingRentals(): Observable<PendingRentalDTO[]>{
        return this.httpCliente.get<PendingRentalDTO[]>(`${this.url}/Rental/Pending`);
  }

  public deleteRental(id: number): Observable<any>{
        return this.httpCliente.delete<any>(`${this.url}/Rental/${id}`);
  }
}
