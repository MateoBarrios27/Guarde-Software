export type ChatSenderType = 'visitor' | 'staff';
export type ChatConversationStatus = 'open' | 'closed';

/** sent | failed | no_email | visitor_online | limit. Sólo en respuestas de la administración. */
export type ChatEmailStatus = 'sent' | 'failed' | 'no_email' | 'visitor_online' | 'limit';

export interface ChatMessage {
  id: number;
  senderType: ChatSenderType;
  senderName: string;
  body: string;
  createdAt: string;
  receivedOutOfHours: boolean;
  emailStatus: ChatEmailStatus | null;
}

export interface ChatConversationSummary {
  id: number;
  publicId: string;
  visitorName: string;
  visitorEmail: string | null;
  visitorPhone: string | null;
  sourceUrl: string | null;
  status: ChatConversationStatus;
  createdAt: string;
  lastMessageAt: string;
  lastMessagePreview: string;
  lastMessageSender: ChatSenderType | null;
  /** El visitante escribió y la administración todavía no respondió. */
  awaitingReply: boolean;
  /** Arranca el reloj de los 2 minutos. */
  awaitingReplySince: string | null;
  awaitingOutOfHours: boolean;
  unreadCount: number;
  visitorOnline: boolean;
  assignedUserName: string | null;
}

export interface ChatConversationDetail {
  conversation: ChatConversationSummary;
  messages: ChatMessage[];
}

export interface ChatReplyResult {
  message: ChatMessage;
  conversation: ChatConversationSummary;
}

export interface ChatInboxSummary {
  awaitingReplyCount: number;
  unreadConversationCount: number;
  oldestAwaitingSince: string | null;
  isOpenNow: boolean;
  responseTimeMinutes: number;
}

export interface ChatStatus {
  isOpen: boolean;
  responseTimeMinutes: number;
  closesAtLabel: string | null;
  nextOpenLabel: string | null;
  scheduleLabel: string;
}

/** 1 = lunes ... 7 = domingo. Horas en HH:mm, hora de Argentina. */
export interface ChatBusinessDay {
  dayOfWeek: number;
  isOpen: boolean;
  opensAt: string | null;
  closesAt: string | null;
}

export interface ChatBusinessHours {
  days: ChatBusinessDay[];
  status: ChatStatus;
}

export interface ChatIncomingMessage {
  conversation: ChatConversationSummary;
  message: ChatMessage;
}

export interface ChatPresenceChange {
  publicId: string;
  online: boolean;
}
