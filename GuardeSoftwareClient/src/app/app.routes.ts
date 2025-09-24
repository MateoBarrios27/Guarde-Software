import { Routes } from '@angular/router';
import { ClientsComponent } from './pages/clients/clients.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { LockersComponent } from './pages/lockers/lockers.component';
import { FinancesComponent } from './pages/finances/finances.component';
import { CommunicationsComponent } from './pages/communications/communications.component';
import { ReportsComponent } from './pages/reports/reports.component';
import { SettingsComponent } from './pages/settings/settings.component';

export const routes: Routes = [
  // Redirect the root path to /clients by default
  { path: '', redirectTo: '/clients', pathMatch: 'full' }, 
  
  { path: 'dashboard', component: DashboardComponent },
  { path: 'lockers', component: LockersComponent },
  { path: 'clients', component: ClientsComponent },
  { path: 'finances', component: FinancesComponent },
  { path: 'communications', component: CommunicationsComponent },
  { path: 'reports', component: ReportsComponent },
  { path: 'settings', component: SettingsComponent },
  
  // Wildcard route to handle not-found URLs
  { path: '**', redirectTo: '/clients' } 
];