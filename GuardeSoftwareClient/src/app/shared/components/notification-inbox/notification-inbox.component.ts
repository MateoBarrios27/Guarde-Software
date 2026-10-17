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

  private readonly autoOpenDelayMs = 1_500;
  private readonly subscriptions = new Subscription();
  private readonly pendingAutoOpenSignatures = new Set<string>();
  private readonly inMemoryAutoOpenedSignatures = new Set<string>();
  private refreshTimer?: ReturnType<typeof setInterval>;
  private autoOpenTimer?: ReturnType<typeof setTimeout>;
  private pageHasLoaded = document.readyState === 'complete';
  private readonly windowLoadHandler = (): void => {
    this.pageHasLoaded = true;
    this.scheduleAutoOpen();
  };

  constructor(
    private readonly notificationService: NotificationService,
    private readonly alertService: AlertService,
    private readonly router: Router,
    private readonly elementRef: ElementRef<HTMLElement>
  ) {}

  ngOnInit(): void {
    if (!this.pageHasLoaded) {
      window.addEventListener('load', this.windowLoadHandler, { once: true });
    }
    this.loadNotifications();
    this.subscriptions.add(
      this.alertService.notificationsChanged$.subscribe(() => this.loadNotifications())
    );
    this.refreshTimer = setInterval(() => this.loadNotifications(), 60_000);
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    if (this.refreshTimer) clearInterval(this.refreshTimer);
    if (this.autoOpenTimer) clearTimeout(this.autoOpenTimer);
    window.removeEventListener('load', this.windowLoadHandler);
  }

  toggle(event: MouseEvent): void {
    event.stopPropagation();
    this.cancelPendingAutoOpen(true);
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

  deleteNotification(notification: UserNotification, event: MouseEvent): void {
    event.stopPropagation();

    this.notifications = this.notifications.filter(item => item.id !== notification.id);
    if (!notification.isRead) {
      this.unreadCount = Math.max(0, this.unreadCount - 1);
    }

    this.notificationService.deleteForCurrentUser(notification.id).subscribe({
      error: () => this.loadNotifications()
    });
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
        this.considerAutoOpen(inbox.items);
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

  private considerAutoOpen(notifications: UserNotification[]): void {
    const alreadyAutoOpened = this.getAutoOpenedSignatures();
    const unseenSignatures = notifications
      .filter(notification => !notification.isRead)
      .map(notification => this.notificationSignature(notification))
      .filter(signature => !alreadyAutoOpened.has(signature));

    if (unseenSignatures.length === 0) return;

    unseenSignatures.forEach(signature => this.pendingAutoOpenSignatures.add(signature));
    if (this.isOpen) {
      this.rememberPendingAutoOpenSignatures();
      return;
    }

    this.scheduleAutoOpen();
  }

  private scheduleAutoOpen(): void {
    if (!this.pageHasLoaded || this.autoOpenTimer || this.pendingAutoOpenSignatures.size === 0) return;

    this.autoOpenTimer = setTimeout(() => {
      this.autoOpenTimer = undefined;
      if (this.pendingAutoOpenSignatures.size === 0) return;

      this.isOpen = true;
      this.rememberPendingAutoOpenSignatures();
    }, this.autoOpenDelayMs);
  }

  private cancelPendingAutoOpen(markAsSeen: boolean): void {
    if (this.autoOpenTimer) {
      clearTimeout(this.autoOpenTimer);
      this.autoOpenTimer = undefined;
    }
    if (markAsSeen) {
      this.rememberPendingAutoOpenSignatures();
    } else {
      this.pendingAutoOpenSignatures.clear();
    }
  }

  private rememberPendingAutoOpenSignatures(): void {
    if (this.pendingAutoOpenSignatures.size === 0) return;

    const signatures = this.getAutoOpenedSignatures();
    this.pendingAutoOpenSignatures.forEach(signature => {
      signatures.add(signature);
      this.inMemoryAutoOpenedSignatures.add(signature);
    });
    this.pendingAutoOpenSignatures.clear();

    try {
      localStorage.setItem(this.autoOpenStorageKey(), JSON.stringify(Array.from(signatures).slice(-200)));
    } catch {
      // El fallback en memoria evita reaperturas durante la sesión si el navegador bloquea localStorage.
    }
  }

  private getAutoOpenedSignatures(): Set<string> {
    const signatures = new Set(this.inMemoryAutoOpenedSignatures);
    try {
      const stored = JSON.parse(localStorage.getItem(this.autoOpenStorageKey()) ?? '[]');
      if (Array.isArray(stored)) {
        stored.filter(value => typeof value === 'string').forEach(value => signatures.add(value));
      }
    } catch {
      // Un valor local inválido no debe impedir cargar ni abrir la bandeja.
    }
    return signatures;
  }

  private autoOpenStorageKey(): string {
    const userName = localStorage.getItem('userName')?.trim().toLowerCase() || 'anonymous';
    return `guarde.notifications.auto-opened.${userName}`;
  }

  private notificationSignature(notification: UserNotification): string {
    return `${notification.id}:${notification.createdAt}`;
  }
}
