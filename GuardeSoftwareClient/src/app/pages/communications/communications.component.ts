import { Component, signal, computed, ChangeDetectionStrategy, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent } from "../../shared/components/icon/icon.component";
import { CommunicationService } from '../../core/services/communication-service/communication.service';
import { ComunicacionDto, CommunicationDispatchDto, UpsertComunicacionRequest } from '../../core/dtos/communications/communicationDto';
import { ClientService } from '../../core/services/client-service/client.service';
import { DeleteConfirmationService } from '../../shared/services/delete-confirmation.service';
import { catchError, debounceTime, distinctUntilChanged, of, Subject, switchMap, Subscription } from 'rxjs';
import { QuillModule } from 'ngx-quill';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

interface Channel {
  id: number;
  name: 'Email' | 'WhatsApp';
  spanishLabel: 'Email' | 'WhatsApp'; // User-facing text
  icon: string;
}

/** State for the Add/Edit form */
interface FormDataState {
  id: number | null;
  title: string;
  content: string; 
  sendDate: string;
  sendTime: string;
  channels: ('Email' | 'WhatsApp')[];
  recipients: string[];
  type: 'programar' | 'borrador' | 'enviar_ahora';
  isAccountStatement: boolean;
  isNextMonthStatement: boolean;
  sendToAllEmails: boolean;
  smtpConfigId?: number | null
}

/** State for the notification toast */
interface ToastState {
  show: boolean;
  message: string;
  description: string;
  icon: string;
  color: 'success' | 'error';
}

interface ClientSelectorItem {
  id: number;
  fullName: string;
  email: string;
  balance: number;
  unpaidMonths: number;
  status: 'Moroso' | 'Pendiente' | 'AlDia';
  selected: boolean;
  currentRentAmount: number;
  nextPaymentDate: Date | null;
  paymentIdentifier?: number | null;
}

interface MonthFilter {
  label: string;
  year: number;
  month: number;
}

const COMMUNICATION_CHANNELS: Channel[] = [
  { id: 1, name: 'Email', spanishLabel: 'Email', icon: 'Mail' },
  { id: 2, name: 'WhatsApp', spanishLabel: 'WhatsApp', icon: 'whatsapp' }
];

@Component({
  selector: 'communications',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, QuillModule],
  templateUrl: './communications.component.html',
  styleUrl: './communications.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CommunicationsComponent implements OnInit, OnDestroy {

  communications = signal<ComunicacionDto[]>([]); 
  staticGroups = signal<string[]>([]); 
  searchResults = signal<string[]>([]);
  isSearchFocused = signal(false);
  private searchSubject = new Subject<string>();
  selectedFiles = signal<File[]>([]);
  smtpConfigs = signal<any[]>([]);

  showRecipientModal = signal(false);
  previewContent = signal<string | null>(null);
  previewClientName = signal('');
  allClients = signal<ClientSelectorItem[]>([]); 
  filteredClients = signal<ClientSelectorItem[]>([]); 
  recipientSearchTerm = signal('');
  
  selectedCount = computed(() => this.formData().sendToAllEmails ? 0 : this.formData().recipients.length);
  modalSelectedCount = computed(() => this.allClients().filter(c => c.selected).length);
  currentSort = signal<'name' | 'status' | 'payment_identifier'>('name');

  selectedSummary = computed(() => {
      if (this.formData().sendToAllEmails) {
        return 'Todos los emails de la base de datos';
      }

      const recipients = this.formData().recipients;
      const count = recipients.length;
      
      if (count === 0) return '';
      
      const names = recipients.slice(0, 2);
      return `${names.join(', ')} ${count > 2 ? `(+${count - 2} más)` : ''}`;
  });
  
  dynamicMonthFilters = signal<MonthFilter[]>([]);
  activeQuickFilter = signal<string | null>(null);
  modalSendToAllEmails = signal(false);

  private signalRSubscription?: Subscription;

  constructor(
    private commService: CommunicationService, 
    private clientService: ClientService,
    private sanitizer: DomSanitizer,
    private deleteConfirmation: DeleteConfirmationService
  ) {}

  private readonly icbcTemplate = `
<p style="color: #111827;"><strong>Estimado/a: {data[0]}</strong></p>
<p style="color: #1d4ed8;"><strong>POR SER CLIENTE DE GUARDE LO QUE QUIERA</strong></p>
<p style="color: #15803d;"><strong>EL BANCO ICBC LE OFRECE BONIFICACIONES EN CUENTAS, PAQUETES Y MUCHO MÁS.</strong></p>
<p style="color: #b91c1c;"><strong>CONTACTO ICBC:</strong> Natalia Pedro 113478-9917</p>
<p style="color: #6b7280;">Saludos</p>
<p style="color: #6b7280;">La Administración</p>
<p><a href="https://www.guardeloquequiera.com.ar/">guardeloquequiera.com.ar</a></p>
<p style="color: #6b7280;">011-4762-0599 / 011-4730-2192</p>
<p style="color: #15803d;">WhatsApp 115-780-0251</p>`;

  private readonly icbcTemplate2 = `
<!doctype html>
<html lang="es">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <meta name="x-apple-disable-message-reformatting">
    <title>Beneficios ICBC para nuestros clientes</title>
</head>

<body style="
    margin: 0;
    padding: 0;
    background-color: #f3f5f7;
    font-family: Arial, Helvetica, sans-serif;
    color: #263238;
">

    <!-- Texto de previsualización que aparece junto al asunto -->
    <div style="
        display: none;
        max-height: 0;
        overflow: hidden;
        opacity: 0;
        color: transparent;
        mso-hide: all;
    ">
        Consultá las bonificaciones disponibles en cuentas, paquetes bancarios
        y otras propuestas de ICBC.
    </div>

    <table
        role="presentation"
        width="100%"
        cellpadding="0"
        cellspacing="0"
        border="0"
        style="
            width: 100%;
            background-color: #f3f5f7;
            border-collapse: collapse;
        "
    >
        <tr>
            <td align="center" style="padding: 32px 12px;">

                <table
                    role="presentation"
                    width="100%"
                    cellpadding="0"
                    cellspacing="0"
                    border="0"
                    style="
                        width: 100%;
                        max-width: 620px;
                        background-color: #ffffff;
                        border-collapse: separate;
                        border-spacing: 0;
                        border-radius: 14px;
                        overflow: hidden;
                        box-shadow: 0 6px 22px rgba(25, 45, 60, 0.10);
                    "
                >

                    <!-- Encabezado -->
                    <tr>
                        <td
                            style="
                                padding: 25px 32px;
                                background-color: #17324d;
                                border-bottom: 4px solid #d71920;
                            "
                        >
                            <p style="
                                margin: 0 0 7px;
                                color: #ffffff;
                                font-size: 20px;
                                line-height: 26px;
                                font-weight: 700;
                                letter-spacing: 0.4px;
                            ">
                                GUARDE LO QUE QUIERA
                            </p>

                            <p style="
                                margin: 0;
                                color: #cbd8e3;
                                font-size: 12px;
                                line-height: 18px;
                                font-weight: 600;
                                letter-spacing: 1px;
                                text-transform: uppercase;
                            ">
                                Beneficio especial para clientes
                            </p>
                        </td>
                    </tr>

                    <!-- Presentación principal -->
                    <tr>
                        <td style="padding: 36px 32px 18px;">

                            <p style="
                                margin: 0 0 12px;
                                color: #d71920;
                                font-size: 13px;
                                line-height: 18px;
                                font-weight: 700;
                                letter-spacing: 0.9px;
                                text-transform: uppercase;
                            ">
                                Propuesta especial ICBC
                            </p>

                            <h1 style="
                                margin: 0 0 22px;
                                color: #17324d;
                                font-size: 30px;
                                line-height: 38px;
                                font-weight: 700;
                            ">
                                Más beneficios por ser nuestro cliente
                            </h1>

                            <p style="
                                margin: 0 0 18px;
                                color: #263238;
                                font-size: 17px;
                                line-height: 27px;
                            ">
                                Hola, <strong>{data[0]}</strong>:
                            </p>

                            <p style="
                                margin: 0;
                                color: #455a64;
                                font-size: 16px;
                                line-height: 26px;
                            ">
                                Por ser cliente de
                                <strong style="color: #263238;">
                                    Guarde Lo Que Quiera
                                </strong>,
                                queremos acercarte una propuesta especial de
                                <strong style="color: #263238;">ICBC</strong>
                                con bonificaciones y alternativas pensadas para vos.
                            </p>

                        </td>
                    </tr>

                    <!-- Beneficios -->
                    <tr>
                        <td style="padding: 14px 32px 26px;">

                            <table
                                role="presentation"
                                width="100%"
                                cellpadding="0"
                                cellspacing="0"
                                border="0"
                                style="
                                    width: 100%;
                                    background-color: #f7f9fb;
                                    border: 1px solid #e2e8ed;
                                    border-radius: 10px;
                                "
                            >
                                <tr>
                                    <td style="padding: 24px;">

                                        <p style="
                                            margin: 0 0 16px;
                                            color: #17324d;
                                            font-size: 17px;
                                            line-height: 24px;
                                            font-weight: 700;
                                        ">
                                            Podés consultar por:
                                        </p>

                                        <table
                                            role="presentation"
                                            width="100%"
                                            cellpadding="0"
                                            cellspacing="0"
                                            border="0"
                                        >
                                            <tr>
                                                <td
                                                    width="25"
                                                    valign="top"
                                                    style="
                                                        padding: 3px 0 11px;
                                                        color: #d71920;
                                                        font-size: 17px;
                                                        font-weight: 700;
                                                    "
                                                >
                                                    ✓
                                                </td>
                                                <td style="
                                                    padding: 0 0 11px;
                                                    color: #455a64;
                                                    font-size: 15px;
                                                    line-height: 23px;
                                                ">
                                                    Bonificaciones en cuentas.
                                                </td>
                                            </tr>

                                            <tr>
                                                <td
                                                    width="25"
                                                    valign="top"
                                                    style="
                                                        padding: 3px 0 11px;
                                                        color: #d71920;
                                                        font-size: 17px;
                                                        font-weight: 700;
                                                    "
                                                >
                                                    ✓
                                                </td>
                                                <td style="
                                                    padding: 0 0 11px;
                                                    color: #455a64;
                                                    font-size: 15px;
                                                    line-height: 23px;
                                                ">
                                                    Beneficios en paquetes bancarios.
                                                </td>
                                            </tr>

                                            <tr>
                                                <td
                                                    width="25"
                                                    valign="top"
                                                    style="
                                                        padding: 3px 0 0;
                                                        color: #d71920;
                                                        font-size: 17px;
                                                        font-weight: 700;
                                                    "
                                                >
                                                    ✓
                                                </td>
                                                <td style="
                                                    padding: 0;
                                                    color: #455a64;
                                                    font-size: 15px;
                                                    line-height: 23px;
                                                ">
                                                    Otras alternativas disponibles según tu
                                                    perfil y las condiciones vigentes.
                                                </td>
                                            </tr>
                                        </table>

                                    </td>
                                </tr>
                            </table>

                        </td>
                    </tr>

                    <!-- Contacto -->
                    <tr>
                        <td style="padding: 0 32px 34px;">

                            <p style="
                                margin: 0 0 18px;
                                color: #263238;
                                font-size: 18px;
                                line-height: 26px;
                                font-weight: 700;
                            ">
                                ¿Querés conocer más?
                            </p>

                            <p style="
                                margin: 0 0 20px;
                                color: #455a64;
                                font-size: 15px;
                                line-height: 24px;
                            ">
                                Para recibir asesoramiento y conocer las opciones,
                                los requisitos y las condiciones vigentes,
                                comunicate directamente con:
                            </p>

                            <table
                                role="presentation"
                                width="100%"
                                cellpadding="0"
                                cellspacing="0"
                                border="0"
                                style="
                                    width: 100%;
                                    background-color: #fff5f5;
                                    border-left: 4px solid #d71920;
                                    border-radius: 8px;
                                "
                            >
                                <tr>
                                    <td style="padding: 19px 20px;">

                                        <p style="
                                            margin: 0 0 4px;
                                            color: #263238;
                                            font-size: 17px;
                                            line-height: 24px;
                                            font-weight: 700;
                                        ">
                                            Natalia Pedro
                                        </p>

                                        <p style="
                                            margin: 0 0 7px;
                                            color: #607d8b;
                                            font-size: 14px;
                                            line-height: 21px;
                                        ">
                                            Contacto ICBC
                                        </p>

                                        <p style="
                                            margin: 0;
                                            color: #263238;
                                            font-size: 17px;
                                            line-height: 24px;
                                            font-weight: 700;
                                        ">
                                            <a
                                                href="tel:+541134789917"
                                                style="
                                                    color: #d71920;
                                                    text-decoration: none;
                                                "
                                            >
                                                11 3478-9917
                                            </a>
                                        </p>

                                    </td>
                                </tr>
                            </table>

                            <!-- Botón principal -->
                            <table
                                role="presentation"
                                cellpadding="0"
                                cellspacing="0"
                                border="0"
                                align="center"
                                style="margin: 26px auto 0;"
                            >
                                <tr>
                                    <td
                                        align="center"
                                        bgcolor="#d71920"
                                        style="
                                            border-radius: 7px;
                                            background-color: #d71920;
                                        "
                                    >
                                        <a
                                            href="tel:+541134789917"
                                            style="
                                                display: inline-block;
                                                padding: 14px 28px;
                                                color: #ffffff;
                                                font-size: 15px;
                                                line-height: 20px;
                                                font-weight: 700;
                                                text-decoration: none;
                                                border-radius: 7px;
                                            "
                                        >
                                            Consultar beneficios
                                        </a>
                                    </td>
                                </tr>
                            </table>

                        </td>
                    </tr>

                    <!-- Despedida -->
                    <tr>
                        <td
                            style="
                                padding: 27px 32px;
                                background-color: #f7f9fb;
                                border-top: 1px solid #e2e8ed;
                            "
                        >
                            <p style="
                                margin: 0 0 7px;
                                color: #455a64;
                                font-size: 15px;
                                line-height: 23px;
                            ">
                                Esperamos que esta propuesta te resulte útil.
                            </p>

                            <p style="
                                margin: 0;
                                color: #263238;
                                font-size: 15px;
                                line-height: 23px;
                            ">
                                Saludos,<br>
                                <strong>La Administración</strong><br>
                                Guarde Lo Que Quiera
                            </p>
                        </td>
                    </tr>

                    <!-- Datos de Guarde Lo Que Quiera -->
                    <tr>
                        <td
                            align="center"
                            style="
                                padding: 25px 26px;
                                background-color: #17324d;
                            "
                        >
                            <p style="
                                margin: 0 0 9px;
                                color: #ffffff;
                                font-size: 14px;
                                line-height: 22px;
                                font-weight: 700;
                            ">
                                Guarde Lo Que Quiera
                            </p>

                            <p style="
                                margin: 0 0 8px;
                                color: #d9e3ea;
                                font-size: 13px;
                                line-height: 21px;
                            ">
                                <a
                                    href="tel:+541147620599"
                                    style="color: #d9e3ea; text-decoration: none;"
                                >
                                    11 4762-0599
                                </a>
                                &nbsp;·&nbsp;
                                <a
                                    href="tel:+541147302192"
                                    style="color: #d9e3ea; text-decoration: none;"
                                >
                                    11 4730-2192
                                </a>
                            </p>

                            <p style="
                                margin: 0 0 8px;
                                color: #d9e3ea;
                                font-size: 13px;
                                line-height: 21px;
                            ">
                                WhatsApp:
                                <a
                                    href="https://wa.me/5491157800251"
                                    target="_blank"
                                    style="
                                        color: #83d9a5;
                                        font-weight: 700;
                                        text-decoration: none;
                                    "
                                >
                                    11 5780-0251
                                </a>
                            </p>

                            <p style="
                                margin: 0;
                                color: #d9e3ea;
                                font-size: 13px;
                                line-height: 21px;
                            ">
                                <a
                                    href="https://www.guardeloquequiera.com.ar/"
                                    target="_blank"
                                    style="
                                        color: #ffffff;
                                        font-weight: 700;
                                        text-decoration: underline;
                                    "
                                >
                                    www.guardeloquequiera.com.ar
                                </a>
                            </p>
                        </td>
                    </tr>

                </table>

                <!-- Aclaración comercial -->
                <table
                    role="presentation"
                    width="100%"
                    cellpadding="0"
                    cellspacing="0"
                    border="0"
                    style="width: 100%; max-width: 620px;"
                >
                    <tr>
                        <td align="center" style="padding: 18px 20px 0;">

                            <p style="
                                margin: 0;
                                color: #7b8a92;
                                font-size: 11px;
                                line-height: 17px;
                            ">
                                Esta comunicación tiene carácter informativo.
                                Las bonificaciones, los productos y sus condiciones
                                comerciales dependen de ICBC y pueden variar.
                                Consultá las condiciones vigentes con el contacto indicado.
                            </p>

                        </td>
                    </tr>
                </table>

            </td>
        </tr>
    </table>

</body>
</html>
`;

  ngOnInit(): void {
    // Iniciar conexión SignalR y escuchar actualizaciones
    this.ensureQuillStylesheet();
    this.commService.startSignalRConnection();
    this.signalRSubscription = this.commService.onCommunicationUpdated$.subscribe((id) => {
      // Recargar listado en segundo plano
      this.loadCommunications();
      
      // Si el usuario está viendo los detalles o reintentos de ese mismo comunicado, 
      // actualizamos los datos silenciosamente
      const currentComm = this.selectedCommunication();
      if (currentComm && currentComm.id === id) {
          this.commService.getCommunicationById(id).subscribe(updatedComm => {
              this.selectedCommunication.set(updatedComm);
          });
      }
    });

    this.loadCommunications();
    this.loadRecipientOptions();
    this.setupSearchDebounce();  
    this.loadSmtpConfigs();
    this.loadClientsForSelector();
    this.generateMonthFilters();
  }

  private ensureQuillStylesheet(): void {
    const stylesheetId = 'quill-snow-styles';
    if (document.getElementById(stylesheetId)) return;

    const link = document.createElement('link');
    link.id = stylesheetId;
    link.rel = 'stylesheet';
    link.href = 'assets/quill/quill.snow.css';
    document.head.appendChild(link);
  }

  ngOnDestroy(): void {
    if (this.signalRSubscription) {
      this.signalRSubscription.unsubscribe();
    }
    this.commService.stopSignalRConnection();
  }

  loadCommunications(): void {
    this.commService.getCommunications().subscribe({ 
      next: (data) => {
        const oldComms = this.communications();
        const newTrans = new Set(this.transitioningCommunications());
        let hasNewTransitions = false;

        data.forEach(newComm => {
          const oldComm = oldComms.find(c => c.id === newComm.id);
          const isFinishedStatus = newComm.status === 'Finished' || newComm.status === 'Failed' || newComm.status === 'Finished w/ Errors';
          
          if (oldComm && oldComm.status === 'Procesando' && isFinishedStatus) {
            newTrans.add(newComm.id);
            hasNewTransitions = true;
            
            // Remove from transition state after 3.5 seconds
            setTimeout(() => {
              this.transitioningCommunications.update(set => {
                const updated = new Set(set);
                updated.delete(newComm.id);
                return updated;
              });
            }, 3500);
          }
        });
        
        if (hasNewTransitions) {
          this.transitioningCommunications.set(newTrans);
        }
        this.communications.set(data);
      },
      error: (err) => this.showToast('Error de Carga', 'No se pudieron cargar los datos', '❌', 'error')
    });
  }

  loadRecipientOptions(): void {
    this.clientService.getRecipientOptions().subscribe({
      next: (data) => {
        const groups = data.filter(d => 
            d.startsWith("Todos los clientes") || 
            d.startsWith("Clientes morosos") || 
            d.startsWith("Clientes al día")
        );
        this.staticGroups.set(groups);
      },
      error: (err) => {
        this.staticGroups.set([
          'Todos los clientes', 'Clientes morosos', 'Clientes al día'
        ]);
      }
    });
  }

  setupSearchDebounce(): void {
    this.searchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(query => {
        if (query.length < 2) {
          return of([]);
        }
        return this.clientService.searchClients(query).pipe(
          catchError(() => of([]))
        );
      })
    ).subscribe(results => {
      this.searchResults.set(results);
    });
  }

  onSearchInput(event: Event): void {
    const query = (event.target as HTMLInputElement).value;
    this.searchSubject.next(query);
  }
  
  formData = signal<FormDataState>({
    id: null,
    title: '',
    content: '', 
    sendDate: '',
    sendTime: '',
    channels: [],
    recipients: [],
    type: 'enviar_ahora',
    smtpConfigId: null,
    isAccountStatement: false,
    isNextMonthStatement: false,
    sendToAllEmails: false
  });
  
  currentModal = signal<'add' | 'edit' | 'view' | 'send-confirm' | 'retry' | 'history' | 'none'>('none');
  selectedCommunication = signal<ComunicacionDto | null>(null);
  transitioningCommunications = signal<Set<number>>(new Set());

  // History Modal Signals
  historySearchTerm = signal<string>('');
  historyStatusFilter = signal<string>('ALL');
  historyChannelFilter = signal<string>('ALL');
  historyCurrentPage = signal<number>(1);
  historyItemsPerPage = 10;

  toast = signal<ToastState>({
    show: false,
    message: '',
    description: '',
    icon: '',
    color: 'success',
  });

  channels = COMMUNICATION_CHANNELS;
  
  // --- Computed Signals ---
  
  scheduledCommunications = computed(() => {
    const trans = this.transitioningCommunications();
    return this.communications().filter(c => 
      c.status === 'Scheduled' || 
      c.status === 'Procesando' || 
      trans.has(c.id)
    );
  });

  draftCommunications = computed(() => 
    this.communications().filter(c => c.status === 'Draft')
  );

  pastCommunications = computed(() => {
    const trans = this.transitioningCommunications();
    return this.communications().filter(c => 
      (c.status === 'Finished' || c.status === 'Finished w/ Errors' || c.status === 'Failed') &&
      !trans.has(c.id)
    );
  });

  filteredHistoryCommunications = computed(() => {
    let comms = this.pastCommunications();
    const search = this.historySearchTerm().toLowerCase().trim();
    const status = this.historyStatusFilter();
    const channel = this.historyChannelFilter();

    if (search) {
      comms = comms.filter(c => c.title.toLowerCase().includes(search));
    }
    if (status !== 'ALL') {
      if (status === 'Failed') {
        comms = comms.filter(c => c.status === 'Failed' || c.status === 'Finished w/ Errors');
      } else {
        comms = comms.filter(c => c.status === status);
      }
    }
    if (channel !== 'ALL') {
      if (channel === 'Email') {
         comms = comms.filter(c => c.channel.includes('Email'));
      } else if (channel === 'WhatsApp') {
         comms = comms.filter(c => c.channel.includes('WhatsApp'));
      }
    }
    return comms;
  });

  paginatedHistoryCommunications = computed(() => {
    const all = this.filteredHistoryCommunications();
    const page = this.historyCurrentPage();
    const startIndex = (page - 1) * this.historyItemsPerPage;
    return all.slice(startIndex, startIndex + this.historyItemsPerPage);
  });

  historyTotalPages = computed(() => {
    return Math.max(1, Math.ceil(this.filteredHistoryCommunications().length / this.historyItemsPerPage));
  });

  isFormValid = computed(() => {
    const data = this.formData();
    
    const isContentEmpty = !data.content || data.content.trim() === '<p><br></p>' || data.content.trim() === '';
    const contentIsValid = data.isAccountStatement || !isContentEmpty;
    const recipientsAreValid = data.sendToAllEmails
      ? data.channels.includes('Email')
      : data.recipients.length > 0;

    let baseValid = data.title.trim().length > 0 && 
                    contentIsValid && 
                    data.channels.length > 0 && 
                    recipientsAreValid;
    
    if (data.type === 'programar') {
      return baseValid && data.sendDate.length > 0 && data.sendTime.length > 0;
    }
    
    return baseValid;
  });


  private resetForm(): void {
    const defaultSmtp = this.smtpConfigs().length > 0 ? this.smtpConfigs()[0].id : null;

    this.formData.set({
      id: null,
      title: '',
      content: '',
      sendDate: '',
      sendTime: '',
      channels: [],
      recipients: [],
      type: 'enviar_ahora',
      smtpConfigId: defaultSmtp,
      isAccountStatement: false,
      isNextMonthStatement: false,
      sendToAllEmails: false
    });
  }

  private showToast(message: string, description: string, icon: string, color: 'success' | 'error'): void {
    this.toast.set({ show: true, message, description, icon, color });
    setTimeout(() => this.toast.set({ ...this.toast(), show: false }), 4000);
  }

  openModal(
    modalType: 'add' | 'edit' | 'view' | 'send-confirm' | 'retry' | 'history',
    communication: ComunicacionDto | null = null, 
    isResend: boolean = false // Este flag ahora servirá para "Clonar"
  ): void {
    this.selectedCommunication.set(communication);
    this.resetForm();

    let finalModalType = modalType;

    if (communication && (modalType === 'edit' || isResend)) {
      // 1. Get channels as array
      let channelsArray: ('Email' | 'WhatsApp')[] = [];
      if (communication.channel.includes('Email')) channelsArray.push('Email');
      if (communication.channel.includes('WhatsApp')) channelsArray.push('WhatsApp');

      // 2. Determine form type based on communication status and whether it's a resend
      let formType: 'programar' | 'borrador' | 'enviar_ahora' = 'borrador';
      
      // If it's a resend, default to 'enviar_ahora' regardless of original status
      if (communication.status === 'Scheduled' || communication.status === 'Procesando') {
        formType = 'programar';
      } 
      // If the communication failed or had errors, we want to allow quick resend
      else if (communication.status === 'Failed' || communication.status === 'Finished w/ Errors') {
        formType = 'enviar_ahora';
      } 
      // If it's a draft, keep it as draft
      else {
        formType = 'borrador';
      }

      this.formData.set({
        id: isResend ? null : communication.id, 
        title: communication.title,
        content: communication.content,
        sendDate: isResend ? '' : (communication.sendDate || ''),
        sendTime: isResend ? '' : (communication.sendTime || ''),
        channels: channelsArray,
        recipients: communication.sendToAllEmails ? [] : [...communication.recipients],
        
        type: isResend ? 'enviar_ahora' : formType,
        
        smtpConfigId: communication.smtpConfigId || null,
        isAccountStatement: communication.isAccountStatement || false,
        isNextMonthStatement: communication.isNextMonthStatement || false,
        sendToAllEmails: communication.sendToAllEmails || false
      });
      
      if (isResend) finalModalType = 'add';
    }
    
    if (communication && (modalType === 'view' || modalType === 'retry')) {
      this.commService.getCommunicationById(communication.id).subscribe({
        next: (fullComm) => {
          if (modalType === 'retry' && fullComm.dispatches) {
            fullComm.dispatches.forEach(d => d.isSelected = (d.status !== 'Exitoso'));
          }
          this.selectedCommunication.set(fullComm);
        },
        error: (err) => console.error('Error cargando detalle del comunicado', err)
      });
    }

    this.currentModal.set(finalModalType);
  }

  closeModal(): void {
    this.currentModal.set('none');
    this.selectedCommunication.set(null);
    this.historySearchTerm.set('');
    this.historyStatusFilter.set('ALL');
    this.historyChannelFilter.set('ALL');
    this.historyCurrentPage.set(1);
    this.resetForm();
  }

  async confirmDeleteCommunication(communication: ComunicacionDto): Promise<void> {
    this.closeModal();

    const confirmed = await this.deleteConfirmation.confirm({
      message: 'Esta acción eliminará el comunicado',
      highlightedText: communication.title,
      messageSuffix: 'de forma permanente.'
    });
    if (confirmed) {
      this.handleDeleteCommunication(communication.id);
    }
  }

  changeHistoryPage(delta: number): void {
    const newPage = this.historyCurrentPage() + delta;
    if (newPage >= 1 && newPage <= this.historyTotalPages()) {
      this.historyCurrentPage.set(newPage);
    }
  }

  addCommunication(): void {
    const data = this.formData();
    if (!this.isFormValid()) { return; }

    let finalSendDate = '';
    let finalSendTime = '';
    let finalType = 'draft';

    if (data.type === 'programar') {
      finalType = 'schedule';
      finalSendDate = data.sendDate;
      finalSendTime = data.sendTime;
    } 
    else if (data.type === 'enviar_ahora') {
      finalType = 'schedule';
      
      const now = new Date();
      const year = now.getFullYear();
      const month = String(now.getMonth() + 1).padStart(2, '0');
      const day = String(now.getDate()).padStart(2, '0');
      finalSendDate = `${year}-${month}-${day}`; 

      const hours = String(now.getHours()).padStart(2, '0');
      const minutes = String(now.getMinutes()).padStart(2, '0');
      finalSendTime = `${hours}:${minutes}`;
    }

    const request = {
      ...data,
      content: data.isAccountStatement ? 'Estado de cuenta (Autm.)' : data.content,
      type: finalType,
      sendDate: finalSendDate,
      sendTime: finalSendTime
    };

    this.commService.createCommunication(request, this.selectedFiles()).subscribe({
      next: (newCommunication) => {
        this.communications.update(comms => [newCommunication, ...comms]);
        this.closeModal();
        this.showToast('¡Comunicado creado!', 'Se guardó correctamente', '📨', 'success');
        this.selectedFiles.set([]);
      },
      error: (err) => this.showToast('Error', 'No se pudo crear el comunicado', '❌', 'error')
    });
  }

  sendTestCommunication(): void {
    const data = this.formData();
    const canTestWhatsAppStatement = data.isAccountStatement && data.channels.includes('WhatsApp');
    const hasEmailTestChannel = data.channels.includes('Email');
    if (!this.isFormValid() || (!hasEmailTestChannel && !canTestWhatsAppStatement)) { return; }

    const now = new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const day = String(now.getDate()).padStart(2, '0');
    const finalSendDate = `${year}-${month}-${day}`; 

    const hours = String(now.getHours()).padStart(2, '0');
    const minutes = String(now.getMinutes()).padStart(2, '0');
    const finalSendTime = `${hours}:${minutes}`;

    const request = {
      ...data,
      title: `[PRUEBA] ${data.title}`,
      content: data.isAccountStatement ? 'Estado de cuenta (Autm.)' : data.content,
      channels: data.isAccountStatement
        ? data.channels
        : data.channels.filter(channel => channel === 'Email'),
      type: 'schedule',
      sendDate: finalSendDate,
      sendTime: finalSendTime,
      isTestMode: true,
      testEmailAddress: 'fsgbrunofranco@gmail.com'
    };

    this.commService.createCommunication(request, this.selectedFiles()).subscribe({
      next: (newCommunication) => {
        this.communications.update(comms => [newCommunication, ...comms]);
        this.closeModal();
        const testDestination = canTestWhatsAppStatement
          ? hasEmailTestChannel
            ? 'WhatsApp al 1160244908 y Email a fsgbrunofranco@gmail.com'
            : 'WhatsApp al 1160244908'
          : 'Email a fsgbrunofranco@gmail.com';
        this.showToast('¡Prueba enviada!', `El envío se está procesando y llegará a ${testDestination}.`, 'check-circle', 'success');
      },
      error: (err) => {
        console.error(err);
        this.showToast('Error', 'No se pudo enviar la prueba', 'alert-circle', 'error');
      }
    });
  }

  editCommunication(): void {
    const data = this.formData();
    const commId = data.id;

    if (!commId || !this.isFormValid()) { return; }

    let finalSendDate = '';
    let finalSendTime = '';
    let finalType: 'draft' | 'schedule' = 'draft';

    if (data.type === 'programar') {
      finalType = 'schedule';
      finalSendDate = data.sendDate;
      finalSendTime = data.sendTime;
    } 
    else if (data.type === 'enviar_ahora') {
      finalType = 'schedule';
      
      const now = new Date();
      const year = now.getFullYear();
      const month = String(now.getMonth() + 1).padStart(2, '0');
      const day = String(now.getDate()).padStart(2, '0');
      finalSendDate = `${year}-${month}-${day}`; 

      const hours = String(now.getHours()).padStart(2, '0');
      const minutes = String(now.getMinutes()).padStart(2, '0');
      finalSendTime = `${hours}:${minutes}`;
    }

    const request: UpsertComunicacionRequest = {
      id: commId,
      title: data.title,
      content: data.isAccountStatement ? 'Estado de cuenta (Autm.)' : data.content,
      type: finalType,
      sendDate: finalType === 'schedule' ? finalSendDate : null,
      sendTime: finalType === 'schedule' ? finalSendTime : null,
      channels: data.channels,
      recipients: data.recipients,
      smtpConfigId: data.smtpConfigId,
      isAccountStatement: data.isAccountStatement,
      isNextMonthStatement: data.isNextMonthStatement,
      sendToAllEmails: data.sendToAllEmails
    };

    this.commService.updateCommunication(commId, request).subscribe({
      next: (updatedComm) => {
        this.communications.update(comms => comms.map(c => c.id === commId ? updatedComm : c));
        
        this.closeModal();

        const msg = data.type === 'enviar_ahora' 
          ? '¡Enviando comunicado!' 
          : '¡Comunicado actualizado!';
          
        this.showToast(msg, 'Los cambios se guardaron correctamente', '✏️', 'success');
      },
      error: (err) => {
        console.error(err);
        this.showToast('Error', 'No se pudo actualizar el comunicado', '❌', 'error');
      }
    });
  }

  handleDeleteCommunication(communicationId: number): void {
    this.commService.deleteCommunication(communicationId).subscribe({
      next: () => {
        this.communications.update(comms => comms.filter(c => c.id !== communicationId));
        this.closeModal();
        this.showToast('Comunicado eliminado', 'Se eliminó correctamente', '🗑️', 'success');
      },
      error: (err) => this.showToast('Error', 'No se pudo eliminar', '❌', 'error')
    });
  }

  handleSendCommunication(communicationId: number): void {
    this.commService.sendDraftNow(communicationId).subscribe({
      next: (sentComm) => {
        this.communications.update(comms => comms.map(c => c.id === communicationId ? sentComm : c));
        this.closeModal();
        this.showToast('¡Comunicado enviado!', 'El envío se ha puesto en cola', '✅', 'success');
      },
      error: (err) => this.showToast('Error', 'No se pudo enviar', '❌', 'error')
    });
  }

  toggleChannel(channelName: 'Email' | 'WhatsApp'): void {
    if (this.formData().sendToAllEmails && channelName === 'WhatsApp') {
      this.showToast(
        'Selección exclusiva por Email',
        'La opción "Todos los emails" no envía mensajes por WhatsApp.',
        'mail',
        'error'
      );
      return;
    }

    const currentChannels = this.formData().channels;
    const isAdding = !currentChannels.includes(channelName);
    
    const newChannels = isAdding
      ? [...currentChannels, channelName]
      : currentChannels.filter(c => c !== channelName);

    this.formData.update(data => ({ 
      ...data, 
      channels: newChannels,
      sendToAllEmails: data.sendToAllEmails && channelName === 'Email' && !isAdding
        ? false
        : data.sendToAllEmails
    }));
  }

  addRecipientFromList(recipient: string, inputElement: HTMLInputElement): void {
    if (recipient && !this.formData().recipients.includes(recipient)) {
      this.formData.update(data => ({ ...data, recipients: [...data.recipients, recipient] }));
    }
    inputElement.value = ''; 
    this.searchResults.set([]);
    this.isSearchFocused.set(false);
  }

  removeRecipient(recipient: string): void {
    this.formData.update(data => ({ ...data, recipients: data.recipients.filter(d => d !== recipient) }));
  }


  getBadgeMeta(status: ComunicacionDto['status']): { text: string; classes: string; icon?: string } {
    let colorClass = '';
    let icon: string | undefined;
    let text: string;

    switch (status) {
      case 'Finished': 
        colorClass = 'badge-finished'; icon = 'check-circle'; text = 'Enviado';
        break;
      case 'Scheduled': 
        colorClass = 'badge-scheduled'; icon = 'clock'; text = 'Programado';
        break;
      case 'Draft': 
        colorClass = 'badge-draft'; icon = 'file-text'; text = 'Borrador';
        break;
      case 'Procesando':
        colorClass = 'processing-badge'; icon = 'refresh-cw'; text = 'Procesando';
        break;
      case 'Failed':
      case 'Finished w/ Errors':
        colorClass = 'badge-error'; icon = 'alert-triangle'; text = 'Error';
        break;
      default: 
        colorClass = 'badge-draft'; text = status;
    }
    return { text, classes: `status-badge ${colorClass}`, icon };
  }

  getChannelMeta(channel: string): { icons: { name: string; classes?: string }[] } {
    const mail = { name: 'Mail', classes: 'text-blue-600' };
    const wa = { name: 'whatsapp', classes: 'text-green-600' };
    if (channel === 'Email + WhatsApp') return { icons: [mail, wa] };
    if (channel === 'Email') return { icons: [mail] };
    if (channel === 'WhatsApp') return { icons: [wa] };
    return { icons: [] };
  }

  getCommunicationPreview(content: string, channel: string): string {
  if (!content) return '';

  const tempDiv = document.createElement('div');
  tempDiv.innerHTML = content;
  const text = tempDiv.textContent || tempDiv.innerText || '';

  return text.length > 150 ? text.substring(0, 150) + '...' : text;
}

  updateFormField<K extends keyof FormDataState>(field: K, value: FormDataState[K]) {
    this.formData.update(currentData => {
      const updated = {
        ...currentData,
        [field]: value
      };
      if (field === 'isAccountStatement' && value === true) {
        updated.title = 'ESTADO DE CUENTA';
        // El estado se puede entregar por ambos canales. Conservamos WhatsApp
        // si ya estaba elegido y agregamos Email como canal predeterminado.
        updated.channels = Array.from(new Set([...updated.channels, 'Email']));
      }
      return updated;
    });
  }

  loadIcbcTemplate(): void {
    const currentChannels = this.formData().channels;
    const channels: FormDataState['channels'] = currentChannels.includes('Email')
      ? currentChannels
      : [...currentChannels, 'Email'];

    this.updateFormField('title', 'Beneficio especial ICBC para clientes');
    this.updateFormField('channels', channels);
    this.updateFormField('content', this.icbcTemplate);
    this.showToast(
      'Plantilla cargada',
      'Revisá el contenido y seleccioná "Todos los emails" antes de enviarlo.',
      'mail',
      'success'
    );
  }

  loadIcbcTemplate2(): void {
    const currentChannels = this.formData().channels;
    const channels: FormDataState['channels'] = currentChannels.includes('Email')
      ? currentChannels
      : [...currentChannels, 'Email'];

    this.updateFormField('title', 'Beneficios ICBC para nuestros clientes');
    this.updateFormField('channels', channels);
    this.updateFormField('content', this.icbcTemplate2);
    this.showToast(
      'Plantilla 2 cargada',
      'Revisá el contenido y seleccioná "Todos los emails" antes de enviarlo.',
      'mail',
      'success'
    );
  }

  getSanitizedHtmlContent(): SafeHtml {
    const html = this.selectedCommunication()?.content || '';
    return this.sanitizer.bypassSecurityTrustHtml(html);
  }

  loadSmtpConfigs(): void {
    this.commService.getAllSmtpConfigs().subscribe({
        next: (configs) => {
            this.smtpConfigs.set(configs);
            if (configs.length > 0 && !this.formData().smtpConfigId) {
                this.updateFormField('smtpConfigId', configs[0].id);
            }
        },
        error: () => console.error('No se pudieron cargar las configuraciones SMTP')
    });
  }

  onFileSelected(event: any): void {
    const files = event.target.files;
    if (files) {
      const fileArray = Array.from(files) as File[];
      this.selectedFiles.update(current => [...current, ...fileArray]);
    }
  }

  removeFile(index: number): void {
    this.selectedFiles.update(files => files.filter((_, i) => i !== index));
  }

  openRetryModal(comm: ComunicacionDto): void {
    this.openModal('retry', comm);
  }

  viewDispatchContent(dispatchId: number, clientName: string): void {
    this.commService.getDispatchContent(dispatchId).subscribe({
      next: ({ content }) => {
        this.previewClientName.set(clientName);
        this.previewContent.set(content);
      },
      error: () => {
        this.showToast(
          'No se pudo abrir el contenido',
          'Intentá nuevamente en unos segundos.',
          'alert-triangle',
          'error',
        );
      },
    });
  }

  closeContentPreview(): void {
    this.previewContent.set(null);
    this.previewClientName.set('');
  }

  getFailedDispatches(comm: ComunicacionDto): CommunicationDispatchDto[] {
    if (!comm.dispatches || comm.dispatches.length === 0) {
      return comm.recipients.map((name, idx) => ({
        dispatchId: -(idx + 1),
        clientId: -(idx + 1),
        clientName: name,
        channel: comm.channel,
        status: 'Fallido',
        errorMessage: comm.errorMessage || 'Envío fallido o interrumpido',
        dispatchDate: comm.creationDate,
        isSelected: true
      }));
    }
    return comm.dispatches.filter(d => d.status !== 'Exitoso');
  }

  toggleAllRetrySelection(select: boolean): void {
    const comm = this.selectedCommunication();
    if (!comm || !comm.dispatches) return;
    comm.dispatches.forEach(d => {
      if (d.status !== 'Exitoso') d.isSelected = select;
    });
    this.selectedCommunication.set({ ...comm });
  }

  getSelectedRetryCount(): number {
    const comm = this.selectedCommunication();
    if (!comm) return 0;
    const failed = this.getFailedDispatches(comm);
    return failed.filter(d => d.isSelected !== false).length;
  }

  getSuccessCount(dispatches: CommunicationDispatchDto[]): number {
    return dispatches.filter(d => d.status === 'Exitoso').length;
  }

  getFailedCount(dispatches: CommunicationDispatchDto[]): number {
    return dispatches.filter(d => d.status !== 'Exitoso').length;
  }

  confirmRetrySelected(): void {
    const comm = this.selectedCommunication();
    if (!comm) return;
    const failed = this.getFailedDispatches(comm);
    const selected = failed.filter(d => d.isSelected !== false);
    
    if (selected.length === 0) {
      this.showToast('Advertencia', 'Debes seleccionar al menos un cliente para reintentar.', 'alert-triangle', 'error');
      return;
    }

    const selectedClientIds = selected.map(d => d.clientId).filter(id => id > 0);
    
    this.commService.retrySelectedCommunication(comm.id, selectedClientIds).subscribe({
      next: (res) => {
        this.showToast('Reintento Programado', `Se enviará a ${selected.length} clientes seleccionados.`, 'check', 'success');
        this.closeModal();
        this.loadCommunications();
      },
      error: (err) => {
        console.error('Error en reintento', err);
        this.showToast('Error', 'Hubo un error al iniciar el reintento.', 'alert-triangle', 'error');
      }
    });
  }

  // --- new methods for client selector ---
  loadClientsForSelector(): void {
    this.commService.getClientsForSelector().subscribe({
        next: (data: any[]) => {
            const mapped = data.map(c => {
                let status: 'Moroso' | 'Pendiente' | 'AlDia' = 'AlDia';
                
                if (c.maxUnpaidMonths > 0) {
                    status = 'Moroso';
                } else if (c.balance < 0 && c.maxUnpaidMonths === 0) { 
                    status = 'Pendiente';
                } else {
                    status = 'AlDia'; 
                }

                return {
                    id: c.id,
                    fullName: c.fullName,
                    email: c.email,
                    balance: c.balance,
                    unpaidMonths: c.maxUnpaidMonths,
                    currentRentAmount: c.currentRentAmount || 0,
                    status: status,
                    selected: false,
                    nextPaymentDate: c.nextPaymentDate ? new Date(c.nextPaymentDate) : null,
                    paymentIdentifier: c.paymentIdentifier
                };
            });
            this.allClients.set(mapped);
            this.filterList();
        },
        error: (err) => console.error('Error cargando clientes', err)
    });
  }

  openRecipientSelector(): void {
    const currentRecipients = this.formData().recipients;
    const sendToAllEmails = this.formData().sendToAllEmails;
    this.modalSendToAllEmails.set(sendToAllEmails);
    this.activeQuickFilter.set(sendToAllEmails ? 'TodosLosEmails' : null);

    if (!sendToAllEmails && currentRecipients.length > 0) {
        this.allClients.update(list => list.map(c => ({
            ...c,
            selected: currentRecipients.includes(c.fullName)
        })));
    }
    
    this.filterList();
    this.showRecipientModal.set(true);
  }

  applyFilter(type: 'Todos' | 'Ninguno' | 'MesImpago', targetYear?: number, targetMonth?: number, filterLabel?: string): void {
      if (this.modalSendToAllEmails()) {
          this.modalSendToAllEmails.set(false);
      }

      if (type === 'MesImpago' && filterLabel) {
          if (this.activeQuickFilter() === filterLabel) {
              this.activeQuickFilter.set(null);
              this.allClients.update(list => list.map(c => ({ ...c, selected: false })));
              this.filterList();
              return;
          }
          this.activeQuickFilter.set(filterLabel);
      } else if (type === 'Todos') {
          if (this.activeQuickFilter() === 'Todos') {
              this.activeQuickFilter.set(null);
              this.allClients.update(list => list.map(c => ({ ...c, selected: false })));
              this.filterList();
              return;
          }
          this.activeQuickFilter.set('Todos');
      } else if (type === 'Ninguno') {
          this.activeQuickFilter.set(null);
      }

      this.allClients.update(list => list.map(c => {
          let shouldSelect = c.selected; 
          
          switch(type) {
              case 'Todos': 
                  shouldSelect = true; 
                  break;
              case 'Ninguno': 
                  shouldSelect = false; 
                  break;
              case 'MesImpago':
                  if (c.nextPaymentDate && targetYear !== undefined && targetMonth !== undefined) {
                      const paymentYear = c.nextPaymentDate.getFullYear();
                      const paymentMonth = c.nextPaymentDate.getMonth();
                      
                      // Selecciona al cliente si su próxima fecha de pago es MENOR o IGUAL al mes del botón
                      if (paymentYear < targetYear || (paymentYear === targetYear && paymentMonth <= targetMonth)) {
                          shouldSelect = true;
                      }
                  }
                  break;
          }
          
          return { ...c, selected: shouldSelect };
      }));

      this.filterList(); 
  }

  selectAllEmailRecipients(): void {
      this.activeQuickFilter.set('TodosLosEmails');
      this.modalSendToAllEmails.set(true);
      this.recipientSearchTerm.set('');
      this.filteredClients.set([]);
  }

  toggleSort(): void {
      this.currentSort.update(current => {
          if (current === 'name') return 'status';
          if (current === 'status') return 'payment_identifier';
          return 'name';
      });
      this.filterList(); 
  }

  

  onSearch(term: string): void {
      this.recipientSearchTerm.set(term);
      this.filterList();
  }

  filterList(): void {
      const term = this.recipientSearchTerm().toLowerCase().trim();
      let list = this.allClients();
      
      if (term) {
          const termClean = term.replace(/^n[°º.]?\s*/i, '').trim() || term;
          list = list.filter(c => {
              const nameMatch = c.fullName.toLowerCase().includes(term);
              const emailMatch = !!(c.email && c.email.toLowerCase().includes(term));
              let idMatch = false;
              if (c.paymentIdentifier !== undefined && c.paymentIdentifier !== null) {
                  const numVal = Number(c.paymentIdentifier);
                  if (!isNaN(numVal)) {
                      const numStr = c.paymentIdentifier.toString();
                      const fixedStr = numVal.toFixed(2);
                      idMatch = numStr.includes(termClean) ||
                                fixedStr.includes(termClean) ||
                                numStr.replace('.', ',').includes(termClean) ||
                                fixedStr.replace('.', ',').includes(termClean);
                  }
              }
              return nameMatch || emailMatch || idMatch;
          });
      }

      const sortType = this.currentSort();
      
      list.sort((a, b) => {
          if (sortType === 'status') {
              const priority: Record<string, number> = { 'Moroso': 1, 'Pendiente': 2, 'AlDia': 3 };
              
              const pA = priority[a.status] || 99;
              const pB = priority[b.status] || 99;
              
              if (pA !== pB) return pA - pB;
          } else if (sortType === 'payment_identifier') {
              const valA = a.paymentIdentifier ?? 0;
              const valB = b.paymentIdentifier ?? 0;
              if (valA !== valB) return valA - valB;
          }

          return a.fullName.localeCompare(b.fullName);
      });

      this.filteredClients.set([...list]);
  }

  toggleSelection(id: number): void {
      const currentActive = this.activeQuickFilter();
      let toggledClient: ClientSelectorItem | null = null;

      this.allClients.update(list => list.map(c => {
          if (c.id === id) {
              const updated = { ...c, selected: !c.selected };
              toggledClient = updated;
              return updated;
          }
          return c;
      }));

      if (toggledClient && (toggledClient as ClientSelectorItem).selected === false && currentActive) {
          if (currentActive === 'Todos' || currentActive === 'TodosLosEmails') {
              this.activeQuickFilter.set(null);
          } else {
              const activeFilterObj = this.dynamicMonthFilters().find(f => f.label === currentActive);
              if (activeFilterObj && (toggledClient as ClientSelectorItem).nextPaymentDate) {
                  const targetYear = activeFilterObj.year;
                  const targetMonth = activeFilterObj.month;
                  const paymentYear = (toggledClient as ClientSelectorItem).nextPaymentDate!.getFullYear();
                  const paymentMonth = (toggledClient as ClientSelectorItem).nextPaymentDate!.getMonth();
                  
                  if (paymentYear < targetYear || (paymentYear === targetYear && paymentMonth <= targetMonth)) {
                      this.activeQuickFilter.set(null);
                  }
              }
          }
      }

      this.filterList();
  }

  confirmSelection(): void {
      if (this.modalSendToAllEmails()) {
          this.formData.update(data => ({
              ...data,
              channels: ['Email'],
              recipients: [],
              sendToAllEmails: true
          }));
          this.showRecipientModal.set(false);
          return;
      }

      const selectedNames = this.allClients()
          .filter(c => c.selected)
          .map(c => c.fullName);
      
      this.formData.update(data => ({
          ...data,
          recipients: selectedNames,
          sendToAllEmails: false
      }));
      this.showRecipientModal.set(false);
  }

  // --- New method to select month unpaids filters ---
  generateMonthFilters(): void {
    const months = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];
    const filters: MonthFilter[] = [];
    const now = new Date();
    
    let currentYear = now.getFullYear();
    let currentMonth = now.getMonth();

    // Generamos 3 meses (Actual, +1, +2)
    for (let i = 0; i < 3; i++) {
      filters.push({
        label: `${months[currentMonth]} Impago`,
        year: currentYear,
        month: currentMonth
      });

      currentMonth++;
      // Si nos pasamos de diciembre (11), volvemos a enero (0) y sumamos un año
      if (currentMonth > 11) {
        currentMonth = 0;
        currentYear++;
      }
    }
    this.dynamicMonthFilters.set(filters);
  }
}
