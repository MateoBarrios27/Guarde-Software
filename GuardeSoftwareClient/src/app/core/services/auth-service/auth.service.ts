import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { environment } from '../../../../environments/environments';
import { WorkspaceRouteReuseStrategy } from '../../routing/workspace-route-reuse.strategy';

interface LoginRequest {
  emailOrUserName: string;
  password: string;
}

interface AuthResponse {
  token: string;
  expiresAt: string;
  userId: number;
  userTypeId: number;
  userTypeName: string;
  userName: string;
  firstName: string;
  lastName: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private url: string = environment.apiUrl;
  private expirationTimer: any;
  private isShowingAlert = false;

  constructor(
    private http: HttpClient,
    private router: Router,
    private workspaceRouteReuse: WorkspaceRouteReuseStrategy,
  ) {
    this.autoLogoutOnTokenExpiration();
  }

  login(emailOrUserName: string, password: string) {
    const body: LoginRequest = { emailOrUserName, password };

    return this.http.post<AuthResponse>(`${this.url}/Auth/login`, body).pipe(
      tap(res => {
        this.workspaceRouteReuse.startSession();
        localStorage.setItem('authToken', res.token);
        localStorage.setItem('expiresAt', res.expiresAt);
        localStorage.setItem('userName', res.userName);
        localStorage.setItem('firstName', res.firstName);
        localStorage.setItem('lastName', res.lastName);
        localStorage.setItem('userTypeId', res.userTypeId.toString());
        localStorage.setItem('userTypeName', res.userTypeName);
        this.autoLogoutOnTokenExpiration();
      })
    );
  }

  logout() {
    this.workspaceRouteReuse.clearForLogout();

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
    localStorage.removeItem('userTypeName');
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
    const typeId = this.getTokenClaim('businessUserTypeId') ?? localStorage.getItem('userTypeId');
    return typeId === '1'; 
  }

  isObserver(): boolean {
    const typeName = this.getTokenClaim('businessUserTypeName') ?? localStorage.getItem('userTypeName');
    return typeName?.trim().toLocaleLowerCase('es-AR') === 'observador';
  }

  private getTokenClaim(claimName: string): string | null {
    const token = this.getToken();
    if (!token) return null;

    try {
      const encodedPayload = token.split('.')[1];
      if (!encodedPayload) return null;

      const base64 = encodedPayload.replace(/-/g, '+').replace(/_/g, '/');
      const paddedBase64 = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=');
      const payload = JSON.parse(atob(paddedBase64)) as Record<string, unknown>;
      const claimValue = payload[claimName];
      return typeof claimValue === 'string' ? claimValue : null;
    } catch {
      return null;
    }
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

  public async triggerExpirationAlert(): Promise<void> {
    if (this.isShowingAlert) return;

    if (!this.getToken()) return;

    this.isShowingAlert = true;
    this.logout();

    if (this.router.url.includes('/login')) {
      this.isShowingAlert = false;
      return;
    }

    const { default: Swal } = await import('../../../shared/services/ui-alert.service');
    await Swal.fire({
      title: 'Sesión expirada',
      text: 'Tu sesión ha expirado por inactividad o límite de tiempo. Por favor, inicia sesión nuevamente.',
      icon: 'warning',
      confirmButtonText: 'Aceptar',
      confirmButtonColor: '#3085d6',
      allowOutsideClick: false
    });
    this.isShowingAlert = false;
    await this.router.navigate(['/login']);
  }
}
