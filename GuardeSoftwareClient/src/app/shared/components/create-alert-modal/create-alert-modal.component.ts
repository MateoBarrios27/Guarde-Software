import { Component, EventEmitter, Output, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AlertService, CreateAlertPayload } from '../../../core/services/alert-service/alert.service';
import { IconComponent } from '../icon/icon.component';

interface SeverityOption {
  value: 'warning' | 'danger' | 'info' | 'maintenance';
  label: string;
  description: string;
  icon: string;
  colorClass: string;
  selectedClass: string;
  ringClass: string;
}

@Component({
  selector: 'app-create-alert-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, IconComponent],
  templateUrl: './create-alert-modal.component.html',
  styleUrls: ['./create-alert-modal.component.css']
})
export class CreateAlertModalComponent implements OnInit {
  @Output() closeModal = new EventEmitter<void>();

  form!: FormGroup;
  isLoading = false;
  feedbackMessage = '';
  feedbackIsError = false;

  readonly severityOptions: SeverityOption[] = [
    {
      value: 'warning',
      label: 'Advertencia',
      description: 'Aviso importante no urgente',
      icon: 'alert-triangle',
      colorClass: 'text-[#C2410C]',
      selectedClass: 'alert-severity--warning',
      ringClass: ''
    },
    {
      value: 'danger',
      label: 'Urgente',
      description: 'Situación crítica inmediata',
      icon: 'alert-octagon',
      colorClass: 'text-[#E11D48]',
      selectedClass: 'alert-severity--danger',
      ringClass: ''
    },
    {
      value: 'maintenance',
      label: 'Mantenimiento',
      description: 'Tareas de mantenimiento',
      icon: 'settings',
      colorClass: 'text-[#4F46E5]',
      selectedClass: 'alert-severity--maintenance',
      ringClass: ''
    },
    {
      value: 'info',
      label: 'Información',
      description: 'Comunicado general',
      icon: 'info',
      colorClass: 'text-[#2563EB]',
      selectedClass: 'alert-severity--info',
      ringClass: ''
    }
  ];

  constructor(
    private fb: FormBuilder,
    private alertService: AlertService
  ) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      severity: ['warning', Validators.required],
      title: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(80)]],
      message: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(500)]]
    });
  }

  selectSeverity(value: 'warning' | 'danger' | 'info' | 'maintenance'): void {
    this.form.patchValue({ severity: value });
  }

  getSelectedOption(): SeverityOption {
    const val = this.form.get('severity')?.value ?? 'warning';
    return this.severityOptions.find(o => o.value === val) ?? this.severityOptions[0];
  }

  onSubmit(): void {
    if (this.form.invalid || this.isLoading) return;

    this.isLoading = true;
    this.feedbackMessage = '';

    const payload: CreateAlertPayload = this.form.value;

    this.alertService.sendAlert(payload).subscribe({
      next: () => {
        this.isLoading = false;
        this.feedbackMessage = '✓ Advertencia emitida y guardada en la bandeja de todos los usuarios.';
        this.feedbackIsError = false;
        setTimeout(() => this.closeModal.emit(), 2000);
      },
      error: (err) => {
        this.isLoading = false;
        this.feedbackMessage = 'No se pudo emitir la advertencia. Intentá nuevamente.';
        this.feedbackIsError = true;
        console.error('[CreateAlertModal] Error:', err);
      }
    });
  }
}
