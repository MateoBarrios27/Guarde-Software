import { Injectable } from '@angular/core';
import { User } from '../../models/user';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environments';
import { CreateUserDTO } from '../../dtos/user/CreateUserDTO';
import { UpdateUserDTO } from '../../dtos/user/UpdateUserDTO';
 
@Injectable({
  providedIn: 'root'
})
export class UserService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getUsers(): Observable<User[]>{
        return this.httpCliente.get<User[]>(`${this.url}/User`);
  }

  public getUserById(id: number): Observable<User>{
      return this.httpCliente.get<User>(`${this.url}/User/${id}`);
  }

   public deleteUser(id: number): Observable<any>{
      return this.httpCliente.delete<any>(`${this.url}/User/${id}`);
  }

  public createUser(createUserDto: CreateUserDTO): Observable<User>{
    return this.httpCliente.post<User>(`${this.url}/User`,createUserDto);
  }

  public updateUser(id: number, dto: UpdateUserDTO): Observable<any>{
    return this.httpCliente.put<any>(`${this.url}/User/${id}`, dto);
  }

}
