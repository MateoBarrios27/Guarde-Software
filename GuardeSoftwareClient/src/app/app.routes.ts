import { Routes } from '@angular/router';
import { AuthGuard } from './core/guards/auth.guard';
import { adminGuard } from './core/guards/admin.guard';

export const routes: Routes = [
  { path: '', redirectTo: '/login', pathMatch: 'full' },

  {
    path: 'login',
    loadComponent: () => import('./pages/auth/login/login.component').then(m => m.LoginComponent),
    data: { title: 'Login' }
  },
  {
    path: 'dashboard',
    loadComponent: () => import('./pages/dashboard/dashboard.component').then(m => m.DashboardComponent),
    canActivate: [AuthGuard],
    data: { title: 'Panel Principal', reuse: true }
  },
  {
    path: 'clients',
    loadComponent: () => import('./pages/clients/clients.component').then(m => m.ClientsComponent),
    canActivate: [AuthGuard],
    data: { title: 'Clientes', reuse: true }
  },
  {
    path: 'lockers',
    loadComponent: () => import('./pages/lockers/lockers.component').then(m => m.LockersComponent),
    canActivate: [AuthGuard],
    data: { title: 'Bauleras', reuse: true }
  },
  {
    path: 'finances',
    loadComponent: () => import('./pages/finances/finances.component').then(m => m.FinancesComponent),
    canActivate: [AuthGuard],
    data: { title: 'Finanzas', reuse: true }
  },
  {
    path: 'communications',
    loadComponent: () => import('./pages/communications/communications.component').then(m => m.CommunicationsComponent),
    data: { title: 'Comunicaciones', reuse: true }
  },
  {
    path: 'reports',
    loadComponent: () => import('./pages/reports/reports.component').then(m => m.ReportsComponent),
    canActivate: [AuthGuard],
    data: { title: 'Reportes', reuse: true }
  },
  {
    path: 'statistics',
    loadComponent: () => import('./pages/statistics/statistics.component').then(m => m.StatisticsComponent),
    canActivate: [AuthGuard],
    data: { title: 'Estadísticas', reuse: true }
  },
  {
    path: 'settings',
    loadComponent: () => import('./pages/settings/settings.component').then(m => m.SettingsComponent),
    canActivate: [AuthGuard],
    data: { title: 'Configuración', reuse: true }
  },
  {
    path: 'cash',
    loadComponent: () => import('./pages/cash/cash.component').then(m => m.CashComponent),
    canActivate: [adminGuard],
    data: { title: 'Caja', reuse: true }
  },

  { path: '**', redirectTo: '/login' }
];
