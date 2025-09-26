import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environmets';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Locker } from '../../models/locker';

@Injectable({
  providedIn: 'root'
})
export class LockerService {

  private url: string = environment.apiUrl
  constructor(private httpCliente: HttpClient) { }

  public getLockers(): Observable<Locker[]>{
    return this.httpCliente.get<Locker[]>(`${this.url}/Locker`);
  }

}
