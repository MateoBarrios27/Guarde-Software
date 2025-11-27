import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { tap } from 'rxjs';
import { environment } from '../../../../environments/environments';

interface LoginRequest {
  emailOrUserName: string;
  password: string;
}

interface AuthResponse {
  token: string;
  expiresAt: string;
  userId: number;
  userTypeId: number;
  userName: string;
  firstName: string;
  lastName: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private url: string = environment.apiUrl

  constructor(private http: HttpClient) {}

  login(emailOrUserName: string, password: string) {
    const body: LoginRequest = { emailOrUserName, password };

    return this.http.post<AuthResponse>(`${this.url}/Auth/login`, body).pipe(
      tap(res => {
        localStorage.setItem('authToken', res.token);
        localStorage.setItem('expiresAt', res.expiresAt);
        localStorage.setItem('userName', res.userName);
        localStorage.setItem('firstName', res.firstName);
        localStorage.setItem('lastName', res.lastName);
      })
    );
  }

  logout() {
    localStorage.removeItem('authToken');
    localStorage.removeItem('expiresAt');
  }

  getToken(): string | null {
    return localStorage.getItem('authToken');
  }

  isLoggedIn(): boolean {
    const token = this.getToken();
    if (!token) return false;

    const exp = localStorage.getItem('expiresAt');
    if (!exp) return false;

    return new Date() < new Date(exp);
  }
}
