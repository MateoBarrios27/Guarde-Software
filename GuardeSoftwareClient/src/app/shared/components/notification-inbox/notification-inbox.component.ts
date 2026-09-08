import { CommonModule } from '@angular/common';
import { Component, ElementRef, HostListener, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { UserNotification } from '../../../core/models/notification';
import { AlertService } from '../../../core/services/alert-service/alert.service';
import { NotificationService } from '../../../core/services/notification-service/notification.service';
import { IconComponent } from '../icon/icon.component';

@Component({
  selector: 'app-notification-inbox',
  standalone: true,
  imports: [CommonModule, IconComponent],
  templateUrl: './notification-inbox.component.html',
  styleUrls: ['./notification-inbox.component.css']
})
export class NotificationInboxComponent implements OnInit, OnDestroy {
  notifications: UserNotification[] = [];
  unreadCount = 0;
  isOpen = false;
  isLoading = false;
  hasLoadError = false;

  private readonly subscriptions = new Subscription();
  private refreshTimer?: ReturnType<typeof setInterval>;

  constructor(
    private readonly notificationService: NotificationService,
    private readonly alertService: AlertService,
    private readonly router: Router,
    private readonly elementRef: ElementRef<HTMLElement>
  ) {}

  ngOnInit(): void {
    this.loadNotifications();
    this.subscriptions.add(
      this.alertService.notificationsChanged$.subscribe(() => this.loadNotifications())
    );
    this.refreshTimer = setInterval(() => this.loadNotifications(), 60_000);
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    if (this.refreshTimer) clearInterval(this.refreshTimer);
  }

  toggle(event: MouseEvent): void {
    event.stopPropagation();
    this.isOpen = !this.isOpen;
    if (this.isOpen) this.loadNotifications();
  }

  close(): void {
    this.isOpen = false;
  }

  markAllAsRead(event: MouseEvent): void {
    event.stopPropagation();
    if (this.unreadCount === 0) return;

    const previous = this.notifications.map(item => ({ ...item }));
    const previousCount = this.unreadCount;
    this.notifications = this.notifications.map(item => ({ ...item, isRead: true }));
    this.unreadCount = 0;

    this.notificationService.markAllAsRead().subscribe({
      error: () => {
        this.notifications = previous;
        this.unreadCount = previousCount;
      }
    });
  }

  openNotification(notification: UserNotification, event: MouseEvent): void {
    event.stopPropagation();
    const wasUnread = !notification.isRead;
    this.markOneLocally(notification);

    if (wasUnread) {
      this.notificationService.markAsRead(notification.id).subscribe({
        error: () => this.loadNotifications()
      });
    }

    if (notification.actionUrl) {
      this.close();
      void this.router.navigateByUrl(notification.actionUrl);
    }
  }

  trackById(_: number, notification: UserNotification): number {
    return notification.id;
  }

  iconFor(notification: UserNotification): string {
    switch (notification.sourceType) {
      case 'interests_applied': return 'percent';
      case 'client_increase_due': return 'trending-up';
      case 'monthly_increase_missing': return 'calendar';
      case 'system_alert': return 'megaphone';
      default: return notification.severity === 'danger' ? 'alert-octagon' : 'info';
    }
  }

  iconClasses(notification: UserNotification): string {
    switch (notification.severity) {
      case 'danger': return 'bg-red-100 text-red-600';
      case 'warning': return 'bg-amber-100 text-amber-600';
      case 'success': return 'bg-emerald-100 text-emerald-600';
      default: return 'bg-blue-100 text-blue-600';
    }
  }

  relativeDate(value: string): string {
    const date = new Date(value);
    const elapsed = Date.now() - date.getTime();
    if (Number.isNaN(elapsed)) return '';

    const minutes = Math.floor(elapsed / 60_000);
    if (minutes < 1) return 'Recién';
    if (minutes < 60) return `Hace ${minutes} min`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `Hace ${hours} h`;
    const days = Math.floor(hours / 24);
    if (days < 7) return `Hace ${days} d`;
    return date.toLocaleDateString('es-AR', { day: '2-digit', month: 'short' });
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.isOpen && !this.elementRef.nativeElement.contains(event.target as Node)) {
      this.close();
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.close();
  }

  loadNotifications(): void {
    if (this.isLoading) return;
    this.isLoading = true;
    this.hasLoadError = false;
    this.notificationService.getInbox().subscribe({
      next: inbox => {
        this.notifications = inbox.items;
        this.unreadCount = inbox.unreadCount;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.hasLoadError = true;
      }
    });
  }

  private markOneLocally(notification: UserNotification): void {
    if (notification.isRead) return;
    notification.isRead = true;
    this.unreadCount = Math.max(0, this.unreadCount - 1);
  }
}
