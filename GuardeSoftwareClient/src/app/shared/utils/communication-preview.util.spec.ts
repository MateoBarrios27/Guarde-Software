import {
  buildCommunicationPreviewDocument,
  buildCommunicationPreviewText
} from './communication-preview.util';

describe('communication preview utilities', () => {
  const legacyCommunication = `
    <!doctype html>
    <html>
      <head><style>.legacy { color: red; }</style></head>
      <body>
        <img src="https://www.guardeloquequiera.com.ar/assets/imgs/guardeloquequiera-logo.jpg">
        <p>Hola &amp; equipo inmobiliario</p>
      </body>
    </html>`;

  it('builds the card summary without loading or displaying the legacy logo', () => {
    expect(buildCommunicationPreviewText(legacyCommunication))
      .toBe('Hola & equipo inmobiliario');
  });

  it('removes the legacy logo from the isolated HTML preview', () => {
    const preview = buildCommunicationPreviewDocument(legacyCommunication);

    expect(preview).not.toContain('guardeloquequiera-logo');
    expect(preview).not.toContain('www.guardeloquequiera.com.ar');
    expect(preview).toContain('Hola &amp; equipo inmobiliario');
  });
});
