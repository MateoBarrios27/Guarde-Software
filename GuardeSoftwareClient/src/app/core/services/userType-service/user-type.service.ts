import { Injectable } from '@angular/core';
import { UserType } from '../../models/user-type';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environmets';

@Injectable({
  providedIn: 'root'
})
export class UserTypeService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getUserType(): Observable<UserType[]>{
        return this.httpCliente.get<UserType[]>(`${this.url}/UserType`);
  }
}
