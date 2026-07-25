import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, NavigationEnd, ActivatedRoute, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs/operators';
import { SidebarComponent } from './shared/components/sidebar/sidebar.component';
import { ScrollingModule } from '@angular/cdk/scrolling';
import { AlertService } from './core/services/alert-service/alert.service';
import { SystemAlertModalComponent } from './shared/components/system-alert-modal/system-alert-modal.component';
import { AuthService } from './core/services/auth-service/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    SidebarComponent,
    ScrollingModule,
    SystemAlertModalComponent
  ],
  templateUrl: './app.component.html',
})
export class AppComponent implements OnInit, OnDestroy {
  isSidebarOpen = false;
  pageTitle = '';
  isLoginRoute = false;

  constructor(
    private router: Router,
    private activatedRoute: ActivatedRoute,
    private alertService: AlertService,
    public authService: AuthService
  ) {}

  ngOnInit() {
    // Escuchar cambios de ruta para actualizar el título
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd),
      map(() => {
        let route = this.activatedRoute;
        while (route.firstChild) {
          route = route.firstChild;
        }
        return route;
      }),
      filter(route => route.outlet === 'primary'),
      map(route => route.snapshot.data['title'])
    ).subscribe(title => {
      this.pageTitle = title || 'Dashboard';
    });

    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe((event: NavigationEnd) => {
        const url = event.urlAfterRedirects || event.url;
        this.isLoginRoute = url.startsWith('/login');

        // Iniciar SignalR cuando el usuario ya está en una ruta autenticada
        if (!this.isLoginRoute && this.authService.isLoggedIn()) {
          this.alertService.startConnection();
        }
      });
  }

  toggleSidebar() {
    this.isSidebarOpen = !this.isSidebarOpen;
  }

  closeSidebar() {
    this.isSidebarOpen = false;
  }

  ngOnDestroy() {
    this.alertService.stopConnection();
  }
}