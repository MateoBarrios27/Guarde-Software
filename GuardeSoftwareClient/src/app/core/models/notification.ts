export type NotificationSeverity = 'info' | 'success' | 'warning' | 'danger';

export interface UserNotification {
  id: number;
  sourceType: string;
  severity: NotificationSeverity;
  title: string;
  message: string;
  actionUrl: string | null;
  createdAt: string;
  isRead: boolean;
}

export interface NotificationInbox {
  items: UserNotification[];
  unreadCount: number;
}
