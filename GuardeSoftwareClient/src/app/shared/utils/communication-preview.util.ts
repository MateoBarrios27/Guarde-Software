const PREVIEW_STYLES = `
  :root {
    color-scheme: light;
  }

  *, *::before, *::after {
    box-sizing: border-box;
  }

  html {
    min-height: 100%;
    background: #f8fafc;
  }

  body {
    min-height: 100%;
    margin: 0;
    padding: 24px;
    background: #f8fafc;
    color: #1f2937;
    font-family: Arial, Helvetica, sans-serif;
    line-height: 1.55;
  }

  img, video {
    max-width: 100%;
    height: auto;
  }

  table {
    max-width: 100%;
  }

  a {
    overflow-wrap: anywhere;
  }

  .communication-preview-surface {
    width: min(100%, 760px);
    min-height: calc(100vh - 48px);
    margin: 0 auto;
    padding: 28px;
    overflow-wrap: anywhere;
    background: #ffffff;
    border: 1px solid #e5e7eb;
    border-radius: 16px;
    box-shadow: 0 12px 32px rgba(15, 23, 42, 0.08);
  }

  .communication-preview-surface.preview-plain {
    white-space: pre-wrap;
  }

  @media (max-width: 640px) {
    body {
      padding: 12px;
    }

    .communication-preview-surface {
      min-height: calc(100vh - 24px);
      padding: 18px;
      border-radius: 12px;
    }
  }
`;

const INLINE_RESOURCE_PREVIEW_URLS: Record<string, string> = {
  'cid:guarde-header': '/assets/email-templates/inmobiliarias/encabezado_guarde_16_anios.png',
  'cid:guarde-instagram': '/assets/email-templates/inmobiliarias/icon_instagram_white.png',
  'cid:guarde-web': '/assets/email-templates/inmobiliarias/icon_web_white.png',
  'cid:guarde-whatsapp': '/assets/email-templates/inmobiliarias/icon_whatsapp_white.png'
};

const LEGACY_BRAND_LOGO_IMAGE_REGEX =
  /<img\b[^>]*guardeloquequiera-logo(?:\.jpg)?[^>]*>/gi;

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

function hasHtmlMarkup(value: string): boolean {
  return /<\/?[a-z][\s\S]*?>/i.test(value);
}

export function removeLegacyCommunicationBrandLogo(value: string): string {
  return value.replace(LEGACY_BRAND_LOGO_IMAGE_REGEX, '');
}

export function buildCommunicationPreviewText(
  content: string | null | undefined,
  maxLength = 150
): string {
  const value = removeLegacyCommunicationBrandLogo((content ?? '').trim());
  if (!value) return '';

  if (!hasHtmlMarkup(value)) {
    return value.length > maxLength
      ? `${value.substring(0, maxLength)}...`
      : value;
  }

  // La previsualización de una tarjeta sólo necesita texto. Eliminamos todo
  // recurso o bloque no visible antes de decodificar entidades, evitando que
  // el navegador solicite imágenes externas de comunicados históricos.
  const encodedText = value
    .replace(/<head\b[^>]*>[\s\S]*?<\/head>/gi, ' ')
    .replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, ' ')
    .replace(/<style\b[^>]*>[\s\S]*?<\/style>/gi, ' ')
    .replace(/<noscript\b[^>]*>[\s\S]*?<\/noscript>/gi, ' ')
    .replace(/<img\b[^>]*>/gi, ' ')
    .replace(/<(?:br|hr)\b[^>]*>/gi, ' ')
    .replace(/<\/(?:p|div|li|td|tr|h[1-6]|table|section|article)>/gi, ' ')
    .replace(/<[^>]+>/g, ' ');

  const decoder = document.createElement('textarea');
  decoder.innerHTML = encodedText;
  const text = decoder.value.replace(/\s+/g, ' ').trim();

  return text.length > maxLength
    ? `${text.substring(0, maxLength)}...`
    : text;
}

function resolveInlineResourcesForPreview(value: string): string {
  return Object.entries(INLINE_RESOURCE_PREVIEW_URLS).reduce(
    (resolved, [contentId, previewUrl]) => resolved.replace(
      new RegExp(contentId, 'gi'),
      previewUrl
    ),
    value
  );
}

/**
 * Returns an isolated document suitable for iframe[srcdoc]. Existing complete
 * email documents keep their own layout; fragments receive a consistent,
 * responsive preview surface so they look like the original communication.
 */
export function buildCommunicationPreviewDocument(content: string | null | undefined): string {
  const value = resolveInlineResourcesForPreview(
    removeLegacyCommunicationBrandLogo((content ?? '').trim())
  );
  const styleTag = `<style data-communication-preview="true">${PREVIEW_STYLES}</style>`;

  if (!value) {
    return `<!doctype html><html lang="es"><head><meta charset="utf-8">${styleTag}</head><body><main class="communication-preview-surface preview-plain">Sin contenido disponible.</main></body></html>`;
  }

  if (/<html(?:\s|>)/i.test(value)) {
    if (/<\/head>/i.test(value)) {
      return value.replace(/<\/head>/i, `${styleTag}</head>`);
    }

    return value.replace(/<body(\s[^>]*)?>/i, (bodyTag) => `<head>${styleTag}</head>${bodyTag}`);
  }

  const bodyClass = hasHtmlMarkup(value)
    ? 'communication-preview-surface'
    : 'communication-preview-surface preview-plain';
  const bodyContent = hasHtmlMarkup(value) ? value : escapeHtml(value);

  return `<!doctype html><html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">${styleTag}</head><body><main class="${bodyClass}">${bodyContent}</main></body></html>`;
}
