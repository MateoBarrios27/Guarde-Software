import { ApplicationConfig, LOCALE_ID, provideZoneChangeDetection, isDevMode } from '@angular/core';
import { provideRouter, RouteReuseStrategy } from '@angular/router';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http'; 
import { HTTP_INTERCEPTORS } from '@angular/common/http';

import { routes } from './app.routes';
import { AuthInterceptor } from './core/interceptors/auth.interceptor';
import { registerLocaleData } from '@angular/common';
import localeEsAr from '@angular/common/locales/es-AR';
import { provideServiceWorker } from '@angular/service-worker';
import { WorkspaceRouteReuseStrategy } from './core/routing/workspace-route-reuse.strategy';

registerLocaleData(localeEsAr, 'es-AR');

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    WorkspaceRouteReuseStrategy,
    { provide: RouteReuseStrategy, useExisting: WorkspaceRouteReuseStrategy },
    { provide: LOCALE_ID, useValue: 'es-AR' },
    provideHttpClient(withInterceptorsFromDi()),

    {
      provide: HTTP_INTERCEPTORS,
      useClass: AuthInterceptor,
      multi: true,
    }, provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      // Register the shell before the user can lose the connection and
      // reload a deep route while offline.
      registrationStrategy: 'registerImmediately'
    }),
  ],
};
