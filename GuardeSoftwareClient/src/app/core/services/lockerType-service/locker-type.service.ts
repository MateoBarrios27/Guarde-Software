import { Injectable } from '@angular/core';
import { LockerType } from '../../models/locker-type';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environments';

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
  public createLockerType(lockerType: LockerType): Observable<LockerType>{
      return this.httpCliente.post<LockerType>(`${this.url}/LockerType`,lockerType);
  }
}
