import { Routes } from '@angular/router';
import { ClientsComponent } from './pages/clients/clients.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { LockersComponent } from './pages/lockers/lockers.component';
import { FinancesComponent } from './pages/finances/finances.component';
import { CommunicationsComponent } from './pages/communications/communications.component';
import { ReportsComponent } from './pages/reports/reports.component';
import { SettingsComponent } from './pages/settings/settings.component';
import { StatisticsComponent } from './pages/statistics/statistics.component';
import { LoginComponent } from './pages/auth/login/login.component';
import { AuthGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  // Redirect the root path to /clients by default
  { path: '', redirectTo: '/login', pathMatch: 'full' },

  { path: 'login', component: LoginComponent, data: { title: 'Login' } },
  
  { path: 'dashboard', component: DashboardComponent, canActivate: [AuthGuard], data: { title: 'Dashboard' } },
  { path: 'clients', component: ClientsComponent, canActivate: [AuthGuard], data: { title: 'Clientes' } },
  { path: 'lockers', component: LockersComponent, canActivate: [AuthGuard], data: { title: 'Bauleras' } },
  { path: 'finances', component: FinancesComponent, canActivate: [AuthGuard], data: { title: 'Finanzas' } },
  { path: 'communications', component: CommunicationsComponent, data: { title: 'Comunicaciones' } },
  { path: 'reports', component: ReportsComponent, canActivate: [AuthGuard], data: { title: 'Reportes' } },
  { path: 'statistics', component: StatisticsComponent, canActivate: [AuthGuard], data: { title: 'Estadísitcas' } },
  { path: 'settings', component: SettingsComponent, canActivate: [AuthGuard], data: { title: 'Configuración' } },
  
  // Wildcard route to handle not-found URLs
  { path: '**', redirectTo: '/login' }
];


