import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { MonthlyStatisticsDTO } from '../../core/dtos/statistics/MonthlyStatisticsDTO';
import { StatisticsService } from '../../core/services/statics-service/statics-service.service';
import { IconComponent } from '../../shared/components/icon/icon.component';
import { MonthPickerComponent, PickedMonth } from '../../shared/components/month-picker/month-picker.component';
import { Subscription } from 'rxjs';
import { DataRefreshService } from '../../core/services/data-refresh-service/data-refresh.service';

@Component({
  selector: 'app-statistics',
  standalone: true,
  imports: [CommonModule, IconComponent, MonthPickerComponent],
  templateUrl: './statistics.component.html',
})
export class StatisticsComponent implements OnInit, OnDestroy {
  currentDate = new Date();
  /** El mes en curso: las estadísticas no miran el futuro. Es un valor fijo para no cambiar la referencia en cada revisión. */
  readonly maxMonth: PickedMonth = { month: new Date().getMonth() + 1, year: new Date().getFullYear() };
  isLoading = false;
  hasError = false;

  stats: MonthlyStatisticsDTO = this.emptyStats();

  private readonly dataRefreshSubscription = new Subscription();

  constructor(
    private statisticsService: StatisticsService,
    private dataRefresh: DataRefreshService,
  ) {}

  ngOnInit(): void {
    this.dataRefreshSubscription.add(
      this.dataRefresh.watch(['clients', 'finances', 'lockers'], 'statistics').subscribe(() => {
        this.loadStats();
      }),
    );
    this.loadStats();
  }

  ngOnDestroy(): void {
    this.dataRefreshSubscription.unsubscribe();
  }

  loadStats(): void {
    this.isLoading = true;
    this.hasError = false;

    const year = this.currentDate.getFullYear();
    const month = this.currentDate.getMonth() + 1;

    this.statisticsService.getMonthlyStatistics(year, month).subscribe({
      next: (data) => {
        this.stats = data;
        this.isLoading = false;
      },
      error: (error) => {
        console.error('Error cargando estadísticas:', error);
        this.hasError = true;
        this.isLoading = false;
      },
    });
  }

  /** Las flechas del selector de meses: -1 mes anterior, +1 mes siguiente (no pasa del mes en curso). */
  prevOrNext(delta: number): void {
    if (delta > 0 && this.isCurrentMonth) return;
    this.moveMonth(delta);
  }

  /** Un mes elegido en el panel del selector, o «Volver al mes actual». */
  selectMonth(picked: PickedMonth): void {
    if (picked.year > this.maxMonth.year || (picked.year === this.maxMonth.year && picked.month > this.maxMonth.month)) return;
    this.currentDate = new Date(picked.year, picked.month - 1, 1);
    this.loadStats();
  }

  get monthName(): string {
    return this.currentDate.toLocaleString('es-AR', {
      month: 'long',
      year: 'numeric',
    });
  }

  get isCurrentMonth(): boolean {
    const today = new Date();
    return (
      this.currentDate.getMonth() === today.getMonth() &&
      this.currentDate.getFullYear() === today.getFullYear()
    );
  }

  get totalIva(): number {
    return (this.stats.totalIvaFacturaA || 0) + (this.stats.totalIvaFacturaB || 0);
  }

  get totalEstadoCobranza(): number {
    return Math.max(
      0,
      (this.stats.totalAplicadoAlPeriodo || 0) +
        (this.stats.totalPendienteDelPeriodo || 0),
    );
  }

  get cubiertoDesdeInicioPeriodo(): number {
    return Math.max(
      0,
      (this.stats.totalAplicadoAlPeriodo || 0) -
        (this.stats.totalCubiertoAntesDelPeriodo || 0),
    );
  }

  get porcentajeCobradoPeriodo(): number {
    return this.percentage(
      this.stats.totalAplicadoAlPeriodo,
      this.totalEstadoCobranza,
    );
  }

  get porcentajeCubiertoAntes(): number {
    return this.percentage(
      this.stats.totalCubiertoAntesDelPeriodo,
      this.totalEstadoCobranza,
    );
  }

  get porcentajeCubiertoDesdeInicio(): number {
    return this.percentage(
      this.cubiertoDesdeInicioPeriodo,
      this.totalEstadoCobranza,
    );
  }

  get porcentajePendientePeriodo(): number {
    return this.percentage(
      this.stats.totalPendienteDelPeriodo,
      this.totalEstadoCobranza,
    );
  }

  get cobradoParaPeriodoActualOPasado(): number {
    return Math.max(
      0,
      (this.stats.totalPagado || 0) -
        (this.stats.totalCobradoParaMesesFuturos || 0),
    );
  }

  get porcentajeCobradoParaFuturo(): number {
    return this.percentage(
      this.stats.totalCobradoParaMesesFuturos,
      this.stats.totalPagado,
    );
  }

  get porcentajeCobradoNoFuturo(): number {
    return Math.max(0, 100 - this.porcentajeCobradoParaFuturo);
  }

  get collectionDonutStyle(): string {
    const advance = this.porcentajeCubiertoAntes;
    const current = advance + this.porcentajeCubiertoDesdeInicio;

    return `conic-gradient(#2563eb 0 ${advance}%, #60a5fa ${advance}% ${current}%, #e5e7eb ${current}% 100%)`;
  }

  get occupancyDonutStyle(): string {
    const occupied = this.clampPercentage(this.stats.porcentajeOcupacion || 0);
    return `conic-gradient(#0f766e 0 ${occupied}%, #e5e7eb ${occupied}% 100%)`;
  }

  get maxWarehouseRevenue(): number {
    return Math.max(
      0,
      ...this.revenueWarehouses.map((warehouse) =>
        Number(warehouse.revenue || 0),
      ),
    );
  }

  get revenueWarehouses() {
    return (this.stats.warehouseRevenues || []).filter(
      (warehouse) =>
        Number(warehouse.totalSpaces || 0) > 0 ||
        Number(warehouse.revenue || 0) !== 0,
    );
  }

  get occupancyWarehouses() {
    return (this.stats.warehouseRevenues || []).filter(
      (warehouse) => Number(warehouse.totalSpaces || 0) > 0,
    );
  }

  warehouseRevenuePercentage(revenue: number): number {
    return this.percentage(revenue, this.maxWarehouseRevenue);
  }

  trackWarehouse(index: number, warehouse: { name: string }): string {
    return warehouse.name || index.toString();
  }

  private moveMonth(offset: number): void {
    this.currentDate = new Date(
      this.currentDate.getFullYear(),
      this.currentDate.getMonth() + offset,
      1,
    );
    this.loadStats();
  }

  private emptyStats(): MonthlyStatisticsDTO {
    return {
      year: this.currentDate.getFullYear(),
      month: this.currentDate.getMonth() + 1,
      totalAlquileres: 0,
      balanceGlobalActual: 0,
      totalIntereses: 0,
      deudaTotalDelMes: 0,
      totalPagado: 0,
      totalAdvancePayments: 0,
      totalEspaciosOcupados: 0,
      totalIvaFacturaA: 0,
      totalIvaFacturaB: 0,
      totalObligacionDelPeriodo: 0,
      totalAplicadoAlPeriodo: 0,
      totalPendienteDelPeriodo: 0,
      totalCobradoParaMesesFuturos: 0,
      totalCubiertoAntesDelPeriodo: 0,
      totalEspacios: 0,
      porcentajeOcupacion: 0,
      abonoPromedioPorEspacio: 0,
      warehouseRevenues: [],
    };
  }

  private percentage(
    value: number | null | undefined,
    total: number | null | undefined,
  ): number {
    const safeValue = Number(value || 0);
    const safeTotal = Number(total || 0);
    if (safeTotal <= 0) return 0;
    return (
      Math.round(this.clampPercentage((safeValue / safeTotal) * 100) * 10) / 10
    );
  }

  private clampPercentage(value: number): number {
    return Math.min(100, Math.max(0, value));
  }
}
