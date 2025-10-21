// DTO para mostrar la lista de comunicaciones
export interface ComunicacionDto {
  id: number;
  title: string;
  content: string; // El contenido principal (el backend lo tomará de comunicados_x_canal)
  sendDate: string | null;
  sendTime: string | null;
  channel: string; // "Email", "WhatsApp", "Email + WhatsApp"
  recipients: string[]; // ["Todos los clientes", "Juan Pérez"]
  status: 'Enviado' | 'Programado' | 'Borrador';
  creationDate: string;
}

// DTO para crear o editar una comunicación (basado en tu FormDataState)
export interface UpsertComunicacionRequest {
  id: number | null; // null para crear, ID para editar
  title: string;
  content: string; // El contenido único que el backend duplicará por canal
  sendDate: string | null;
  sendTime: string | null;
  channels: ('Email' | 'WhatsApp')[];
  recipients: string[]; // ["Todos los clientes", "Juan Pérez"]
  type: 'programar' | 'borrador'; // 'schedule' or 'draft'
}