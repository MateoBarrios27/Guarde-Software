export const LOGISTICAS_TEMPLATE_MARKER = 'GUARDE_TEMPLATE:LOGISTICAS_V1';
const COPY_START = '<!--GUARDE_LOGISTICAS_COPY_START-->';
const COPY_END = '<!--GUARDE_LOGISTICAS_COPY_END-->';

/** Edit only the copy with Quill; keep the table layout, CID image and links intact. */
export function getLogisticasTemplateCopy(html: string): string {
  const start = html.indexOf(COPY_START);
  const end = html.indexOf(COPY_END, start + COPY_START.length);
  return start >= 0 && end >= 0 ? html.slice(start + COPY_START.length, end).trim() : '';
}

export function updateLogisticasTemplateCopy(html: string, copy: string | null): string {
  const start = html.indexOf(COPY_START);
  const end = html.indexOf(COPY_END, start + COPY_START.length);
  if (start < 0 || end < 0) return html;
  return html.slice(0, start + COPY_START.length) + '\n' + (copy ?? '') + '\n' + html.slice(end);
}
