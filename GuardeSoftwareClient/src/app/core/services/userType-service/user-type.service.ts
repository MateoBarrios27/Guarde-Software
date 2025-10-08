import { Injectable } from '@angular/core';
import { UserType } from '../../models/user-type';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environmets';
import { CreateUserTypeDTO } from '../../dtos/userType/CreateUserTypeDTO';

@Injectable({
  providedIn: 'root'
})
export class UserTypeService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getUserTypes(): Observable<UserType[]>{
        return this.httpCliente.get<UserType[]>(`${this.url}/UserType`);
  }

  public getUserTypeById(id: number): Observable<UserType>{
        return this.httpCliente.get<UserType>(`${this.url}/UserType/${id}`);
  }

  public deleteUserType(id: number): Observable<any>{
        return this.httpCliente.delete<any>(`${this.url}/UserType/${id}`);
  }

  public createUserType(createUserTypeDto: CreateUserTypeDTO): Observable<UserType>{
      return this.httpCliente.post<UserType>(`${this.url}/UserType`, createUserTypeDto);
  }
    
}
