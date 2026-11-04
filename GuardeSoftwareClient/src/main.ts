import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';

// Safari de iPhone ignora `user-scalable=no` en el navegador y sigue permitiendo alejar la página con el pellizco
// (se ve la app chica sobre el fondo). Sus eventos `gesture*` sí se pueden cancelar.
for (const type of ['gesturestart', 'gesturechange', 'gestureend']) {
  document.addEventListener(type, event => event.preventDefault(), { passive: false });
}

bootstrapApplication(AppComponent, appConfig)
  .catch((err) => console.error(err));
