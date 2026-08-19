import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivityLog, ActivityLogFilter, ActivityLogPage, ActivityLogUser } from '../../../core/models/activity-log';
import { ActivityLogService } from '../../../core/services/activityLog-service/activity-log.service';
import { IconComponent } from '../icon/icon.component';

interface ActivityAreaOption {
  value: string;
  label: string;
}

@Component({
  selector: 'app-activity-log-panel',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './activity-log-panel.component.html',
  styleUrl: './activity-log-panel.component.css'
})
export class ActivityLogPanelComponent implements OnInit {
  constructor(private readonly activityLogService: ActivityLogService) {}

  readonly areas: ActivityAreaOption[] = [
    { value: 'clients', label: 'Clientes' },
    { value: 'payments', label: 'Pagos' },
    { value: 'lockers', label: 'Bauleras' },
    { value: 'payment_methods', label: 'Medios de pago' },
    { value: 'communications', label: 'Comunicaciones' },
    { value: 'warehouses', label: 'Depósitos' },
    { value: 'locker_types', label: 'Tipos de bauleras' },
    { value: 'rental_amount_history', label: 'Abonos' },
    { value: 'auth', label: 'Inicios de sesión' }
  ];

  readonly actions = [
    { value: 'CREATE', label: 'Alta' },
    { value: 'UPDATE', label: 'Modificación' },
    { value: 'DELETE', label: 'Baja' },
    { value: 'DEACTIVATE', label: 'Desactivación' },
    { value: 'REACTIVATE', label: 'Reactivación' },
    { value: 'LOGIN', label: 'Inicio de sesión' }
  ];

  readonly pageSizes = [25, 50, 100];

  activities: ActivityLog[] = [];
  users: ActivityLogUser[] = [];
  totalCount = 0;
  totalPages = 0;
  pageNumber = 1;
  pageSize = 25;
  isLoading = false;
  errorMessage = '';

  selectedArea = '';
  selectedAction = '';
  selectedUserId: number | '' = '';
  fromDate = '';
  toDate = '';
  search = '';

  ngOnInit(): void {
    this.loadUsers();
    this.loadActivities();
  }

  loadUsers(): void {
    this.activityLogService.getActivityLogUsers().subscribe({
      next: users => this.users = users,
      error: () => this.errorMessage = 'No se pudieron cargar los usuarios para filtrar.'
    });
  }

  loadActivities(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.activityLogService.getActivityLogs(this.buildFilter()).subscribe({
      next: (page: ActivityLogPage) => {
        this.activities = page.items ?? [];
        this.totalCount = page.totalCount ?? 0;
        this.totalPages = page.totalPages ?? 0;
        this.pageNumber = page.pageNumber ?? this.pageNumber;
        this.pageSize = page.pageSize ?? this.pageSize;
        this.isLoading = false;
      },
      error: () => {
        this.activities = [];
        this.totalCount = 0;
        this.totalPages = 0;
        this.isLoading = false;
        this.errorMessage = 'No se pudo cargar el registro de actividad.';
      }
    });
  }

  applyFilters(): void {
    this.pageNumber = 1;
    this.loadActivities();
  }

  clearFilters(): void {
    this.selectedArea = '';
    this.selectedAction = '';
    this.selectedUserId = '';
    this.fromDate = '';
    this.toDate = '';
    this.search = '';
    this.applyFilters();
  }

  selectArea(area: string): void {
    this.selectedArea = this.selectedArea === area ? '' : area;
    this.applyFilters();
  }

  goToPage(page: number): void {
    if (this.isLoading || page < 1 || (this.totalPages > 0 && page > this.totalPages)) return;
    this.pageNumber = page;
    this.loadActivities();
  }

  onPageSizeChange(): void {
    this.pageSize = Number(this.pageSize) || 25;
    this.pageNumber = 1;
    this.loadActivities();
  }

  get visiblePages(): number[] {
    if (this.totalPages <= 1) return this.totalPages === 1 ? [1] : [];

    const start = Math.max(1, this.pageNumber - 2);
    const end = Math.min(this.totalPages, start + 4);
    const adjustedStart = Math.max(1, end - 4);
    return Array.from({ length: end - adjustedStart + 1 }, (_, index) => adjustedStart + index);
  }

  areaLabel(value: string): string {
    const normalizedValue = value?.toLowerCase();
    return this.areas.find(area => area.value === normalizedValue)?.label ?? value;
  }

  actionLabel(value: string): string {
    const normalizedValue = value?.toUpperCase();
    return this.actions.find(action => action.value === normalizedValue)?.label ?? value;
  }

  actionClass(value: string): string {
    switch (value?.toUpperCase()) {
      case 'CREATE': return 'activity-action activity-action-create';
      case 'DELETE':
      case 'DEACTIVATE': return 'activity-action activity-action-delete';
      case 'LOGIN': return 'activity-action activity-action-login';
      case 'REACTIVATE': return 'activity-action activity-action-reactivate';
      default: return 'activity-action activity-action-update';
    }
  }

  displayUser(activity: ActivityLog): string {
    return activity.userDisplayName || activity.userName || `Usuario #${activity.userId}`;
  }

  snapshotPreview(snapshot?: string): string {
    if (!snapshot) return '—';

    try {
      const parsed = JSON.parse(snapshot) as Record<string, unknown>;
      const entries = Object.entries(parsed)
        .filter(([key]) => !['Id', 'id', 'UserID', 'userId'].includes(key))
        .slice(0, 3)
        .map(([key, value]) => `${this.humanizeKey(key)}: ${this.formatValue(value)}`);
      return entries.length > 0 ? entries.join(' · ') : 'Ver detalle';
    } catch {
      return snapshot.length > 140 ? `${snapshot.slice(0, 140)}…` : snapshot;
    }
  }

  trackByActivityId(_: number, activity: ActivityLog): number {
    return activity.id;
  }

  private buildFilter(): ActivityLogFilter {
    return {
      pageNumber: this.pageNumber,
      pageSize: this.pageSize,
      area: this.selectedArea || undefined,
      action: this.selectedAction || undefined,
      userId: this.selectedUserId === '' ? undefined : Number(this.selectedUserId),
      fromDate: this.fromDate || undefined,
      toDate: this.toDate || undefined,
      search: this.search.trim() || undefined
    };
  }

  private humanizeKey(value: string): string {
    return value
      .replace(/([a-z])([A-Z])/g, '$1 $2')
      .replace(/_/g, ' ')
      .replace(/^./, character => character.toUpperCase());
  }

  private formatValue(value: unknown): string {
    if (value === null || value === undefined || value === '') return '—';
    if (typeof value === 'boolean') return value ? 'Sí' : 'No';
    if (typeof value === 'object') return '[detalle]';
    return String(value);
  }
}
