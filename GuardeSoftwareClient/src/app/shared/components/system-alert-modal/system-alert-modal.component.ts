import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { AlertService, SystemAlert } from '../../../core/services/alert-service/alert.service';
import { IconComponent } from '../icon/icon.component';

interface SeverityConfig {
  icon: string;
  label: string;
  containerClass: string;
  iconBgClass: string;
  iconColorClass: string;
  titleClass: string;
  messageClass: string;
  badgeClass: string;
  btnClass: string;
  progressClass: string;
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
    warning: {
      icon: 'alert-triangle',
      label: 'Advertencia',
      containerClass: 'border-amber-300 bg-amber-50',
      iconBgClass: 'bg-amber-100',
      iconColorClass: 'text-amber-600',
      titleClass: 'text-amber-900',
      messageClass: 'text-amber-800',
      badgeClass: 'bg-amber-100 text-amber-700 border border-amber-300',
      btnClass: 'bg-amber-500 hover:bg-amber-600 text-white',
      progressClass: 'bg-amber-400'
    },
    danger: {
      icon: 'alert-octagon',
      label: 'Urgente',
      containerClass: 'border-red-300 bg-red-50',
      iconBgClass: 'bg-red-100',
      iconColorClass: 'text-red-600',
      titleClass: 'text-red-900',
      messageClass: 'text-red-800',
      badgeClass: 'bg-red-100 text-red-700 border border-red-300',
      btnClass: 'bg-red-500 hover:bg-red-600 text-white',
      progressClass: 'bg-red-400'
    },
    info: {
      icon: 'info',
      label: 'Información',
      containerClass: 'border-blue-300 bg-blue-50',
      iconBgClass: 'bg-blue-100',
      iconColorClass: 'text-blue-600',
      titleClass: 'text-blue-900',
      messageClass: 'text-blue-800',
      badgeClass: 'bg-blue-100 text-blue-700 border border-blue-300',
      btnClass: 'bg-blue-500 hover:bg-blue-600 text-white',
      progressClass: 'bg-blue-400'
    },
    maintenance: {
      icon: 'settings',
      label: 'Mantenimiento',
      containerClass: 'border-violet-300 bg-violet-50',
      iconBgClass: 'bg-violet-100',
      iconColorClass: 'text-violet-600',
      titleClass: 'text-violet-900',
      messageClass: 'text-violet-800',
      badgeClass: 'bg-violet-100 text-violet-700 border border-violet-300',
      btnClass: 'bg-violet-500 hover:bg-violet-600 text-white',
      progressClass: 'bg-violet-400'
    }
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
