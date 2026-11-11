import { Component, ElementRef, EventEmitter, Input, Output, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NgControl } from '@angular/forms';
import { ConnectedPosition, Overlay, OverlayModule } from '@angular/cdk/overlay';
import { IconComponent } from '../icon/icon.component';

export interface PickedMonth {
  month: number; // 1-12
  year: number;
}

let nextPanelId = 0;

/**
 * Selector de meses con el mismo diseño y comportamiento que el de Finanzas: ‹ Noviembre 2026 ▾ ›. Las flechas avanzan
 * o retroceden un mes y el título abre un panel con ‹ año › y los 12 meses en una grilla de 4×3 (el elegido en azul, el
 * mes en curso con un punto). Cambiar de año en el panel no cambia el mes mostrado hasta elegir uno.
 *
 * Se usa de dos maneras:
 * - **Controlado** (Caja, Estadísticas): se le pasan `[month]` y `[year]`, avisa con (monthStep) y (monthSelect) y quien lo
 *   usa decide si cambia el mes (Caja, por ejemplo, pide confirmación antes de recargar los datos). No cambia nada solo.
 * - **Como campo de formulario** (`ngModel`, `formControlName`): el valor es un texto `YYYY-MM`, el mismo que daba el
 *   `<input type="month">` al que reemplaza, y vacío mientras no se eligió nada. Acá sí cambia por su cuenta.
 *
 * Variantes: `solid` es la barra azul de Caja y Estadísticas; `field` es el mismo azul en una sola fila de campo, para
 * formularios (sin la marca del mes en curso ni el atajo para volver a él); `compact` es una píldora clara para tablas.
 */
@Component({
  selector: 'app-month-picker',
  imports: [CommonModule, OverlayModule, IconComponent],
  templateUrl: './month-picker.component.html',
  styleUrls: ['./month-picker.component.css'],
  host: { '[class.mp-host-solid]': "variant === 'solid'" },
})
export class MonthPickerComponent implements ControlValueAccessor {
  readonly monthNames = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];
  readonly monthShortNames = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];

  /** Mes mostrado (1-12). Como campo de formulario lo fija el valor del campo. */
  @Input() month: number = new Date().getMonth() + 1;
  @Input() year: number = new Date().getFullYear();
  /** Primer año al que se puede ir: no hay mes anterior a enero de este año. Sin valor, no hay tope. */
  @Input() minYear: number | null = null;
  /** Último mes al que se puede ir (Estadísticas no mira el futuro). Sin valor, no hay tope. */
  @Input() maxMonth: PickedMonth | null = null;
  /** `solid` es la barra azul de Finanzas/Caja; `field` es esa misma barra en una fila de formulario; `compact`, una píldora clara. */
  @Input() variant: 'solid' | 'field' | 'compact' = 'solid';
  /** Texto del título mientras un campo de formulario no tiene mes elegido. */
  @Input() placeholder = 'Elegir mes';
  @Input() disabled = false;

  /** -1 o +1: las flechas. */
  @Output() monthStep = new EventEmitter<number>();
  /** Un mes elegido en el panel, o «Volver al mes actual». */
  @Output() monthSelect = new EventEmitter<PickedMonth>();

  private readonly overlay = inject(Overlay);
  private readonly ngControl = inject(NgControl, { optional: true, self: true });
  readonly scrollStrategy = this.overlay.scrollStrategies.reposition();
  readonly positions: ConnectedPosition[] = [
    { originX: 'center', originY: 'bottom', overlayX: 'center', overlayY: 'top', offsetY: 8 },
    { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 8 },
    { originX: 'center', originY: 'top', overlayX: 'center', overlayY: 'bottom', offsetY: -8 },
    { originX: 'start', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -8 },
  ];

  readonly panelId = `month-picker-panel-${nextPanelId++}`;
  open = false;
  /** Año que recorre el panel; sigue al mes elegido pero se puede navegar sin cambiar de mes. */
  railYear = new Date().getFullYear();
  /** Falso sólo en un campo de formulario que todavía no tiene mes: muestra el texto de ayuda en vez de un mes. */
  hasValue = true;
  @ViewChild('title') private title?: ElementRef<HTMLButtonElement>;

  private onChange: (value: string) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  constructor() {
    if (this.ngControl) this.ngControl.valueAccessor = this;
  }

  /** Dentro de un formulario el selector cambia solo y guarda el valor; si no, sólo avisa. */
  private get isFormField(): boolean {
    return !!this.ngControl;
  }

  // --- ControlValueAccessor: el valor es un texto «YYYY-MM» ---
  writeValue(value: unknown): void {
    const match = typeof value === 'string' ? /^(\d{4})-(0[1-9]|1[0-2])/.exec(value) : null;
    if (match) {
      this.year = Number(match[1]);
      this.month = Number(match[2]);
      this.hasValue = true;
    } else {
      const now = new Date();
      this.year = now.getFullYear();
      this.month = now.getMonth() + 1;
      this.hasValue = false;
    }
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled = isDisabled;
    if (isDisabled) this.open = false;
  }

  get displayName(): string {
    return this.hasValue ? this.monthNames[this.month - 1] : this.placeholder;
  }

  get isCurrent(): boolean {
    const now = new Date();
    return this.hasValue && this.year === now.getFullYear() && this.month === now.getMonth() + 1;
  }

  /** La flecha de «mes anterior» se apaga en enero del primer año permitido. */
  get atMinimum(): boolean {
    return this.minYear !== null && (this.year < this.minYear || (this.year === this.minYear && this.month <= 1));
  }

  /** La flecha de «mes siguiente» se apaga en el último mes permitido. */
  get atMaximum(): boolean {
    const max = this.maxMonth;
    return !!max && (this.year > max.year || (this.year === max.year && this.month >= max.month));
  }

  get canGoToPreviousRailYear(): boolean {
    return this.minYear === null || this.railYear > this.minYear;
  }

  get canGoToNextRailYear(): boolean {
    return !this.maxMonth || this.railYear < this.maxMonth.year;
  }

  step(delta: number): void {
    if (this.disabled || (delta < 0 && this.atMinimum) || (delta > 0 && this.atMaximum)) return;
    this.close();
    if (this.isFormField) {
      const next = new Date(this.year, this.month - 1 + delta, 1);
      this.apply(next.getMonth() + 1, next.getFullYear());
    }
    this.monthStep.emit(delta);
  }

  goToCurrent(): void {
    if (this.disabled) return;
    const now = new Date();
    this.close();
    this.pick(now.getMonth() + 1, now.getFullYear());
  }

  toggle(): void {
    if (this.disabled) return;
    if (this.open) {
      this.close();
      return;
    }
    this.railYear = this.year;
    this.open = true;
  }

  close(restoreFocus = false): void {
    if (!this.open) return;
    this.open = false;
    this.onTouched();
    if (restoreFocus) setTimeout(() => this.title?.nativeElement.focus());
  }

  /** Un clic afuera cierra el panel, salvo en el propio título: ese clic ya lo abre o lo cierra. */
  onOutsideClick(event: MouseEvent): void {
    if (event.target instanceof Node && this.title?.nativeElement.contains(event.target)) return;
    this.close();
  }

  /** Escape cierra sólo el panel: no debe llegar al modal que contiene el campo y cerrarlo también. */
  onPanelKeydown(event: KeyboardEvent): void {
    if (event.key !== 'Escape') return;
    event.stopPropagation();
    this.close(true);
  }

  /** Al abrir, el foco va al mes elegido (o al mes en curso, o al primero) para seguir con el teclado. */
  focusPanel(): void {
    setTimeout(() => {
      const panel = document.getElementById(this.panelId);
      if (!panel) return;
      const target = panel.querySelector<HTMLElement>('.mp-panel-month-active')
        ?? panel.querySelector<HTMLElement>('.mp-panel-month-current:not(:disabled)')
        ?? panel.querySelector<HTMLElement>('.mp-panel-month:not(:disabled)');
      target?.focus();
    });
  }

  shiftRailYear(delta: number): void {
    if ((delta < 0 && !this.canGoToPreviousRailYear) || (delta > 0 && !this.canGoToNextRailYear)) return;
    this.railYear += delta;
  }

  isActive(monthIndex: number): boolean {
    return this.hasValue && this.year === this.railYear && this.month === monthIndex + 1;
  }

  isCurrentMonth(monthIndex: number): boolean {
    const now = new Date();
    return now.getFullYear() === this.railYear && now.getMonth() === monthIndex;
  }

  /** Un mes posterior al último permitido no se puede elegir. */
  isMonthDisabled(monthIndex: number): boolean {
    const max = this.maxMonth;
    return !!max && (this.railYear > max.year || (this.railYear === max.year && monthIndex + 1 > max.month));
  }

  select(monthIndex: number): void {
    if (this.isMonthDisabled(monthIndex)) return;
    this.close(true);
    this.pick(monthIndex + 1, this.railYear);
  }

  private pick(month: number, year: number): void {
    if (this.isFormField) this.apply(month, year);
    this.monthSelect.emit({ month, year });
  }

  /** Sólo como campo de formulario: toma el mes y avisa al formulario con «YYYY-MM». */
  private apply(month: number, year: number): void {
    this.month = month;
    this.year = year;
    this.hasValue = true;
    this.onChange(`${year}-${String(month).padStart(2, '0')}`);
  }
}
