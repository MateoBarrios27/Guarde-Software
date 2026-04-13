import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators, FormsModule } from '@angular/forms'; // <-- Agregado FormsModule
import { IconComponent } from '../icon/icon.component';
import { AccountMovementService } from '../../../core/services/accountMovement-service/account-movement.service';
import { CreateAccountMovementDTO } from '../../../core/dtos/accountMovement/create-account-movement.dto';
import Swal from 'sweetalert2';
import { CurrencyFormatDirective } from "../../directives/currency-format.directive";

@Component({
  selector: 'app-create-movement-modal',
  standalone: true,
  // IMPORTANTE: Asegurate de que FormsModule esté acá
  imports: [CommonModule, ReactiveFormsModule, FormsModule, IconComponent, CurrencyFormatDirective], 
  templateUrl: './create-movement-modal.component.html',
})
export class CreateMovementModalComponent implements OnInit {
  @Input() clientId!: number;
  @Output() closeModal = new EventEmitter<void>();
  @Output() saveSuccess = new EventEmitter<void>();

  newMovementForm!: FormGroup;
  isLoading = false;

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

    // --- LÓGICA DE LA FECHA ---
    // Calculamos la fecha manteniendo la hora actual para evitar problemas de zona horaria
    let finalDate = new Date();
    if (this.manualDateEnabled && this.dateString) {
      const [year, month, day] = this.dateString.split('-').map(Number);
      const currentTime = new Date();
      finalDate = new Date(year, month - 1, day, currentTime.getHours(), currentTime.getMinutes(), currentTime.getSeconds());
    }

    const dto: CreateAccountMovementDTO = {
      clientId: this.clientId,
      movementType: formValue.movementType,
      amount: formValue.amount,
      concept: formValue.concept,
      date: finalDate // <-- Enviamos la fecha configurada
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