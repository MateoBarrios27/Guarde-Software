import { Injectable } from '@angular/core';
import { LockerType } from '../../models/locker-type';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environments';
import { UpdateLockerTypeDto } from '../../dtos/lockerType/updateLockerTypeDto';
import { CreateLockerTypeDto } from '../../dtos/lockerType/CreateLockerTypeDto';

@Injectable({
  providedIn: 'root'
})
export class LockerTypeService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getLockerTypes(): Observable<LockerType[]>{
        return this.httpCliente.get<LockerType[]>(`${this.url}/LockerType`);
  }

  public getLockerTypeById(id: number): Observable<LockerType>{
        return this.httpCliente.get<LockerType>(`${this.url}/LockerType/${id}`);
  }

  //CAMBIAR A DTO
  public createLockerType(lockerType: CreateLockerTypeDto): Observable<LockerType>{
      return this.httpCliente.post<LockerType>(`${this.url}/LockerType`,lockerType);
  }

  public updateLockerType(id: number, lockerType: UpdateLockerTypeDto): Observable<boolean>{
      return this.httpCliente.put<boolean>(`${this.url}/LockerType/${id}`,lockerType);
  }

  public deleteLockerType(id: number): Observable<boolean>{
      return this.httpCliente.delete<boolean>(`${this.url}/LockerType/${id}`);
  }
}
