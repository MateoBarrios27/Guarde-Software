import { CommonModule, DatePipe } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { IconComponent } from '../icon/icon.component';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-payment-increase-modal',
  standalone: true,
  imports: [CommonModule, DatePipe, FormsModule, IconComponent],
  templateUrl: './payment-increase-modal.component.html'
})
export class PaymentIncreaseModalComponent {
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
    return this.hasMultipleStages ? 'Actualización de abonos' : 'Actualización de abono';
  }

  get confirmIsDisabled(): boolean {
    return this.disabled || this.loading || this.projectedRent <= 0;
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
