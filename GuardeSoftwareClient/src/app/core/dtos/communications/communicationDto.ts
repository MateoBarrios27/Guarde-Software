export interface CommunicationDispatchDto {
  dispatchId: number;
  clientId: number;
  clientName: string;
  channel: string;
  status: string;
  errorMessage: string;
  dispatchDate: string;
  recipientPhone?: string | null;
  isSelected?: boolean;
  hasContent?: boolean;
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
}
