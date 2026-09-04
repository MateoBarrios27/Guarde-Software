export interface CommunicationDispatchDto {
  dispatchId: number;
  clientId: number;
  externalRecipientId?: number | null;
  isExternalRecipient?: boolean;
  clientName: string;
  channel: string;
  status: string;
  errorMessage: string;
  dispatchDate: string;
  recipientPhone?: string | null;
  isTest?: boolean;
  isSelected?: boolean;
  hasContent?: boolean;
}

export type CommunicationExtensionMode = 'never-attempted' | 'without-success';

export interface CommunicationExtensionRecipient {
  id: number;
  name: string;
  email: string;
  type: string | null;
  isActive: boolean;
  isAssociated: boolean;
  hasRealAttempt: boolean;
  hasRealSuccess: boolean;
  lastStatus: string | null;
  lastAttemptWasTest: boolean;
}

export interface CommunicationExtensionPreview {
  communicationId: number;
  title: string;
  status: string;
  recipientType: string;
  mode: CommunicationExtensionMode;
  totalInDirectory: number;
  eligibleWithEmail: number;
  alreadySuccessfulCount: number;
  neverAttemptedCount: number;
  previouslyAttemptedCount: number;
  failedOrPendingCount: number;
  alreadyAssociatedCount: number;
  newToCommunicationCount: number;
  selectedForSendCount: number;
  inactiveOrWithoutEmailCount: number;
  isTestCommunication: boolean;
  candidateListTruncated: boolean;
  recipients: CommunicationExtensionRecipient[];
}

export interface ExtendCommunicationRequest {
  recipientType: string;
  mode: CommunicationExtensionMode;
}

export interface CommunicationExtensionResult extends CommunicationExtensionPreview {
  queued: boolean;
  addedAssociationCount: number;
  communication?: ComunicacionDto | null;
}

export interface CommunicationExternalRecipientDto {
  id: number;
  name: string | null;
  email: string | null;
  type: string | null;
}

export interface ComunicacionDto {
  id: number;
  title: string;
  content: string;
  sendDate: string | null;
  sendTime: string | null;
  channel: string;
  recipients: string[];
  status: 'Draft' | 'Scheduled' | 'Procesando' | 'Finished' | 'Finished w/ Errors' | 'Failed';
  creationDate: string;
  smtpConfigId?: number | null;
  isAccountStatement: boolean;
  isNextMonthStatement: boolean;
  sendToAllEmails: boolean;
  externalRecipients: CommunicationExternalRecipientDto[];
  errorMessage?: string | null;
  dispatches?: CommunicationDispatchDto[];
}


export interface UpsertComunicacionRequest {
  id: number | null;
  title: string;
  content: string;
  sendDate: string | null;
  sendTime: string | null;
  channels: ('Email' | 'WhatsApp')[];
  recipients: string[];
  type: 'schedule' | 'draft';
  smtpConfigId?: number | null;
  isAccountStatement: boolean;
  isNextMonthStatement: boolean;
  sendToAllEmails: boolean;
  externalRecipientIds: number[];
}
