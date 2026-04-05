import { Injectable } from '@angular/core';

export interface ReceiptConcept {
  description: string;
  amount: number;
}

export interface ReceiptData {
  date: string;
  clientNumber: number;
  clientName: string;
  concepts: ReceiptConcept[];
  totalAmount: number;
}

@Injectable({
  providedIn: 'root'
})
export class PdfGeneratorService {

  constructor() {}

  private formatNumber(amount: number): string {
    return new Intl.NumberFormat('es-AR', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    }).format(amount);
  }

  async generateBauleraReceipt(data: ReceiptData): Promise<void> {
    const pdfMakeModule = await import('pdfmake/build/pdfmake');
    const pdfFontsModule = await import('pdfmake/build/vfs_fonts');

    const pdfMake: any = (pdfMakeModule as any).default || pdfMakeModule;
    const pdfFonts: any = pdfFontsModule as any;
    pdfMake.vfs = pdfFonts.vfs || (pdfFonts.pdfMake && pdfFonts.pdfMake.vfs);

    // 1. Armamos las filas de la tabla dinámicamente
    const tableBody: any[] = [
      // Cabecera
      [
        { text: 'DESCRIPCIÓN', bold: true, alignment: 'center', colSpan: 3 }, {}, {},
        { text: 'PRECIO', bold: true, alignment: 'center' }
      ]
    ];

    // 2. Agregamos cada concepto
    data.concepts.forEach(c => {
      tableBody.push([
        { text: c.description, colSpan: 3, margin: [3, 8, 0, 8] }, {}, {},
        { text: '$ ' + this.formatNumber(c.amount), alignment: 'right', margin: [0, 8, 3, 8] }
      ]);
    });

    // 3. Agregamos una fila vacía súper alta para empujar el total hacia abajo (como tenías antes)
    tableBody.push([
      { text: '', colSpan: 3, margin: [0, 0, 0, 450], border: [true, false, false, false] }, 
      {}, {}, 
      { text: '', border: [true, false, true, false] }
    ]);

    // 4. Agregamos el pie de la tabla con el total
    tableBody.push([
      { text: 'Cliente Nº', bold: true },
      { text: String(data.clientNumber || ''), margin: [3, 0, 0, 0] },
      { text: 'TOTAL', bold: true, alignment: 'right' },
      { text: '$ ' + this.formatNumber(data.totalAmount), alignment: 'right' }
    ]);

    const docDefinition: any = {
      pageSize: 'A4',
      pageMargins: [40, 50, 40, 40],          
      defaultStyle: { fontSize: 10 },        
      content: [
        {
          table: {
            widths: ['*'],
            body: [[
              {
                columns: [
                  [
                    { text: 'Guarde Lo Que Quiera', bold: true, fontSize: 13, margin: [3, 3, 0, 0] },
                    { text: 'No válido como Factura', fontSize: 7, margin: [3, 3, 0, 0] },
                    { text: `CLIENTE: ${data.clientName}`, alignment: 'left', bold: true, margin: [3, 10, 0, 4] }
                  ],
                  { text: 'X', alignment: 'center', width: 'auto', bold: true, fontSize: 15 },
                  [
                    { text: 'FECHA', bold: true, alignment: 'right', margin: [0, 0, 3, 3]},
                    { text: data.date, alignment: 'right', fontSize: 9, margin: [0, 0, 3, 3] }
                  ]
                ]
              }
            ]]
          },
          layout: {
            hLineWidth: (i: number) => i === 1 ? 0 : 0.8,
            vLineWidth: () => 0.8
          }
        },
        {
          table: {
            widths: ['auto', '*', 'auto', 80],
            body: tableBody // Acá inyectamos la tabla dinámica
          },
          layout: {
            hLineWidth: () => 0.8,
            vLineWidth: () => 0.8
          }
        }
      ]
    };

    pdfMake.createPdf(docDefinition).open();
  }
}