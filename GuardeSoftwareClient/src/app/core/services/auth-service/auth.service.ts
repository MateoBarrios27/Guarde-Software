import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { environment } from '../../../../environments/environments';
import Swal from 'sweetalert2';

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
  private url: string = environment.apiUrl;
  private expirationTimer: any;
  private isShowingAlert = false;

  constructor(private http: HttpClient, private router: Router) {
    this.autoLogoutOnTokenExpiration();
  }

  login(emailOrUserName: string, password: string) {
    const body: LoginRequest = { emailOrUserName, password };

    return this.http.post<AuthResponse>(`${this.url}/Auth/login`, body).pipe(
      tap(res => {
        localStorage.setItem('authToken', res.token);
        localStorage.setItem('expiresAt', res.expiresAt);
        localStorage.setItem('userName', res.userName);
        localStorage.setItem('firstName', res.firstName);
        localStorage.setItem('lastName', res.lastName);
        localStorage.setItem('userTypeId', res.userTypeId.toString());
        this.autoLogoutOnTokenExpiration();
      })
    );
  }

  logout() {
    if (this.expirationTimer) {
      clearTimeout(this.expirationTimer);
      this.expirationTimer = null;
    }
    localStorage.removeItem('authToken');
    localStorage.removeItem('expiresAt');
    localStorage.removeItem('userName');
    localStorage.removeItem('firstName');
    localStorage.removeItem('lastName');
    localStorage.removeItem('userTypeId'); 
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

  isAdmin(): boolean {
    const typeId = localStorage.getItem('userTypeId');
    return typeId === '1'; 
  }

  public autoLogoutOnTokenExpiration() {
    if (this.expirationTimer) {
      clearTimeout(this.expirationTimer);
      this.expirationTimer = null;
    }

    const token = this.getToken();
    const exp = localStorage.getItem('expiresAt');
    if (!token || !exp) return;

    const remainingTime = new Date(exp).getTime() - new Date().getTime();
    if (remainingTime <= 0) {
      this.triggerExpirationAlert();
      return;
    }

    this.expirationTimer = setTimeout(() => {
      this.triggerExpirationAlert();
    }, remainingTime);
  }

  public triggerExpirationAlert() {
    if (this.isShowingAlert) return;

    if (!this.getToken()) return;

    this.isShowingAlert = true;
    this.logout();

    if (this.router.url.includes('/login')) {
      this.isShowingAlert = false;
      return;
    }

    Swal.fire({
      title: 'Sesión expirada',
      text: 'Tu sesión ha expirado por inactividad o límite de tiempo. Por favor, inicia sesión nuevamente.',
      icon: 'warning',
      confirmButtonText: 'Aceptar',
      confirmButtonColor: '#3085d6',
      allowOutsideClick: false
    }).then(() => {
      this.isShowingAlert = false;
      this.router.navigate(['/login']);
    });
  }
}
