import { CommonModule, DatePipe } from '@angular/common';
import { Component, EventEmitter, Input, Output, OnChanges, OnDestroy, SimpleChanges, inject, AfterViewInit, ElementRef, ViewChild } from '@angular/core';
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
export class PaymentIncreaseModalComponent implements OnChanges, OnDestroy, AfterViewInit {
  private readonly monthlyIncreaseService = inject(MonthlyIncreaseService);
  private settingsSubscription?: Subscription;
  private percentageBeforeEdit: number | null = null;
  private rentBeforeEdit: number | null = null;
  @ViewChild('increaseDialog') private dialog?: ElementRef<HTMLElement>;
  private previousFocus: HTMLElement | null = null;
  private restoreFocus = false;

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
  @Output() close = new EventEmitter<void>();
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
      this.percentageBeforeEdit = null;
      this.rentBeforeEdit = null;
      this.loadConfiguredPercentage();
    }
  }

  ngAfterViewInit(): void {
    this.previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    this.dialog?.nativeElement.focus({ preventScroll: true });
  }

  ngOnDestroy(): void {
    this.settingsSubscription?.unsubscribe();
    if (this.restoreFocus && this.previousFocus?.isConnected) this.previousFocus.focus({ preventScroll: true });
  }

  requestClose(event?: Event): void {
    event?.preventDefault();
    event?.stopPropagation();
    if (this.loading) return;
    this.restoreFocus = true;
    this.close.emit();
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

  beginPercentageEdit(event: Event): void {
    this.percentageBeforeEdit = Number((event.target as HTMLInputElement).value) || 0;
  }

  finishPercentageEdit(event: Event): void {
    const value = Number((event.target as HTMLInputElement).value) || 0;
    const changed = this.percentageBeforeEdit !== null && value !== this.percentageBeforeEdit;
    this.percentageBeforeEdit = null;
    if (changed) this.percentageBlur.emit();
  }

  beginRentEdit(event: Event): void {
    this.rentBeforeEdit = Number((event.target as HTMLInputElement).value) || 0;
  }

  finishRentEdit(event: Event): void {
    const value = Number((event.target as HTMLInputElement).value) || 0;
    const changed = this.rentBeforeEdit !== null && value !== this.rentBeforeEdit;
    this.rentBeforeEdit = null;
    if (changed) this.projectedRentBlur.emit();
  }

  blurInput(event: Event): void {
    (event.target as HTMLInputElement | null)?.blur();
  }
}
