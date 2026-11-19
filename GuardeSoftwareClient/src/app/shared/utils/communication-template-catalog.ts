import { getLogisticasTemplateCopy, LOGISTICAS_TEMPLATE_MARKER } from './logisticas-template.util';
import { ICBC_2_TEMPLATE_CONTENT, ICBC_TEMPLATE_CONTENT } from './communication-template-icbc';

export const INMOBILIARIAS_TEMPLATE_MARKER = 'GUARDE_TEMPLATE:INMOBILIARIAS_V1';
export const LABORATORIOS_TEMPLATE_MARKER = 'GUARDE_TEMPLATE:LABORATORIOS_V1';
export const VISITADORES_MEDICOS_TEMPLATE_MARKER = 'GUARDE_TEMPLATE:VISITADORES_MEDICOS_V1';
export { LOGISTICAS_TEMPLATE_MARKER };

/**
 * Plantillas del sistema. Su contenido original vive acá (en un archivo de assets o en texto) y la base sólo
 * guarda nombre, asunto, orden y, si alguien lo editó, el contenido modificado (communication_templates).
 * El orden y los asuntos por defecto deben coincidir con CommunicationTemplateService.SystemTemplates y con
 * la semilla de Database/Update_Add_CommunicationTemplates.sql.
 */
export interface SystemTemplateDefinition {
  key: string;
  name: string;
  subject: string;
  /** Archivo con el HTML diseñado (se pide por HTTP). */
  assetUrl?: string;
  /** HTML escrito en el código. */
  inlineContent?: string;
  /** Comprueba que el archivo descargado sea la plantilla (y no, por ejemplo, el index.html de la app). */
  isValid?: (html: string) => boolean;
}

export const SYSTEM_TEMPLATES: readonly SystemTemplateDefinition[] = [
  {
    key: 'icbc',
    name: 'ICBC',
    subject: 'Beneficio especial ICBC para clientes',
    inlineContent: ICBC_TEMPLATE_CONTENT,
  },
  {
    key: 'inmobiliarias',
    name: 'Inmobiliarias',
    subject: 'PUBLICIDAD | Una solución de guardado para tu inmobiliaria y tus clientes',
    assetUrl: 'assets/email-templates/inmobiliarias/inmobiliarias.html',
    isValid: html => html.includes(INMOBILIARIAS_TEMPLATE_MARKER),
  },
  {
    key: 'logisticas',
    name: 'Logísticas',
    subject: 'Tu logística, con más espacio.',
    assetUrl: 'assets/email-templates/logisticas/logisticas.html',
    isValid: html => html.includes(LOGISTICAS_TEMPLATE_MARKER) && !!getLogisticasTemplateCopy(html),
  },
  {
    key: 'laboratorios',
    name: 'Laboratorios',
    subject: 'PUBLICIDAD | Espacio flexible para materiales y equipamiento de tu laboratorio',
    assetUrl: 'assets/email-templates/laboratorios/laboratorios.html',
    isValid: html => html.includes(LABORATORIOS_TEMPLATE_MARKER),
  },
  {
    key: 'visitadores-medicos',
    name: 'Visitadores médicos',
    subject: 'PUBLICIDAD | Más espacio para organizar tu material de trabajo',
    assetUrl: 'assets/email-templates/visitadores-medicos/visitadores-medicos.html',
    isValid: html => html.includes(VISITADORES_MEDICOS_TEMPLATE_MARKER),
  },
  {
    key: 'icbc-2',
    name: 'ICBC 2',
    subject: 'Beneficios ICBC para nuestros clientes',
    inlineContent: ICBC_2_TEMPLATE_CONTENT,
  },
];

export function getSystemTemplate(key: string | null | undefined): SystemTemplateDefinition | undefined {
  return key ? SYSTEM_TEMPLATES.find(template => template.key === key) : undefined;
}

/**
 * Una plantilla «diseñada» es un documento de correo completo (tablas, estilos, imágenes CID). Quill la
 * reescribiría y perdería el diseño, así que se edita como HTML con vista previa.
 */
export function isDesignedTemplateContent(content: string | null | undefined): boolean {
  const value = content ?? '';
  return /<html(?:\s|>)/i.test(value)
    || /<table\b/i.test(value)
    || value.includes('GUARDE_TEMPLATE:');
}
