import { Component, ElementRef, EventEmitter, HostListener, Input, OnInit, Output, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators, FormsModule } from '@angular/forms'; // <-- Agregado FormsModule
import { IconComponent } from '../icon/icon.component';
import { AccountMovementService } from '../../../core/services/accountMovement-service/account-movement.service';
import { CreateAccountMovementDTO } from '../../../core/dtos/accountMovement/create-account-movement.dto';
import Swal from '../../services/ui-alert.service';
import { CurrencyFormatDirective } from "../../directives/currency-format.directive";

@Component({
  selector: 'app-create-movement-modal',
  standalone: true,
  // IMPORTANTE: Asegurate de que FormsModule esté acá
  imports: [CommonModule, ReactiveFormsModule, FormsModule, IconComponent, CurrencyFormatDirective], 
  templateUrl: './create-movement-modal.component.html',
})
export class CreateMovementModalComponent implements OnInit {
  @ViewChild('conceptAutocomplete', { static: true })
  private conceptAutocomplete?: ElementRef<HTMLElement>;

  @Input() clientId!: number;
  @Output() closeModal = new EventEmitter<void>();
  @Output() saveSuccess = new EventEmitter<void>();

  newMovementForm!: FormGroup;
  isLoading = false;
  showConceptSuggestions = false;

  private readonly spanishMonths = [
    'enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio',
    'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'
  ];

  // --- VARIABLES PARA LA FECHA MANUAL ---
  manualDateEnabled = false;
  dateString: string = new Date().toISOString().split('T')[0];

  constructor(
    private fb: FormBuilder,
    private accountMovementService: AccountMovementService
  ) {}

  ngOnInit(): void {
    if (!this.clientId) {
      console.error("Error: ClientID no fue proporcionado al modal de creación de movimiento.");
    }

    this.newMovementForm = this.fb.group({
      movementType: ['DEBITO', Validators.required],
      amount: [null, [Validators.required, Validators.min(0.01)]],
      concept: ['', [Validators.required, Validators.maxLength(255)]],
    });
  }

  get conceptSuggestions(): string[] {
    const now = new Date();
    const month = this.spanishMonths[now.getMonth()];
    const formattedMonth = month.charAt(0).toUpperCase() + month.slice(1);
    const nextMonthDate = new Date(now.getFullYear(), now.getMonth() + 1, 1);
    const nextMonth = this.spanishMonths[nextMonthDate.getMonth()];
    const formattedNextMonth = nextMonth.charAt(0).toUpperCase() + nextMonth.slice(1);

    return [
      `Interés por mora de ${formattedMonth} ${now.getFullYear()}`,
      `Alquiler ${formattedMonth} ${now.getFullYear()}`,
      `Alquiler ${formattedNextMonth} ${nextMonthDate.getFullYear()}`,
      'Proporcional'
    ];
  }

  openConceptSuggestions(): void {
    this.showConceptSuggestions = true;
  }

  @HostListener('document:pointerdown', ['$event'])
  onDocumentPointerDown(event: PointerEvent): void {
    if (!this.showConceptSuggestions) {
      return;
    }

    const target = event.target as Node | null;
    if (target && this.conceptAutocomplete?.nativeElement.contains(target)) {
      return;
    }

    this.showConceptSuggestions = false;
  }

  onConceptFocusOut(event: FocusEvent): void {
    const nextTarget = event.relatedTarget as Node | null;
    if (nextTarget && this.conceptAutocomplete?.nativeElement.contains(nextTarget)) {
      return;
    }

    this.showConceptSuggestions = false;
  }

  selectConceptSuggestion(suggestion: string): void {
    const conceptControl = this.newMovementForm.get('concept');
    conceptControl?.setValue(suggestion);
    conceptControl?.markAsDirty();
    this.showConceptSuggestions = false;
  }

  // --- MÉTODO PARA ALTERNAR EL CALENDARIO ---
  toggleManualDate() {
    this.manualDateEnabled = !this.manualDateEnabled;
    if (!this.manualDateEnabled) {
      this.dateString = new Date().toISOString().split('T')[0];
    }
  }

  onSubmit(): void {
    if (this.newMovementForm.invalid) {
      this.newMovementForm.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    const formValue = this.newMovementForm.value;

    let finalDate = new Date();
    if (this.manualDateEnabled && this.dateString) {
      const [year, month, day] = this.dateString.split('-').map(Number);
      const currentTime = new Date();
      finalDate = new Date(year, month - 1, day, currentTime.getHours(), currentTime.getMinutes(), currentTime.getSeconds());
    }

    const adjustedDate = new Date(finalDate.getTime() - (finalDate.getTimezoneOffset() * 60000));

    const dto: CreateAccountMovementDTO = {
      clientId: this.clientId,
      movementType: formValue.movementType,
      amount: formValue.amount,
      concept: formValue.concept,
      date: adjustedDate 
    };

    this.accountMovementService.createMovement(dto).subscribe({
      next: () => {
        this.isLoading = false;
        Swal.fire({
          icon: 'success',
          title: 'Movimiento Creado',
          text: 'El nuevo movimiento se ha registrado exitosamente.',
          confirmButtonColor: '#2563eb'
        });
        this.saveSuccess.emit();
      },
      error: (err) => {
        this.isLoading = false;
        console.error('Error al crear movimiento:', err);
        Swal.fire({
          icon: 'error',
          title: 'Error',
          text: 'No se pudo crear el movimiento. ' + (err.error?.message || 'Error desconocido.'),
          confirmButtonColor: '#2563eb'
        });
      },
    });
  }
}
