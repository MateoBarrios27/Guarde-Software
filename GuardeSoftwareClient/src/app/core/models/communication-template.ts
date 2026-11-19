export interface CommunicationTemplate {
  id: number;
  name: string;
  subject: string;
  /** null en una plantilla del sistema que conserva su contenido original. */
  content: string | null;
  /** Clave de una plantilla del sistema (icbc, logisticas…); null en las creadas por el usuario. */
  sourceKey: string | null;
  sortOrder: number;
  updatedAt: string | null;
}

export interface CommunicationTemplateList {
  /** false mientras no se aplicó la migración: lista de sólo lectura con las plantillas del sistema. */
  storageReady: boolean;
  templates: CommunicationTemplate[];
}

export interface SaveCommunicationTemplateRequest {
  name: string;
  subject: string;
  /** En una plantilla del sistema, null conserva o restaura el contenido original. */
  content: string | null;
}
