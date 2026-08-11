import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { MonthlyStatisticsDTO } from '../../core/dtos/statistics/MonthlyStatisticsDTO';
import { StatisticsService } from '../../core/services/statics-service/statics-service.service';
import { IconComponent } from '../../shared/components/icon/icon.component';

@Component({
  selector: 'app-statistics',
  standalone: true,
  imports: [CommonModule, IconComponent],
  templateUrl: './statistics.component.html',
  styleUrls: ['./statistics.component.css'],
})
export class StatisticsComponent implements OnInit {
  currentDate = new Date();
  isLoading = false;
  hasError = false;

  stats: MonthlyStatisticsDTO = this.emptyStats();

  constructor(private statisticsService: StatisticsService) {}

  ngOnInit(): void {
    this.loadStats();
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

  prevMonth(): void {
    this.moveMonth(-1);
  }

  nextMonth(): void {
    if (this.isCurrentMonth) return;
    this.moveMonth(1);
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
      warehouseRevenues: [],
    };
  }
}
