import { Injectable } from '@angular/core';
import { UserType } from '../../models/user-type';

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
