import { Injectable } from '@angular/core';
import { User } from '../../models/user';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environmets';

@Injectable({
  providedIn: 'root'
})
export class UserService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getUser(): Observable<User[]>{
        return this.httpCliente.get<User[]>(`${this.url}/User`);
  }
}
