import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { environment } from '../../../../environments/environments';
import {
  CreateMonthlyInterestDto,
  MonthlyInterestOverview,
  MonthlyInterestSetting,
  UpdateMonthlyInterestDto,
} from '../../models/monthly-interest';
import { DataRefreshService } from '../data-refresh-service/data-refresh.service';
import {
  DEFAULT_INTEREST_APPLY_DAY,
  DEFAULT_INTEREST_PERCENTAGE,
  MonthlyInterestRule,
  resolveInterestRule,
} from '../../utils/interest-rule';

/**
 * Intereses por mora configurables por mes. Guarda en memoria el último resumen recibido
 * para que Registrar Pago (Finanzas y Dashboard) resuelva el día y el porcentaje del mes sin
 * esperar a la red. Sin resumen (sin conexión, error o migración sin aplicar) rige el valor
 * por defecto: 10 % el día 11. El backend vuelve a calcular el recargo al guardar el pago.
 */
@Injectable({ providedIn: 'root' })
export class MonthlyInterestService {
  private readonly apiUrl = `${environment.apiUrl}/MonthlyInterest`;
  private readonly overviewSubject = new BehaviorSubject<MonthlyInterestOverview | null>(null);
  readonly overview$ = this.overviewSubject.asObservable();

  constructor(
    private http: HttpClient,
    private dataRefresh: DataRefreshService,
  ) {}

  get overview(): MonthlyInterestOverview | null {
    return this.overviewSubject.value;
  }

  getOverview(): Observable<MonthlyInterestOverview> {
    return this.http.get<MonthlyInterestOverview>(this.apiUrl).pipe(
      tap(overview => this.overviewSubject.next(overview)),
    );
  }

  /** Recarga el resumen en segundo plano; un error deja el último conocido (o el valor por defecto). */
  refresh(): void {
    this.getOverview().subscribe({ error: () => undefined });
  }

  /** Regla del mes de la fecha dada, con el último resumen conocido. */
  ruleForDate(date: Date | string | null | undefined): MonthlyInterestRule {
    const parsed = date ? new Date(date) : new Date();
    const valid = Number.isNaN(parsed.getTime()) ? new Date() : parsed;
    return this.ruleForMonth(valid.getFullYear(), valid.getMonth() + 1);
  }

  ruleForMonth(year: number, month: number): MonthlyInterestRule {
    const overview = this.overview;
    return resolveInterestRule(overview?.settings, year, month, {
      percentage: overview?.defaultPercentage ?? DEFAULT_INTEREST_PERCENTAGE,
      applyDay: overview?.defaultApplyDay ?? DEFAULT_INTEREST_APPLY_DAY,
    });
  }

  createSetting(dto: CreateMonthlyInterestDto): Observable<MonthlyInterestSetting> {
    return this.http.post<MonthlyInterestSetting>(this.apiUrl, dto).pipe(
      tap(() => this.dataRefresh.notify('catalog')),
    );
  }

  updateSetting(id: number, dto: UpdateMonthlyInterestDto): Observable<unknown> {
    return this.http.put(`${this.apiUrl}/${id}`, dto).pipe(
      tap(() => this.dataRefresh.notify('catalog')),
    );
  }

  deleteSetting(id: number): Observable<unknown> {
    return this.http.delete(`${this.apiUrl}/${id}`).pipe(
      tap(() => this.dataRefresh.notify('catalog')),
    );
  }
}
