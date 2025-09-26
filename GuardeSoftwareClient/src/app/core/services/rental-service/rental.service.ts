import { Injectable } from '@angular/core';
import { rental } from '../../models/rental';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environmets';

@Injectable({
  providedIn: 'root'
})
export class RentalService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getRental(): Observable<rental[]>{
        return this.httpCliente.get<rental[]>(`${this.url}/Rental`);
  }
}
