import { Injectable } from '@angular/core';

export interface ReceiptData {
  date: string;
  clientNumber: number;
  amount: number;
  clientName: string;
  description: string;
}

@Injectable({
  providedIn: 'root'
})
export class PdfGeneratorService {

  constructor() {}

  async generateBauleraReceipt(data: ReceiptData): Promise<void> {
    const pdfMakeModule = await import('pdfmake/build/pdfmake');
    const pdfFontsModule = await import('pdfmake/build/vfs_fonts');

    const pdfMake: any = (pdfMakeModule as any).default || pdfMakeModule;
    const pdfFonts: any = pdfFontsModule as any;
    pdfMake.vfs = pdfFonts.vfs || (pdfFonts.pdfMake && pdfFonts.pdfMake.vfs);

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
                    { 
                      text: `SEÑORES: ${data.clientName}`, 
                      alignment: 'left', 
                      bold: true, 
                      margin: [3, 10, 0, 4] 
                    }
                  ],
                  {
                    text: 'X',
                    alignment: 'center',
                    width: 'auto',
                    bold: true,
                    fontSize: 15
                  },
                  [
                    { text: 'FECHA', bold: true, alignment: 'right', margin: [0, 0, 3, 3]},
                    { text: data.date, alignment: 'right', fontSize: 9,margin: [0, 0, 3, 3] }
                  ]
                ]
              }
            ]]
          },
          layout: {
            hLineWidth: function (i: number) {
              return i === 1 ? 0 : 0.8;
            },
            vLineWidth: function () {
              return 0.8;
            }
          }

        },

        {
          table: {
            widths: ['auto', '*', 'auto', 80],
            body: [
              [
                { text: 'DESCRIPCIÓN', bold: true, alignment: 'center', colSpan: 3 },
                {},
                {},
                { text: 'PRECIO', bold: true, alignment: 'center' }
              ],
              [
                {
                  text: data.description,
                  colSpan: 3,
                  margin: [3, 8, 0, 530] 
                },
                {},
                {},
                {
                  text: '$ ' + data.amount.toFixed(2),
                  alignment: 'right',
                  margin: [0, 8, 3, 530]
                }
              ],
              [
                { text: 'Cliente Nº', bold: true },
                { text: String(data.clientNumber || ''), margin: [3, 0, 0, 0] },
                { text: 'TOTAL', bold: true, alignment: 'right' },
                { text: '$ ' + data.amount.toFixed(2), alignment: 'right' }
              ]
            ]
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
