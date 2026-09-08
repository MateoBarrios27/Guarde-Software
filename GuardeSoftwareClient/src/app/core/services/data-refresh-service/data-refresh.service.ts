import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, Subject } from 'rxjs';
import { filter } from 'rxjs/operators';

export type DataRefreshDomain =
  | 'clients'
  | 'finances'
  | 'lockers'
  | 'catalog'
  | 'communications'
  | 'cash'
  | 'all';

export interface DataRefreshEvent {
  domains: readonly DataRefreshDomain[];
  /** Ruta que originó la mutación. null significa un cambio externo. */
  sourceRoute: string | null;
  emittedAt: number;
}

/**
 * Coordina la invalidación de datos entre pantallas conservadas por
 * WorkspaceRouteReuseStrategy.
 *
 * Los servicios de dominio publican un evento sólo después de que una
 * mutación termina correctamente. Las pantallas observan únicamente los
 * dominios que muestran y pueden ignorar el evento de la ruta que originó la
 * acción porque esa pantalla ya actualiza su estado local.
 */
@Injectable({ providedIn: 'root' })
export class DataRefreshService {
  private readonly changesSubject = new Subject<DataRefreshEvent>();
  readonly changes$ = this.changesSubject.asObservable();

  constructor(private readonly router: Router) {}

  notify(
    domains: DataRefreshDomain | readonly DataRefreshDomain[],
    sourceRoute?: string | null,
  ): void {
    const normalizedDomains = [...new Set(
      typeof domains === 'string' ? [domains] : domains,
    )];

    if (normalizedDomains.length === 0) return;

    this.changesSubject.next({
      domains: normalizedDomains,
      sourceRoute: sourceRoute === undefined ? this.getCurrentRoute() : sourceRoute,
      emittedAt: Date.now(),
    });
  }

  watch(
    domains: readonly DataRefreshDomain[],
    consumerRoute?: string,
  ): Observable<DataRefreshEvent> {
    const watchedDomains = new Set(domains);

    return this.changes$.pipe(
      filter(event => event.domains.some(domain =>
        domain === 'all' || watchedDomains.has(domain),
      )),
      filter(event => consumerRoute === undefined || event.sourceRoute !== consumerRoute),
    );
  }

  private getCurrentRoute(): string | null {
    const path = this.router.url.split(/[?#]/, 1)[0];
    return path.split('/').filter(Boolean)[0] ?? null;
  }
}
