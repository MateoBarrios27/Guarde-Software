import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  OnDestroy,
  OnInit,
  Output,
  ViewChild,
  computed,
  signal
} from '@angular/core';
import { ChatBusinessDay, ChatBusinessHours } from '../../../core/models/chat';
import { ChatService } from '../../../core/services/chat-service/chat.service';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import UiAlertService from '../../../shared/services/ui-alert.service';

interface EditableDay {
  dayOfWeek: number;
  name: string;
  isOpen: boolean;
  opensAt: string;
  closesAt: string;
}

const DAY_NAMES = ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo'];
const DEFAULT_OPENS_AT = '09:30';
const DEFAULT_CLOSES_AT = '15:30';

@Component({
  selector: 'app-chat-hours-modal',
  standalone: true,
  imports: [CommonModule, IconComponent],
  templateUrl: './chat-hours-modal.component.html',
  styleUrls: ['./chat-hours-modal.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChatHoursModalComponent implements OnInit, AfterViewInit, OnDestroy {
  @Input({ required: true }) hours!: ChatBusinessHours;
  @Input() canEdit = false;
  @Output() closed = new EventEmitter<void>();
  @Output() saved = new EventEmitter<ChatBusinessHours>();

  @ViewChild('dialog', { static: true }) private dialog!: ElementRef<HTMLElement>;

  readonly days = signal<EditableDay[]>([]);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  /** Días abiertos cuya apertura no es anterior al cierre. */
  readonly invalidDays = computed(() => new Set(
    this.days().filter(day => day.isOpen && (!day.opensAt || !day.closesAt || day.opensAt >= day.closesAt))
      .map(day => day.dayOfWeek)));
  readonly canSave = computed(() => this.canEdit && !this.saving() && this.invalidDays().size === 0);

  private previousFocus: Element | null = null;
  private previousMainOverflow = '';

  constructor(private readonly chat: ChatService) {}

  ngOnInit(): void {
    this.days.set(Array.from({ length: 7 }, (_, index) => {
      const source = this.hours.days.find(day => day.dayOfWeek === index + 1);
      return {
        dayOfWeek: index + 1,
        name: DAY_NAMES[index],
        isOpen: source?.isOpen ?? false,
        opensAt: source?.opensAt ?? DEFAULT_OPENS_AT,
        closesAt: source?.closesAt ?? DEFAULT_CLOSES_AT
      };
    }));
  }

  ngAfterViewInit(): void {
    // La página se desplaza dentro de #main-scroll: se bloquea ahí mientras el modal está abierto.
    this.previousFocus = document.activeElement;
    const main = document.getElementById('main-scroll');
    if (main) {
      this.previousMainOverflow = main.style.overflow;
      main.style.overflow = 'hidden';
    }
    this.dialog.nativeElement.focus();
  }

  ngOnDestroy(): void {
    const main = document.getElementById('main-scroll');
    if (main) main.style.overflow = this.previousMainOverflow;
    if (this.previousFocus instanceof HTMLElement) this.previousFocus.focus({ preventScroll: true });
  }

  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      // Escape cierra este modal y no la pantalla de abajo.
      event.stopPropagation();
      this.close();
      return;
    }
    if (event.key === 'Tab') this.trapFocus(event);
  }

  close(): void {
    if (!this.saving()) this.closed.emit();
  }

  toggleDay(dayOfWeek: number, isOpen: boolean): void {
    this.update(dayOfWeek, { isOpen });
  }

  setTime(dayOfWeek: number, field: 'opensAt' | 'closesAt', value: string): void {
    this.update(dayOfWeek, { [field]: value });
  }

  save(): void {
    if (!this.canSave()) return;

    const days: ChatBusinessDay[] = this.days().map(day => ({
      dayOfWeek: day.dayOfWeek,
      isOpen: day.isOpen,
      opensAt: day.isOpen ? day.opensAt : null,
      closesAt: day.isOpen ? day.closesAt : null
    }));

    this.saving.set(true);
    this.error.set(null);
    this.chat.updateBusinessHours(days).subscribe({
      next: hours => {
        this.saving.set(false);
        void UiAlertService.success('Horario guardado', 'El chat de la web usa el nuevo horario desde ahora.');
        this.saved.emit(hours);
      },
      error: error => {
        this.saving.set(false);
        this.error.set(error?.error?.message ?? 'No se pudo guardar el horario. Intentá de nuevo.');
      }
    });
  }

  private update(dayOfWeek: number, changes: Partial<EditableDay>): void {
    this.error.set(null);
    this.days.update(days => days.map(day => day.dayOfWeek === dayOfWeek ? { ...day, ...changes } : day));
  }

  private trapFocus(event: KeyboardEvent): void {
    const focusable = Array.from(this.dialog.nativeElement.querySelectorAll<HTMLElement>(
      'button:not([disabled]), input:not([disabled]), [tabindex]:not([tabindex="-1"])'));
    if (focusable.length === 0) {
      event.preventDefault();
      return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const active = document.activeElement;
    if (event.shiftKey && (active === first || active === this.dialog.nativeElement)) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && active === last) {
      event.preventDefault();
      first.focus();
    }
  }
}
