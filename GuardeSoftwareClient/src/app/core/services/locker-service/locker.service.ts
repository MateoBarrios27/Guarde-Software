import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environmets';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Locker } from '../../models/locker';
import { LockerUpdateDTO } from '../../dtos/locker/LockerUpdateDTO';
import { LockerUpdateStatusDTO } from '../../dtos/locker/LockerUpdateStatusDTO';
import { CreateLockerDTO } from '../../dtos/locker/CreateLockerDTO';

@Injectable({
  providedIn: 'root'
})
export class LockerService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getLockers(): Observable<Locker[]>{
    return this.httpCliente.get<Locker[]>(`${this.url}/Locker`);
  }

  public getLockerById(id: number): Observable<Locker>{
    return this.httpCliente.get<Locker>(`${this.url}/Locker/${id}`);
  }

  public updateLocker(id: number, dto: LockerUpdateDTO): Observable<any>{
     return this.httpCliente.put<any>(`${this.url}/Locker/${id}`, dto);
  }

  public updateLockerStatus(id: number, dto: LockerUpdateStatusDTO): Observable<any>{
    return this.httpCliente.patch<any>(`${this.url}/Locker/${id}`, dto);
  }

  public deleteLocker(id: number): Observable<any> {
  return this.httpCliente.delete<any>(`${this.url}/Locker/${id}`);
  }

  public createLocker(createLockerDto: CreateLockerDTO): Observable<Locker> {
    return this.httpCliente.post<Locker>(`${this.url}/locker`, createLockerDto);
  }
}
