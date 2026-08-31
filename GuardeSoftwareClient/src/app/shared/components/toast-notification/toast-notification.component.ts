import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { IconComponent } from '../icon/icon.component';

@Component({
  selector: 'app-toast-notification',
  standalone: true,
  imports: [CommonModule, IconComponent],
  templateUrl: './toast-notification.component.html',
})
export class ToastNotificationComponent {
  @Input() visible = false;
  @Input() message = '';
  @Input() type: 'success' | 'error' = 'success';
}
