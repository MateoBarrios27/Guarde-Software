import { Injectable } from '@angular/core';
import { BehaviorSubject, fromEvent, merge, of } from 'rxjs';
import { map } from 'rxjs/operators';

@Injectable({ providedIn: 'root' })
export class OfflineService {
  private _isOnline = new BehaviorSubject<boolean>(navigator.onLine);

  /** Observable that emits true when online, false when offline */
  readonly isOnline$ = this._isOnline.asObservable();

  get isOnline(): boolean {
    return this._isOnline.getValue();
  }

  constructor() {
    merge(
      of(navigator.onLine),
      fromEvent(window, 'online').pipe(map(() => true)),
      fromEvent(window, 'offline').pipe(map(() => false))
    ).subscribe(status => this._isOnline.next(status as boolean));
  }
}
