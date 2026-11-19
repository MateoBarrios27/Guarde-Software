import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { AlertService, SystemAlert } from '../../../core/services/alert-service/alert.service';
import { IconComponent } from '../icon/icon.component';

interface SeverityConfig {
  icon: string;
  label: string;
  /** Clase de `system-alert-modal.component.css` que pinta el encabezado con el color de la severidad. */
  toneClass: string;
}

@Component({
  selector: 'app-system-alert-modal',
  standalone: true,
  imports: [CommonModule, IconComponent],
  templateUrl: './system-alert-modal.component.html',
  styleUrls: ['./system-alert-modal.component.css']
})
export class SystemAlertModalComponent implements OnInit, OnDestroy {
  alert: SystemAlert | null = null;
  isVisible = false;
  isLeaving = false;
  private subscription!: Subscription;

  readonly severityConfig: Record<string, SeverityConfig> = {
    warning: { icon: 'alert-triangle', label: 'Advertencia', toneClass: 'system-alert--warning' },
    danger: { icon: 'alert-octagon', label: 'Urgente', toneClass: 'system-alert--danger' },
    info: { icon: 'info', label: 'Información', toneClass: 'system-alert--info' },
    maintenance: { icon: 'settings', label: 'Mantenimiento', toneClass: 'system-alert--maintenance' }
  };

  get config(): SeverityConfig {
    return this.severityConfig[this.alert?.severity ?? 'warning'];
  }

  constructor(
    private alertService: AlertService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.alertService.startConnection();
    this.subscription = this.alertService.activeAlert$.subscribe(alert => {
      if (alert) {
        this.alert = alert;
        this.isLeaving = false;
        this.isVisible = true;
      } else {
        this.hide();
      }
      this.cdr.detectChanges();
    });
  }

  dismiss(): void {
    this.hide();
    this.alertService.dismissAlert();
  }

  private hide(): void {
    this.isLeaving = true;
    setTimeout(() => {
      this.isVisible = false;
      this.isLeaving = false;
      this.alert = null;
      this.cdr.detectChanges();
    }, 350);
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
    this.alertService.stopConnection();
  }
}
