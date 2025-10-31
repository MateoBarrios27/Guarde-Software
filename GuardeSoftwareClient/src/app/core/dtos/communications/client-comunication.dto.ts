export interface ClientCommunicationDTO {
  id: number;
  date: Date;
  type: string; // 'email', 'whatsapp', 'system'
  subject: string;
  snippet: string;
}
