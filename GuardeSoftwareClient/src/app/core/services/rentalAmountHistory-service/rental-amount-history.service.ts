import { Injectable } from '@angular/core';
import { RentalAmountHistory } from '../../models/rental-amount-history';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environments';

@Injectable({
  providedIn: 'root'
})
export class RentalAmountHistoryService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getRentalAmountHistories(): Observable<RentalAmountHistory[]>{
        return this.httpCliente.get<RentalAmountHistory[]>(`${this.url}/RentalAmountHistory`);
  }

   public getRentalAmountHistoryByRental(id: number): Observable<RentalAmountHistory>{
        return this.httpCliente.get<RentalAmountHistory>(`${this.url}/RentalAmountHistory/ByRental/${id}`);
  }


  //CAMBIAR POR DTO LUEGO DE HACERLO EN BACK
  public createRentalAmountHistory(rentalAmountHist: RentalAmountHistory):Observable<RentalAmountHistory>{
      return this.httpCliente.post<RentalAmountHistory>(`${this.url}/RentalAmountHistory`, rentalAmountHist);
  }
  
}
