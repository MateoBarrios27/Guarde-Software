import { CommonModule, DatePipe } from '@angular/common';
import { Component, EventEmitter, Input, Output, OnChanges, OnDestroy, SimpleChanges, inject } from '@angular/core';
import { IconComponent } from '../icon/icon.component';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { MonthlyIncreaseService } from '../../../core/services/monthlyIncrease-service/monthly-increase.service';

@Component({
  selector: 'app-payment-increase-modal',
  standalone: true,
  imports: [CommonModule, DatePipe, FormsModule, IconComponent],
  templateUrl: './payment-increase-modal.component.html',
  styleUrl: './payment-increase-modal.component.css'
})
export class PaymentIncreaseModalComponent implements OnChanges, OnDestroy {
  private readonly monthlyIncreaseService = inject(MonthlyIncreaseService);
  private settingsSubscription?: Subscription;

  @Input() increaseYear = 0;
  @Input() increaseMonth = 0;
  @Input() useConfiguredPercentage = true;
  @Output() configuredPercentageResolved = new EventEmitter<number>();

  isLoadingDefault = false;
  configuredPercentage: number | null = null;
  settingsError = '';
  settingsChecked = false;
  @Input() reason = '';
  @Input() clientName = '';
  @Input() stage = 1;
  @Input() stageTotal = 1;
  @Input() stageLabel = '';
  @Input() currentRent = 0;
  @Input() projectedRent = 0;
  @Input() percentage = 0;
  @Input() projectedNextIncreaseDate: Date | string | null = null;
  @Input() showBack = false;
  @Input() showSkip = true;
  @Input() skipLabel = 'Omitir por ahora';
  @Input() confirmLabel = 'Confirmar aumento';
  @Input() loading = false;
  @Input() disabled = false;

  @Output() projectedRentChange = new EventEmitter<number>();
  @Output() percentageChange = new EventEmitter<number>();
  @Output() projectedRentBlur = new EventEmitter<void>();
  @Output() percentageBlur = new EventEmitter<void>();
  @Output() back = new EventEmitter<void>();
  @Output() skip = new EventEmitter<void>();
  @Output() confirm = new EventEmitter<void>();

  get hasMultipleStages(): boolean {
    return this.stageTotal > 1;
  }

  get title(): string {
    return 'Ajustar abono';
  }

  get confirmIsDisabled(): boolean {
    return this.disabled || this.loading || this.isLoadingDefault || this.projectedRent <= 0;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['increaseYear'] || changes['increaseMonth'] || changes['useConfiguredPercentage']) {
      this.loadConfiguredPercentage();
    }
  }

  ngOnDestroy(): void {
    this.settingsSubscription?.unsubscribe();
  }

  loadConfiguredPercentage(): void {
    this.settingsSubscription?.unsubscribe();
    this.isLoadingDefault = false;
    this.settingsError = '';
    this.settingsChecked = false;
    this.configuredPercentage = null;
    if (!this.useConfiguredPercentage || !this.increaseYear || this.increaseMonth < 1 || this.increaseMonth > 12) return;

    this.isLoadingDefault = true;
    this.settingsSubscription = this.monthlyIncreaseService.getPercentageForMonth(this.increaseYear, this.increaseMonth)
      .subscribe({
        next: percentage => {
          this.configuredPercentage = percentage;
          this.isLoadingDefault = false;
          this.settingsChecked = true;
          this.configuredPercentageResolved.emit(percentage ?? 0);
        },
        error: () => {
          this.isLoadingDefault = false;
          this.settingsError = 'No se pudo cargar el porcentaje de Configuración. Podés ingresarlo manualmente.';
        }
      });
  }

  emitProjectedRent(value: number | string): void {
    this.projectedRentChange.emit(Number(value) || 0);
  }

  emitPercentage(value: number | string): void {
    this.percentageChange.emit(Number(value) || 0);
  }

  blurInput(event: Event): void {
    (event.target as HTMLInputElement | null)?.blur();
  }
}
