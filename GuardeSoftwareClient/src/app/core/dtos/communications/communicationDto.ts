export interface CommunicationDispatchDto {
  clientId: number;
  clientName: string;
  channel: string;
  status: string;
  errorMessage: string;
  dispatchDate: string;
  isSelected?: boolean;
}

export interface ComunicacionDto {
  id: number;
  title: string;
  content: string;
  sendDate: string | null;
  sendTime: string | null;
  channel: string;
  recipients: string[];
  status: 'Draft' | 'Scheduled' | 'Processing' | 'Finished' | 'Finished w/ Errors' | 'Failed';
  creationDate: string;
  smtpConfigId?: number | null;
  isAccountStatement: boolean;
  isNextMonthStatement: boolean;
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
}