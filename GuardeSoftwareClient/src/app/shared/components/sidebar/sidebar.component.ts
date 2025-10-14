import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { IconComponent } from '../icon/icon.component';

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
  @Input() isOpen = false;
  @Output() closeSidebar = new EventEmitter<void>();

  menuItems: MenuItem[] = [
    { path: '/dashboard', title: 'Dashboard', icon: 'layout-dashboard' },
    { path: '/lockers', title: 'Bauleras', icon: 'package' },
    { path: '/clients', title: 'Clientes', icon: 'users' },
    { path: '/finances', title: 'Finanzas', icon: 'dollar-sign' },
    { path: '/communications', title: 'Comunicaciones', icon: 'message-circle' },
    // { path: '/reports', title: 'Reportes', icon: 'file-text' },
    { path: '/settings', title: 'Configuración', icon: 'settings' }
  ];

  onLinkClick() {
    if (window.innerWidth < 1024) {
      this.closeSidebar.emit();
    }
  }
}