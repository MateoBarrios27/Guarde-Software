// --- NUEVA INTERFAZ ---
/** Representa un archivo adjunto que ya existe en el servidor */
export interface AttachmentDto {
  fileName: string; // "reporte-ventas.pdf"
  fileUrl: string;  // "https://tu-vps.com/uploads/guid-reporte-ventas.pdf" (o una ruta)
  // O puedes usar un ID si prefieres
  // fileId: number; 
}

/**
 * DTO for displaying a communication (matches backend)
 */
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
  
  // --- PROPIEDAD REQUERIDA ---
  // El backend debe llenar esto con los archivos guardados en el VPS
  attachments: AttachmentDto[]; 
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
  type: 'schedule' | 'draft';

  // --- NUEVO (Opcional pero recomendado) ---
  // Para manejar la eliminación de adjuntos existentes
  attachmentsToRemove?: string[]; // O 'number[]' si usas IDs
}