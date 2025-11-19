import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IconComponent } from '../../shared/components/icon/icon.component';
import { StatisticsService } from '../../core/services/statics-service/statics-service.service';
import { MonthlyStatisticsDTO } from '../../core/dtos/statistics/MonthlyStatisticsDTO';

@Component({
  selector: 'app-statistics',
  standalone: true,
  imports: [CommonModule, IconComponent],
  templateUrl: './statistics.component.html',
})
export class StatisticsComponent implements OnInit {
  
  // Fecha seleccionada (inicia hoy)
  currentDate: Date = new Date();
  
  isLoading = false;
  stats: MonthlyStatisticsDTO = {
    year: this.currentDate.getFullYear(),
    month: this.currentDate.getMonth() + 1,
    totalAbonado: 0,
    totalSaldo: 0,
    totalInteres: 0,
    totalSaldoAnterior: 0,
    totalFacturado: 0
  };

  constructor(private statisticsService: StatisticsService) {}

  ngOnInit(): void {
    this.loadStats();
  }

  loadStats(): void {
    this.isLoading = true;
    const year = this.currentDate.getFullYear();
    const month = this.currentDate.getMonth() + 1; // JavaScript meses son 0-11

    this.statisticsService.getMonthlyStatistics(year, month).subscribe({
      next: (data) => {
        this.stats = data;
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Error cargando estadísticas:', err);
        this.isLoading = false;
        // Aquí podrías poner un Swal de error si quieres
      }
    });
  }

  // --- Navegación de Fechas ---

  prevMonth(): void {
    this.currentDate = new Date(this.currentDate.setMonth(this.currentDate.getMonth() - 1));
    this.loadStats();
  }

  nextMonth(): void {
    this.currentDate = new Date(this.currentDate.setMonth(this.currentDate.getMonth() + 1));
    this.loadStats();
  }

  // Helper para mostrar el nombre del mes en español
  get monthName(): string {
    return this.currentDate.toLocaleString('es-ES', { month: 'long', year: 'numeric' });
  }
  
  // Verifica si es el mes actual para deshabilitar el botón "Siguiente" si no quieres ver futuro
  get isCurrentMonth(): boolean {
      const today = new Date();
      return this.currentDate.getMonth() === today.getMonth() && 
             this.currentDate.getFullYear() === today.getFullYear();
  }
}