import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { IconComponent } from '../icon/icon.component';
import { AccountMovementService } from '../../../core/services/accountMovement-service/account-movement.service';
import { CreateAccountMovementDTO } from '../../../core/dtos/accountMovement/create-account-movement.dto';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-create-movement-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, IconComponent],
  templateUrl: './create-movement-modal.component.html',
})
export class CreateMovementModalComponent implements OnInit {
  @Input() clientId!: number;
  @Output() closeModal = new EventEmitter<void>();
  @Output() saveSuccess = new EventEmitter<void>();

  newMovementForm!: FormGroup;
  isLoading = false;

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

  onSubmit(): void {
    if (this.newMovementForm.invalid) {
      this.newMovementForm.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    const formValue = this.newMovementForm.value;

    const dto: CreateAccountMovementDTO = {
      clientId: this.clientId,
      movementType: formValue.movementType,
      amount: formValue.amount,
      concept: formValue.concept,
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
        this.saveSuccess.emit(); // Emite señal de éxito
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