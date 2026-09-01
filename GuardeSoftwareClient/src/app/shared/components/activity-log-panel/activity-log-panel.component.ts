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
    { value: 'users', label: 'Usuarios' }
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
  selectedActivity: ActivityLog | null = null;

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
    const normalizedValue = this.normalizeArea(value);
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

  activitySummary(activity: ActivityLog): string {
    const area = this.normalizeArea(activity.tableName);
    const action = activity.action?.toUpperCase() ?? '';

    if (area === 'users' && action === 'LOGIN') {
      return this.loginSummary(activity);
    }

    const newSnapshot = this.parseSnapshot(activity.newValue);
    const oldSnapshot = this.parseSnapshot(activity.oldValue);
    const snapshot = newSnapshot ?? oldSnapshot;
    const definiteSubject = this.definiteSubject(area);

    const field = this.snapshotValue(snapshot, ['Field', 'field']);
    if (field !== undefined && (action === 'UPDATE' || action === 'CREATE')) {
      const value = this.snapshotValue(snapshot, ['Value', 'value']);
      const valueText = value === undefined ? '' : ` a ${this.formatValue(value)}`;
      return `Se actualizó ${this.humanizeKey(String(field)).toLowerCase()} ${this.relativeSubject(area)}${valueText}.`;
    }

    let summary: string;
    switch (action) {
      case 'CREATE':
        summary = `Se registró ${this.indefiniteSubject(area)}.`;
        break;
      case 'UPDATE':
        summary = `Se modificó ${definiteSubject}.`;
        break;
      case 'DELETE':
      case 'DEACTIVATE':
        summary = `Se dio de baja ${this.objectSubject(area)}.`;
        break;
      case 'REACTIVATE':
        summary = `Se reactivó ${definiteSubject}.`;
        break;
      default:
        summary = `Se registró una actividad en ${this.areaLabel(activity.tableName).toLowerCase()}.`;
        break;
    }

    const context = this.snapshotSummary(snapshot);
    if (!context || action === 'DELETE' || action === 'DEACTIVATE') return summary;

    return `${summary.slice(0, -1)} (${context}).`;
  }

  openDetails(activity: ActivityLog): void {
    this.selectedActivity = activity;
  }

  closeDetails(): void {
    this.selectedActivity = null;
  }

  formatSnapshotJson(snapshot?: string): string {
    if (!snapshot) return 'Sin información registrada.';

    try {
      return JSON.stringify(JSON.parse(snapshot), null, 2);
    } catch {
      return snapshot;
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
    const normalizedValue = value.toLowerCase().replace(/_/g, '');
    const knownLabels: Record<string, string> = {
      fullname: 'Nombre completo',
      name: 'Nombre',
      amount: 'Monto',
      commission: 'Comisión',
      status: 'Estado',
      operation: 'Operación',
      channel: 'Canal',
      senddate: 'Fecha de envío',
      sendtime: 'Hora de envío',
      startdate: 'Fecha de inicio',
      enddate: 'Fecha de finalización',
      address: 'Dirección',
      identifier: 'Identificador',
      recipientcount: 'Destinatarios',
      attachmentcount: 'Adjuntos',
      sendtoallemails: 'Todos los emails'
    };

    return knownLabels[normalizedValue] ?? value
      .replace(/([a-z])([A-Z])/g, '$1 $2')
      .replace(/_/g, ' ')
      .replace(/^./, character => character.toUpperCase());
  }

  private normalizeArea(value: string): string {
    const normalizedValue = value?.trim().toLowerCase();
    return normalizedValue === 'auth' ? 'users' : normalizedValue;
  }

  private loginSummary(activity: ActivityLog): string {
    const snapshot = this.parseSnapshot(activity.newValue) ?? this.parseSnapshot(activity.oldValue);
    const result = String(this.snapshotValue(snapshot, ['Result', 'result', 'Status', 'status']) ?? '').toLowerCase();

    if (['success', 'successful', 'ok', 'exitoso', 'exitosamente'].includes(result)) {
      return 'Inicio de sesión exitoso.';
    }

    if (['failed', 'failure', 'error', 'rejected', 'rechazado', 'fallido', 'locked_out'].includes(result)) {
      return 'Inicio de sesión rechazado.';
    }

    return 'Inicio de sesión: resultado no informado.';
  }

  private parseSnapshot(snapshot?: string): Record<string, unknown> | null {
    if (!snapshot) return null;

    try {
      const parsed: unknown = JSON.parse(snapshot);
      return parsed !== null && typeof parsed === 'object' && !Array.isArray(parsed)
        ? parsed as Record<string, unknown>
        : null;
    } catch {
      return null;
    }
  }

  private snapshotValue(snapshot: Record<string, unknown> | null, keys: string[]): unknown {
    if (!snapshot) return undefined;

    const matchingKey = Object.keys(snapshot).find(existingKey =>
      keys.some(expectedKey => existingKey.toLowerCase() === expectedKey.toLowerCase()));

    return matchingKey === undefined ? undefined : snapshot[matchingKey];
  }

  private snapshotSummary(snapshot: Record<string, unknown> | null): string {
    if (!snapshot) return '';

    const entries = Object.entries(snapshot)
      .filter(([key, value]) => !this.isTechnicalSnapshotField(key, value))
      .slice(0, 2)
      .map(([key, value]) => `${this.humanizeKey(key)}: ${this.formatValue(value)}`);

    return entries.join(' · ');
  }

  private isTechnicalSnapshotField(key: string, value: unknown): boolean {
    const normalizedKey = key.toLowerCase();
    return normalizedKey === 'id'
      || normalizedKey.endsWith('id')
      || normalizedKey.endsWith('_id')
      || normalizedKey === 'deleted'
      || value === null
      || value === undefined
      || value === '';
  }

  private indefiniteSubject(area: string): string {
    const subjects: Record<string, string> = {
      clients: 'un nuevo cliente',
      payments: 'un nuevo pago',
      lockers: 'una nueva baulera',
      payment_methods: 'un nuevo medio de pago',
      communications: 'una nueva comunicación',
      warehouses: 'un nuevo depósito',
      locker_types: 'un nuevo tipo de baulera',
      rental_amount_history: 'un nuevo abono',
      users: 'un nuevo usuario'
    };

    return subjects[area] ?? 'un nuevo registro';
  }

  private definiteSubject(area: string): string {
    const subjects: Record<string, string> = {
      clients: 'el cliente',
      payments: 'el pago',
      lockers: 'la baulera',
      payment_methods: 'el medio de pago',
      communications: 'la comunicación',
      warehouses: 'el depósito',
      locker_types: 'el tipo de baulera',
      rental_amount_history: 'el abono',
      users: 'el usuario'
    };

    return subjects[area] ?? 'el registro';
  }

  private objectSubject(area: string): string {
    const subjects: Record<string, string> = {
      clients: 'al cliente',
      payments: 'al pago',
      lockers: 'a la baulera',
      payment_methods: 'al medio de pago',
      communications: 'a la comunicación',
      warehouses: 'al depósito',
      locker_types: 'al tipo de baulera',
      rental_amount_history: 'al abono',
      users: 'al usuario'
    };

    return subjects[area] ?? 'al registro';
  }

  private relativeSubject(area: string): string {
    const subjects: Record<string, string> = {
      clients: 'del cliente',
      payments: 'del pago',
      lockers: 'de la baulera',
      payment_methods: 'del medio de pago',
      communications: 'de la comunicación',
      warehouses: 'del depósito',
      locker_types: 'del tipo de baulera',
      rental_amount_history: 'del abono',
      users: 'del usuario'
    };

    return subjects[area] ?? 'del registro';
  }

  private formatValue(value: unknown): string {
    if (value === null || value === undefined || value === '') return '—';
    if (typeof value === 'boolean') return value ? 'Sí' : 'No';
    if (typeof value === 'object') return '[detalle]';

    const text = String(value);
    const knownValues: Record<string, string> = {
      send_now: 'envío inmediato',
      retry: 'reintento',
      retry_selected: 'reintento de seleccionados',
      success: 'exitoso',
      successful: 'exitoso',
      failed: 'rechazado',
      failure: 'rechazado',
      rejected: 'rechazado'
    };

    return knownValues[text.toLowerCase()] ?? text;
  }
}
