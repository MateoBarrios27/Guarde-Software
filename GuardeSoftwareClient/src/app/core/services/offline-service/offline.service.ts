import { Injectable } from '@angular/core';
import { BehaviorSubject, fromEvent, merge } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class OfflineService {
  private network = navigator.onLine;
  private apiAvailable = true;
  private manual = false;
  private readonly onlineSubject = new BehaviorSubject<boolean>(navigator.onLine);
  readonly isOnline$ = this.onlineSubject.asObservable();
  get isOnline(): boolean { return this.onlineSubject.value; }
  get networkOnline(): boolean { return this.network; }
  get manualMode(): boolean { return this.manual; }
  get reason(): string {
    return this.manual ? 'Modo local elegido' : !this.network ? 'Sin conexión a Internet' : 'Servidor no disponible';
  }
  constructor() {
    try { this.manual = sessionStorage.getItem('guarde-local-mode') === 'true'; } catch {}
    this.publish();
    merge(fromEvent(window, 'online'), fromEvent(window, 'offline')).subscribe(() => {
      this.network = navigator.onLine;
      if (!this.network) this.apiAvailable = false;
      this.publish();
    });
  }
  reportApiAvailability(available: boolean): void { this.apiAvailable = available; this.publish(); }
  setManualMode(enabled: boolean): void {
    this.manual = enabled;
    try { sessionStorage.setItem('guarde-local-mode', String(enabled)); } catch {}
    this.publish();
  }
  private publish(): void {
    const value = this.network && this.apiAvailable && !this.manual;
    if (value !== this.onlineSubject.value) this.onlineSubject.next(value);
  }
}
