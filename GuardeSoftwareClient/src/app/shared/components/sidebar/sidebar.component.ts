import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { IconComponent } from '../icon/icon.component';
import { AuthService } from '../../../core/services/auth-service/auth.service';

interface MenuItem {
  path: string;
  title: string;
  icon: string;
}

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule, IconComponent],
  templateUrl: './sidebar.component.html',
})
export class SidebarComponent {

  constructor( public authService: AuthService, private router: Router){}

  userName = '';
  firstName = '';
  lastName = '';
  
  @Input() isOpen = false;
  @Output() closeSidebar = new EventEmitter<void>();

  ngOnInit(): void {
    this.userName = localStorage.getItem('userName') ?? '';
    this.firstName = localStorage.getItem('firstName') ?? '';
    this.lastName = localStorage.getItem('lastName') ?? '';

  }

  menuItems: MenuItem[] = [
    { path: '/dashboard', title: 'Dashboard', icon: 'layout-dashboard' },
    { path: '/clients', title: 'Clientes', icon: 'user' },
    { path: '/finances', title: 'Finanzas', icon: 'dollar-sign' },
    { path: '/communications', title: 'Comunicaciones', icon: 'message-circle' },
    { path: '/lockers', title: 'Bauleras', icon: 'package' },
    { path: '/statistics', title: 'Estadísticas', icon: 'file-text' },
    // { path: '/reports', title: 'Reportes', icon: 'file-text' },
    { path: '/settings', title: 'Configuración', icon: 'settings' }
  ];

  onLinkClick() {
    if (window.innerWidth < 1024) {
      this.closeSidebar.emit();
    }
  }

  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
  
}