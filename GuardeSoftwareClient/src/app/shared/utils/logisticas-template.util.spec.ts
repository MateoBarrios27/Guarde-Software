import { getLogisticasTemplateCopy, updateLogisticasTemplateCopy } from './logisticas-template.util';

describe('logisticas template copy', () => {
  const before = '<table><!--GUARDE_LOGISTICAS_COPY_START-->';
  const after = '<!--GUARDE_LOGISTICAS_COPY_END--><img src="cid:guarde-logisticas-flyer" width="560"><a href="https://wa.me/5491157800251">Consultar</a></table>';
  const template = before + '<p>Texto inicial</p>' + after;
  it('extracts only the copy editable with Quill', () => {
    expect(getLogisticasTemplateCopy(template)).toBe('<p>Texto inicial</p>');
  });
  it('preserves the table, CID image and CTA while editing the text', () => {
    const result = updateLogisticasTemplateCopy(template, '<p><strong>Nuevo texto</strong></p>');
    expect(result).toBe(before + '\n<p><strong>Nuevo texto</strong></p>\n' + after);
    expect(getLogisticasTemplateCopy(result)).toBe('<p><strong>Nuevo texto</strong></p>');
  });
  it('does not rewrite other templates or malformed markup', () => {
    expect(updateLogisticasTemplateCopy('<p>Otro comunicado</p>', 'nuevo')).toBe('<p>Otro comunicado</p>');
    expect(getLogisticasTemplateCopy('<p>Otro comunicado</p>')).toBe('');
    expect(updateLogisticasTemplateCopy(before, 'nuevo')).toBe(before);
  });
  it('handles empty editor content without losing the surrounding template', () => {
    expect(updateLogisticasTemplateCopy(template, null)).toBe(before + '\n\n' + after);
  });
});
