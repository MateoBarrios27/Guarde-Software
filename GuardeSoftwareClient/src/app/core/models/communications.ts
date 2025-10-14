// --- en src/app/comunicaciones/comunicacion.models.ts ---

export interface Communication {
  id: number;
  title: string;
  content: string;
  sendDate: string | null; // Use null for drafts
  sendTime: string | null; // Use null for drafts
  channel: 'Email' | 'WhatsApp';
  recipients: string[];
  status: 'Scheduled' | 'Draft' | 'Sent';
  createdAt: string;
}

// Interface for the list of available clients/groups
export interface ClientOption {
  id: string; // Can be a group name or a specific ID
  name: string;
}

export interface ContenidoEmailRequest {
  subject: string;
  content: string;
}

export interface ContenidoWhatsAppRequest {
  content: string;
}
