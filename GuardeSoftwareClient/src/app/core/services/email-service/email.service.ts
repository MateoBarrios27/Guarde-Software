import { Injectable } from '@angular/core';
import { Email } from '../../models/email';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environmets';

@Injectable({
  providedIn: 'root'
})
export class EmailService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getEmail(): Observable<Email[]>{
        return this.httpCliente.get<Email[]>(`${this.url}/Email`);
  }
}
