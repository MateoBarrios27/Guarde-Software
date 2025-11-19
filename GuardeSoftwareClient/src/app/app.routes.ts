import { Routes } from '@angular/router';
import { ClientsComponent } from './pages/clients/clients.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { LockersComponent } from './pages/lockers/lockers.component';
import { FinancesComponent } from './pages/finances/finances.component';
import { CommunicationsComponent } from './pages/communications/communications.component';
import { ReportsComponent } from './pages/reports/reports.component';
import { SettingsComponent } from './pages/settings/settings.component';
import { StatisticsComponent } from './pages/statistics/statistics.component';

export const routes: Routes = [
  // Redirect the root path to /clients by default
  { path: '', redirectTo: '/clients', pathMatch: 'full' }, 
  
  { path: 'dashboard', component: DashboardComponent, data: { title: 'Dashboard' } },
  { path: 'clients', component: ClientsComponent, data: { title: 'Clientes' } },
  { path: 'lockers', component: LockersComponent, data: { title: 'Bauleras' } },
  { path: 'finances', component: FinancesComponent, data: { title: 'Finanzas' } },
  { path: 'communications', component: CommunicationsComponent, data: { title: 'Comunicaciones' } },
  { path: 'reports', component: ReportsComponent, data: { title: 'Reportes' } },
  { path: 'statistics', component: StatisticsComponent, data: { title: 'Estadísitcas' } },
  { path: 'settings', component: SettingsComponent, data: { title: 'Configuración' } },
  
  // Wildcard route to handle not-found URLs
  { path: '**', redirectTo: '/clients' } 
];