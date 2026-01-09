// core/dtos/communications/communicationDto.ts

/**
 * DTO for displaying a communication (matches backend)
 * (Comentarios en español solo para aclarar el cambio)
 */
export interface ComunicacionDto {
  id: number;
  title: string;
  content: string;
  sendDate: string | null;
  sendTime: string | null;
  channel: string;
  recipients: string[];
  // CORRECCIÓN: Los estados deben ser los que envía el backend
  status: 'Draft' | 'Scheduled' | 'Processing' | 'Finished' | 'Finished w/ Errors' | 'Failed';
  creationDate: string;
  smtpConfigId?: number | null;
  isAccountStatement: boolean;
}

/**
 * DTO for creating/editing a communication (matches backend)
 */
export interface UpsertComunicacionRequest {
  id: number | null;
  title: string;
  content: string;
  sendDate: string | null;
  sendTime: string | null;
  channels: ('Email' | 'WhatsApp')[];
  recipients: string[];
  // CORRECCIÓN: El tipo debe ser el que espera el backend
  type: 'schedule' | 'draft';
  smtpConfigId?: number | null;
  isAccountStatement: boolean;
}