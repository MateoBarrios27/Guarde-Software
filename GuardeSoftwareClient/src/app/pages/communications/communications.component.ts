import { Component, signal, computed, ChangeDetectionStrategy, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { IconComponent } from "../../shared/components/icon/icon.component";
import { CommunicationService } from '../../core/services/communication-service/communication.service';
import {
  ComunicacionDto,
  CommunicationDispatchDto,
  CommunicationExtensionMode,
  CommunicationExtensionPreview,
  UpsertComunicacionRequest
} from '../../core/dtos/communications/communicationDto';
import { ClientService } from '../../core/services/client-service/client.service';
import { MassCommunicationRecipientService } from '../../core/services/mass-communication-recipient-service/mass-communication-recipient.service';
import { MassCommunicationRecipient } from '../../core/models/mass-communication-recipient';
import { DeleteConfirmationService } from '../../shared/services/delete-confirmation.service';
import { catchError, debounceTime, distinctUntilChanged, of, Subject, switchMap, Subscription } from 'rxjs';
import { QuillModule } from 'ngx-quill';
import {
  buildCommunicationPreviewDocument,
  buildCommunicationPreviewText
} from '../../shared/utils/communication-preview.util';
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
  externalRecipientIds: number[];
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

interface ExternalRecipientSelectorItem extends MassCommunicationRecipient {
  displayName: string;
  typeKey: string;
  selected: boolean;
}

interface RecipientTypeOption {
  value: string;
  label: string;
  count: number;
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

const INMOBILIARIAS_TEMPLATE_MARKER = 'GUARDE_TEMPLATE:INMOBILIARIAS_V1';
const INMOBILIARIAS_TEMPLATE_URL = 'assets/email-templates/inmobiliarias/inmobiliarias.html';
const LABORATORIOS_TEMPLATE_MARKER = 'GUARDE_TEMPLATE:LABORATORIOS_V1';
const LABORATORIOS_TEMPLATE_URL = 'assets/email-templates/laboratorios/laboratorios.html';
const VISITADORES_MEDICOS_TEMPLATE_MARKER = 'GUARDE_TEMPLATE:VISITADORES_MEDICOS_V1';
const VISITADORES_MEDICOS_TEMPLATE_URL = 'assets/email-templates/visitadores-medicos/visitadores-medicos.html';
const UNTYPED_RECIPIENT_TYPE = '__sin_rubro__';
const DEFAULT_EXTENSION_RECIPIENT_TYPE = 'inmobiliaria';

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
  isLoadingInmobiliariasTemplate = signal(false);
  isLoadingLaboratoriosTemplate = signal(false);
  isLoadingVisitadoresMedicosTemplate = signal(false);
  smtpConfigs = signal<any[]>([]);

  showRecipientModal = signal(false);
  previewContent = signal<SafeHtml | null>(null);
  previewClientName = signal('');
  allClients = signal<ClientSelectorItem[]>([]); 
  filteredClients = signal<ClientSelectorItem[]>([]); 
  recipientSearchTerm = signal('');
  allExternalRecipients = signal<ExternalRecipientSelectorItem[]>([]);
  filteredExternalRecipients = signal<ExternalRecipientSelectorItem[]>([]);
  externalRecipientSearchTerm = signal('');
  selectedExternalRecipientType = signal('');
  recipientSelectorMode = signal<'clients' | 'external'>('clients');
  isLoadingExternalRecipients = signal(false);
  externalRecipientsLoadError = signal(false);
  
  selectedCount = computed(() => this.formData().sendToAllEmails
    ? 0
    : this.formData().recipients.length + this.formData().externalRecipientIds.length);
  activeDesignedTemplateLabel = computed(() => {
    const content = this.formData().content;
    if (content.includes(INMOBILIARIAS_TEMPLATE_MARKER)) return 'Inmobiliarias';
    if (content.includes(LABORATORIOS_TEMPLATE_MARKER)) return 'Laboratorios';
    if (content.includes(VISITADORES_MEDICOS_TEMPLATE_MARKER)) return 'Visitadores médicos';
    return '';
  });
  isDesignedMarketingTemplate = computed(() => this.activeDesignedTemplateLabel().length > 0);
  selectedExternalCount = computed(() => this.allExternalRecipients().filter(r => r.selected).length);
  modalSelectedCount = computed(() =>
    this.allClients().filter(c => c.selected).length + this.selectedExternalCount());
  recipientTypeOptions = computed<RecipientTypeOption[]>(() => {
    const grouped = new Map<string, RecipientTypeOption>();

    for (const recipient of this.allExternalRecipients()) {
      const existing = grouped.get(recipient.typeKey);
      if (existing) {
        existing.count += 1;
        continue;
      }

      grouped.set(recipient.typeKey, {
        value: recipient.typeKey,
        label: recipient.typeKey === UNTYPED_RECIPIENT_TYPE
          ? 'Sin rubro'
          : (recipient.type?.trim() || 'Sin rubro'),
        count: 1
      });
    }

    return [...grouped.values()].sort((a, b) => {
      if (a.value === UNTYPED_RECIPIENT_TYPE) return 1;
      if (b.value === UNTYPED_RECIPIENT_TYPE) return -1;
      return a.label.localeCompare(b.label, 'es', { sensitivity: 'base' });
    });
  });
  allVisibleExternalRecipientsSelected = computed(() => {
    const visible = this.filteredExternalRecipients();
    return visible.length > 0 && visible.every(recipient => recipient.selected);
  });
  currentSort = signal<'name' | 'status' | 'payment_identifier'>('name');

  selectedSummary = computed(() => {
      if (this.formData().sendToAllEmails) {
        return 'Todos los emails de clientes y receptores externos';
      }

      const data = this.formData();
      const externalNames = data.externalRecipientIds.map(id => {
        const recipient = this.allExternalRecipients().find(item => item.id === id);
        return recipient?.displayName || recipient?.email || `Receptor #${id}`;
      });
      const recipients = [...data.recipients, ...externalNames];
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
    private http: HttpClient,
    private commService: CommunicationService, 
    private clientService: ClientService,
    private massRecipientService: MassCommunicationRecipientService,
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
<p><a href="https://www.guardeloquequiera.net/">guardeloquequiera.net</a></p>
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

                    <!-- Encabezado textual -->
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
                                    href="https://www.guardeloquequiera.net/"
                                    target="_blank"
                                    style="
                                        color: #ffffff;
                                        font-weight: 700;
                                        text-decoration: underline;
                                    "
                                >
                                    www.guardeloquequiera.net
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
    this.loadExternalRecipientsForSelector();
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
    externalRecipientIds: [],
    type: 'enviar_ahora',
    smtpConfigId: null,
    isAccountStatement: false,
    isNextMonthStatement: false,
    sendToAllEmails: false
  });
  
  currentModal = signal<'add' | 'edit' | 'view' | 'send-confirm' | 'retry' | 'extend' | 'history' | 'none'>('none');
  selectedCommunication = signal<ComunicacionDto | null>(null);
  transitioningCommunications = signal<Set<number>>(new Set());
  extensionPreview = signal<CommunicationExtensionPreview | null>(null);
  extensionRecipientType = signal(DEFAULT_EXTENSION_RECIPIENT_TYPE);
  extensionMode = signal<CommunicationExtensionMode>('never-attempted');
  isLoadingExtensionPreview = signal(false);
  isExtendingCommunication = signal(false);

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
      : data.recipients.length > 0 || data.externalRecipientIds.length > 0;
    const externalRecipientsAreValid = data.externalRecipientIds.length === 0
      || (!data.isAccountStatement && data.channels.includes('Email'));

    let baseValid = data.title.trim().length > 0 && 
                    contentIsValid && 
                    data.channels.length > 0 && 
                    recipientsAreValid &&
                    externalRecipientsAreValid;
    
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
      externalRecipientIds: [],
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
        externalRecipientIds: communication.sendToAllEmails
          ? []
          : (communication.externalRecipients || []).map(recipient => recipient.id),
        
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

    // El listado puede llegar antes de que se actualicen sus receptores
    // externos (o puede provenir de una versión anterior de la API). Para la
    // confirmación de envío usamos siempre el detalle actual del comunicado,
    // que incluye la relación communication_mass_recipients.
    if (communication && modalType === 'send-confirm') {
      this.commService.getCommunicationById(communication.id).subscribe({
        next: (fullComm) => this.selectedCommunication.set(fullComm),
        error: (err) => console.error('Error cargando destinatarios del comunicado', err)
      });
    }

    this.currentModal.set(finalModalType);
  }

  closeModal(): void {
    this.currentModal.set('none');
    this.selectedCommunication.set(null);
    this.extensionPreview.set(null);
    this.isLoadingExtensionPreview.set(false);
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
      externalRecipientIds: data.externalRecipientIds,
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

    if (channelName === 'Email' && !isAdding && this.formData().externalRecipientIds.length > 0) {
      this.showToast(
        'El rubro requiere Email',
        'Quitá los receptores externos seleccionados antes de desactivar el canal Email.',
        'mail',
        'error'
      );
      return;
    }
    
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

  getCommunicationPreview(content: string, _channel: string): string {
    return buildCommunicationPreviewText(content);
  }

  updateFormField<K extends keyof FormDataState>(field: K, value: FormDataState[K]) {
    this.formData.update(currentData => {
      const updated = {
        ...currentData,
        [field]: value
      };
      if (field === 'isAccountStatement' && value === true) {
        updated.title = 'ESTADO DE CUENTA';
        updated.externalRecipientIds = [];
        updated.sendToAllEmails = false;
        // El estado se puede entregar por ambos canales. Conservamos WhatsApp
        // si ya estaba elegido y agregamos Email como canal predeterminado.
        updated.channels = Array.from(new Set([...updated.channels, 'Email']));
      }
      return updated;
    });

    if (field === 'isAccountStatement' && value === true) {
      this.allExternalRecipients.update(recipients =>
        recipients.map(recipient => ({ ...recipient, selected: false })));
      this.filterExternalRecipients();
    }
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

  loadInmobiliariasTemplate(): void {
    if (this.isLoadingInmobiliariasTemplate()) return;

    this.isLoadingInmobiliariasTemplate.set(true);
    this.http.get(INMOBILIARIAS_TEMPLATE_URL, { responseType: 'text' }).subscribe({
      next: (template) => {
        if (!template.includes(INMOBILIARIAS_TEMPLATE_MARKER)) {
          this.isLoadingInmobiliariasTemplate.set(false);
          this.showToast(
            'Plantilla no disponible',
            'El archivo de la Plantilla Inmobiliarias no es válido.',
            'alert-circle',
            'error'
          );
          return;
        }

        this.formData.update(data => ({
          ...data,
          title: 'PUBLICIDAD | Una solución de guardado para tu inmobiliaria y tus clientes',
          channels: data.channels.includes('Email')
            ? data.channels
            : [...data.channels, 'Email'],
          content: template
        }));
        this.isLoadingInmobiliariasTemplate.set(false);
        this.showToast(
          'Plantilla Inmobiliarias cargada',
          'Elegí las inmobiliarias destinatarias o enviá una prueba antes del envío final.',
          'mail',
          'success'
        );
      },
      error: (error) => {
        console.error('No se pudo cargar la Plantilla Inmobiliarias', error);
        this.isLoadingInmobiliariasTemplate.set(false);
        this.showToast(
          'Error al cargar la plantilla',
          'No se pudo abrir la Plantilla Inmobiliarias.',
          'alert-circle',
          'error'
        );
      }
    });
  }

  loadLaboratoriosTemplate(): void {
    if (this.isLoadingLaboratoriosTemplate()) return;

    this.isLoadingLaboratoriosTemplate.set(true);
    this.http.get(LABORATORIOS_TEMPLATE_URL, { responseType: 'text' }).subscribe({
      next: (template) => {
        if (!template.includes(LABORATORIOS_TEMPLATE_MARKER)) {
          this.isLoadingLaboratoriosTemplate.set(false);
          this.showToast(
            'Plantilla no disponible',
            'El archivo de la Plantilla Laboratorios no es válido.',
            'alert-circle',
            'error'
          );
          return;
        }

        this.formData.update(data => ({
          ...data,
          title: 'PUBLICIDAD | Espacio flexible para materiales y equipamiento de tu laboratorio',
          channels: data.channels.includes('Email')
            ? data.channels
            : [...data.channels, 'Email'],
          content: template
        }));
        this.isLoadingLaboratoriosTemplate.set(false);
        this.showToast(
          'Plantilla Laboratorios cargada',
          'Elegí los laboratorios destinatarios o enviá una prueba antes del envío final.',
          'mail',
          'success'
        );
      },
      error: (error) => {
        console.error('No se pudo cargar la Plantilla Laboratorios', error);
        this.isLoadingLaboratoriosTemplate.set(false);
        this.showToast(
          'Error al cargar la plantilla',
          'No se pudo abrir la Plantilla Laboratorios.',
          'alert-circle',
          'error'
        );
      }
    });
  }

  loadVisitadoresMedicosTemplate(): void {
    if (this.isLoadingVisitadoresMedicosTemplate()) return;

    this.isLoadingVisitadoresMedicosTemplate.set(true);
    this.http.get(VISITADORES_MEDICOS_TEMPLATE_URL, { responseType: 'text' }).subscribe({
      next: (template) => {
        if (!template.includes(VISITADORES_MEDICOS_TEMPLATE_MARKER)) {
          this.isLoadingVisitadoresMedicosTemplate.set(false);
          this.showToast(
            'Plantilla no disponible',
            'El archivo de la Plantilla Visitadores Médicos no es válido.',
            'alert-circle',
            'error'
          );
          return;
        }

        this.formData.update(data => ({
          ...data,
          title: 'PUBLICIDAD | Más espacio para organizar tu material de trabajo',
          channels: data.channels.includes('Email')
            ? data.channels
            : [...data.channels, 'Email'],
          content: template
        }));
        this.isLoadingVisitadoresMedicosTemplate.set(false);
        this.showToast(
          'Plantilla Visitadores Médicos cargada',
          'Elegí los visitadores destinatarios o enviá una prueba antes del envío final.',
          'mail',
          'success'
        );
      },
      error: (error) => {
        console.error('No se pudo cargar la Plantilla Visitadores Médicos', error);
        this.isLoadingVisitadoresMedicosTemplate.set(false);
        this.showToast(
          'Error al cargar la plantilla',
          'No se pudo abrir la Plantilla Visitadores Médicos.',
          'alert-circle',
          'error'
        );
      }
    });
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

  getCommunicationPreviewDocument(): SafeHtml {
    return this.buildTrustedPreviewDocument(this.selectedCommunication()?.content);
  }

  getFormContentPreviewDocument(): SafeHtml {
    return this.buildTrustedPreviewDocument(this.formData().content);
  }

  private buildTrustedPreviewDocument(content: string | null | undefined): SafeHtml {
    // srcdoc necesita un valor confiable para conservar los estilos de email.
    // El iframe permanece sin allow-scripts ni allow-forms, por lo que el
    // contenido se muestra aislado y no puede ejecutar acciones en la app.
    return this.sanitizer.bypassSecurityTrustHtml(
      buildCommunicationPreviewDocument(content)
    );
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

  openExtensionModal(comm: ComunicacionDto): void {
    this.selectedCommunication.set(comm);
    this.extensionRecipientType.set(DEFAULT_EXTENSION_RECIPIENT_TYPE);
    this.extensionMode.set('never-attempted');
    this.extensionPreview.set(null);
    this.isExtendingCommunication.set(false);
    this.currentModal.set('extend');
    this.refreshExtensionPreview();
  }

  closeExtensionModal(): void {
    if (this.isExtendingCommunication()) return;
    this.closeModal();
  }

  onExtensionRecipientTypeChanged(recipientType: string): void {
    this.extensionRecipientType.set(recipientType);
    this.refreshExtensionPreview();
  }

  onExtensionModeChanged(mode: CommunicationExtensionMode): void {
    this.extensionMode.set(mode);
    this.refreshExtensionPreview();
  }

  refreshExtensionPreview(): void {
    const communication = this.selectedCommunication();
    const recipientType = this.extensionRecipientType().trim();
    if (!communication || !recipientType) {
      this.extensionPreview.set(null);
      return;
    }

    this.isLoadingExtensionPreview.set(true);
    this.commService.getCommunicationExtensionPreview(
      communication.id,
      recipientType,
      this.extensionMode()
    ).subscribe({
      next: preview => {
        this.extensionPreview.set(preview);
        this.isLoadingExtensionPreview.set(false);
      },
      error: err => {
        console.error('Error preparando la ampliación del comunicado', err);
        this.extensionPreview.set(null);
        this.isLoadingExtensionPreview.set(false);
        this.showToast(
          'No se puede ampliar el comunicado',
          err?.error?.message || 'Revisá que sea una campaña externa de Email ya finalizada.',
          'alert-triangle',
          'error'
        );
      }
    });
  }

  confirmExtendCommunication(): void {
    const communication = this.selectedCommunication();
    const preview = this.extensionPreview();
    if (!communication || !preview || preview.selectedForSendCount === 0) {
      this.showToast(
        'Sin destinatarios nuevos',
        'No hay receptores activos con email que cumplan el criterio seleccionado.',
        'alert-triangle',
        'error'
      );
      return;
    }

    this.isExtendingCommunication.set(true);
    this.commService.extendCommunication(communication.id, {
      recipientType: preview.recipientType,
      mode: preview.mode
    }).subscribe({
      next: result => {
        this.isExtendingCommunication.set(false);
        this.showToast(
          'Ampliación programada',
          `Se enviará a ${result.selectedForSendCount} receptor(es) sin repetir entregas reales.`,
          'check',
          'success'
        );
        this.closeModal();
        this.loadCommunications();
      },
      error: err => {
        console.error('Error ampliando el comunicado', err);
        this.isExtendingCommunication.set(false);
        this.showToast(
          'No se pudo ampliar',
          err?.error?.message || 'Volvé a abrir la campaña y revisá el detalle.',
          'alert-triangle',
          'error'
        );
      }
    });
  }

  canExtendCommunication(communication: ComunicacionDto): boolean {
    return communication.status === 'Finished'
      || communication.status === 'Finished w/ Errors'
      || communication.status === 'Failed';
  }

  viewDispatchContent(dispatchId: number, clientName: string): void {
    this.commService.getDispatchContent(dispatchId).subscribe({
      next: ({ content }) => {
        this.previewClientName.set(clientName);
        this.previewContent.set(this.buildTrustedPreviewDocument(content));
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
    return comm.dispatches.filter(d => !d.isTest && d.status !== 'Exitoso');
  }

  toggleAllRetrySelection(select: boolean): void {
    const comm = this.selectedCommunication();
    if (!comm || !comm.dispatches) return;
    comm.dispatches.forEach(d => {
      if (!d.isTest && d.status !== 'Exitoso') d.isSelected = select;
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
    return dispatches.filter(d => !d.isTest && d.status === 'Exitoso').length;
  }

  getFailedCount(dispatches: CommunicationDispatchDto[]): number {
    return dispatches.filter(d => !d.isTest && d.status !== 'Exitoso').length;
  }

  getTestCount(dispatches: CommunicationDispatchDto[]): number {
    return dispatches.filter(d => d.isTest).length;
  }

  getCommunicationRecipientCount(communication: ComunicacionDto): number {
    return communication.sendToAllEmails
      ? 0
      : communication.recipients.length + (communication.externalRecipients?.length || 0);
  }

  confirmRetrySelected(): void {
    const comm = this.selectedCommunication();
    if (!comm) return;
    const failed = this.getFailedDispatches(comm);
    const selected = failed.filter(d => d.isSelected !== false);
    
    if (selected.length === 0) {
      this.showToast('Advertencia', 'Debés seleccionar al menos un destinatario para reintentar.', 'alert-triangle', 'error');
      return;
    }

    const selectedClientIds = selected
      .filter(d => !d.isExternalRecipient)
      .map(d => d.clientId)
      .filter(id => id > 0);
    const selectedExternalRecipientIds = selected
      .map(d => d.externalRecipientId)
      .filter((id): id is number => typeof id === 'number' && id > 0);
    
    this.commService.retrySelectedCommunication(
      comm.id,
      selectedClientIds,
      selectedExternalRecipientIds
    ).subscribe({
      next: (res) => {
        this.showToast('Reintento Programado', 'Se enviará a ' + selected.length + ' destinatarios seleccionados.', 'check', 'success');
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

  loadExternalRecipientsForSelector(): void {
    this.isLoadingExternalRecipients.set(true);
    this.externalRecipientsLoadError.set(false);

    this.massRecipientService.getAll().subscribe({
      next: recipients => {
        const selectedIds = new Set(this.formData().externalRecipientIds);
        const mapped = recipients
          .filter(recipient => recipient.active && !!recipient.email?.trim())
          .map(recipient => {
            const trimmedType = recipient.type?.trim() || null;
            return {
              ...recipient,
              type: trimmedType,
              typeKey: this.getRecipientTypeKey(trimmedType),
              displayName: recipient.name?.trim() || recipient.email?.trim() || `Receptor #${recipient.id}`,
              selected: selectedIds.has(recipient.id)
            } satisfies ExternalRecipientSelectorItem;
          });

        this.allExternalRecipients.set(mapped);
        this.isLoadingExternalRecipients.set(false);
        this.filterExternalRecipients();
      },
      error: error => {
        console.error('Error cargando receptores externos', error);
        this.allExternalRecipients.set([]);
        this.filteredExternalRecipients.set([]);
        this.isLoadingExternalRecipients.set(false);
        this.externalRecipientsLoadError.set(true);
      }
    });
  }

  private getRecipientTypeKey(type: string | null | undefined): string {
    const normalized = type?.trim().toLocaleLowerCase('es-AR');
    return normalized || UNTYPED_RECIPIENT_TYPE;
  }

  showClientRecipientSelector(): void {
    if (this.modalSendToAllEmails()) {
      this.modalSendToAllEmails.set(false);
      this.activeQuickFilter.set(null);
    }
    this.recipientSelectorMode.set('clients');
    this.filterList();
  }

  showExternalRecipientSelector(): void {
    if (this.formData().isAccountStatement) {
      this.showToast(
        'Sólo clientes',
        'Los estados de cuenta no pueden enviarse a receptores externos.',
        'alert-circle',
        'error'
      );
      return;
    }

    if (this.modalSendToAllEmails()) {
      this.modalSendToAllEmails.set(false);
      this.activeQuickFilter.set(null);
    }
    this.recipientSelectorMode.set('external');
    this.filterExternalRecipients();
  }

  onExternalRecipientSearch(term: string): void {
    this.externalRecipientSearchTerm.set(term);
    this.filterExternalRecipients();
  }

  onExternalRecipientTypeChange(typeKey: string): void {
    this.selectedExternalRecipientType.set(typeKey || '');
    this.filterExternalRecipients();
  }

  filterExternalRecipients(): void {
    const typeKey = this.selectedExternalRecipientType();
    const term = this.externalRecipientSearchTerm().trim().toLocaleLowerCase('es-AR');
    let recipients = this.allExternalRecipients();

    if (typeKey) {
      recipients = recipients.filter(recipient => recipient.typeKey === typeKey);
    }

    if (term) {
      recipients = recipients.filter(recipient =>
        recipient.displayName.toLocaleLowerCase('es-AR').includes(term)
        || !!recipient.email?.toLocaleLowerCase('es-AR').includes(term)
        || !!recipient.type?.toLocaleLowerCase('es-AR').includes(term));
    }

    this.filteredExternalRecipients.set([...recipients].sort((a, b) =>
      a.displayName.localeCompare(b.displayName, 'es', { sensitivity: 'base' })));
  }

  toggleExternalRecipientSelection(id: number): void {
    this.allExternalRecipients.update(recipients => recipients.map(recipient =>
      recipient.id === id ? { ...recipient, selected: !recipient.selected } : recipient));
    this.filterExternalRecipients();
  }

  toggleVisibleExternalRecipientsSelection(): void {
    const visibleIds = new Set(this.filteredExternalRecipients().map(recipient => recipient.id));
    if (visibleIds.size === 0) return;

    const shouldSelect = !this.allVisibleExternalRecipientsSelected();
    this.allExternalRecipients.update(recipients => recipients.map(recipient =>
      visibleIds.has(recipient.id) ? { ...recipient, selected: shouldSelect } : recipient));
    this.filterExternalRecipients();
  }

  openRecipientSelector(): void {
    const data = this.formData();
    const currentRecipients = new Set(data.recipients);
    const currentExternalRecipientIds = new Set(data.externalRecipientIds);
    const sendToAllEmails = data.sendToAllEmails;
    this.modalSendToAllEmails.set(sendToAllEmails);
    this.activeQuickFilter.set(sendToAllEmails ? 'TodosLosEmails' : null);
    this.recipientSearchTerm.set('');
    this.externalRecipientSearchTerm.set('');

    this.allClients.update(clients => clients.map(client => ({
      ...client,
      selected: !sendToAllEmails && currentRecipients.has(client.fullName)
    })));
    this.allExternalRecipients.update(recipients => recipients.map(recipient => ({
      ...recipient,
      selected: !sendToAllEmails && currentExternalRecipientIds.has(recipient.id)
    })));

    const selectedExternalRecipients = this.allExternalRecipients().filter(recipient => recipient.selected);
    const selectedTypeKeys = new Set(selectedExternalRecipients.map(recipient => recipient.typeKey));
    this.selectedExternalRecipientType.set(selectedTypeKeys.size === 1 ? [...selectedTypeKeys][0] : '');
    this.recipientSelectorMode.set(
      !sendToAllEmails && currentRecipients.size === 0 && currentExternalRecipientIds.size > 0
        ? 'external'
        : 'clients');
    this.filterList();
    this.filterExternalRecipients();
    this.showRecipientModal.set(true);
  }

  applyFilter(type: 'Todos' | 'Ninguno' | 'MesImpago', targetYear?: number, targetMonth?: number, filterLabel?: string): void {
      if (this.modalSendToAllEmails()) {
          this.modalSendToAllEmails.set(false);
      }
      this.recipientSelectorMode.set('clients');

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
          this.allExternalRecipients.update(recipients =>
            recipients.map(recipient => ({ ...recipient, selected: false })));
          this.filterExternalRecipients();
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
      if (this.formData().isAccountStatement) {
          this.showToast(
            'Sólo clientes',
            'Los estados de cuenta requieren una selección de clientes.',
            'alert-circle',
            'error'
          );
          return;
      }

      this.activeQuickFilter.set('TodosLosEmails');
      this.modalSendToAllEmails.set(true);
      this.recipientSelectorMode.set('clients');
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
              externalRecipientIds: [],
              sendToAllEmails: true
          }));
          this.showRecipientModal.set(false);
          return;
      }

      const selectedNames = this.allClients()
          .filter(c => c.selected)
          .map(c => c.fullName);
      const selectedExternalRecipientIds = this.allExternalRecipients()
          .filter(recipient => recipient.selected)
          .map(recipient => recipient.id);

      if (selectedNames.length === 0 && selectedExternalRecipientIds.length === 0) {
          this.showToast(
            'Sin destinatarios',
            'Seleccioná al menos un cliente o un receptor externo.',
            'alert-circle',
            'error'
          );
          return;
      }
      
      this.formData.update(data => ({
          ...data,
          channels: selectedExternalRecipientIds.length > 0 && !data.channels.includes('Email')
            ? [...data.channels, 'Email']
            : data.channels,
          recipients: selectedNames,
          externalRecipientIds: data.isAccountStatement ? [] : selectedExternalRecipientIds,
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
