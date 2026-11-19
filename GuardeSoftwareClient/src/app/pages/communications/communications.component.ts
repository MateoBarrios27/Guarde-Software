import { Component, signal, computed, ChangeDetectionStrategy, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
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
import { catchError, debounceTime, distinctUntilChanged, finalize, of, Subject, switchMap, Subscription } from 'rxjs';
import { QuillModule } from 'ngx-quill';
import { getLogisticasTemplateCopy, updateLogisticasTemplateCopy } from '../../shared/utils/logisticas-template.util';
import {
  INMOBILIARIAS_TEMPLATE_MARKER,
  LABORATORIOS_TEMPLATE_MARKER,
  LOGISTICAS_TEMPLATE_MARKER,
  VISITADORES_MEDICOS_TEMPLATE_MARKER
} from '../../shared/utils/communication-template-catalog';
import { CommunicationTemplatesComponent, PickedCommunicationTemplate } from './communication-templates/communication-templates.component';
import {
  buildCommunicationPreviewDocument,
  buildCommunicationPreviewText
} from '../../shared/utils/communication-preview.util';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { DataRefreshService } from '../../core/services/data-refresh-service/data-refresh.service';
import { AuthService } from '../../core/services/auth-service/auth.service';

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

/** Pausa mínima entre dos envíos desde Comunicaciones (seguro contra doble clic). */
const SUBMIT_COOLDOWN_MS = 3000;
const UNTYPED_RECIPIENT_TYPE = '__sin_rubro__';
const DEFAULT_EXTENSION_RECIPIENT_TYPE = 'inmobiliaria';

@Component({
  selector: 'communications',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, QuillModule, CommunicationTemplatesComponent],
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
  /** Gestor de plantillas abierto: «Plantillas» (manage) o «Cargar plantilla» desde el alta (pick). */
  templatesView = signal<'manage' | 'pick' | null>(null);
  isEditingLogisticasText = signal(false);
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
    if (content.includes(LOGISTICAS_TEMPLATE_MARKER)) return 'Logísticas';
    return '';
  });
  isDesignedMarketingTemplate = computed(() => this.activeDesignedTemplateLabel().length > 0);
  isLogisticasTemplate = computed(() => this.formData().content.includes(LOGISTICAS_TEMPLATE_MARKER));
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
  private readonly dataRefreshSubscription = new Subscription();

  constructor(
    private commService: CommunicationService, 
    private clientService: ClientService,
    private massRecipientService: MassCommunicationRecipientService,
    private sanitizer: DomSanitizer,
    private deleteConfirmation: DeleteConfirmationService,
    private dataRefresh: DataRefreshService,
    public authService: AuthService,
  ) {}


  ngOnInit(): void {
    if (!this.authService.isObserver()) {
      this.dataRefreshSubscription.add(
        this.dataRefresh.watch(['clients', 'catalog'], 'communications').subscribe(event => {
          if (event.domains.includes('clients')) {
            this.loadRecipientOptions();
            this.loadClientsForSelector();
          }
          if (event.domains.includes('catalog')) {
            this.loadSmtpConfigs();
            this.loadExternalRecipientsForSelector();
          }
        }),
      );
    }

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
    this.setupSearchDebounce();
    if (!this.authService.isObserver()) {
      this.loadRecipientOptions();
      this.loadSmtpConfigs();
      this.loadClientsForSelector();
      this.loadExternalRecipientsForSelector();
    }
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
    this.dataRefreshSubscription.unsubscribe();
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
    channels: ['Email'],
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
                    data.channels.includes('Email') &&
                    recipientsAreValid &&
                    externalRecipientsAreValid;
    
    if (data.type === 'programar') {
      return baseValid && data.sendDate.length > 0 && data.sendTime.length > 0;
    }
    
    return baseValid;
  });

  /**
   * Qué impide enviar, en palabras del operador, para la franja del alta.
   * Sólo presentación: las condiciones son las mismas de `isFormValid`.
   */
  formMissing = computed<string[]>(() => {
    const data = this.formData();
    const missing: string[] = [];
    const hasRecipients = data.sendToAllEmails || data.recipients.length > 0 || data.externalRecipientIds.length > 0;
    const isContentEmpty = !data.content || data.content.trim() === '<p><br></p>' || data.content.trim() === '';

    if (!hasRecipients) missing.push('destinatarios');
    if (!data.title.trim()) missing.push('título');
    if (!data.isAccountStatement && isContentEmpty) missing.push('contenido');
    if (data.externalRecipientIds.length > 0 && data.isAccountStatement) missing.push('quitar los receptores externos');
    if (data.type === 'programar' && (!data.sendDate || !data.sendTime)) missing.push('fecha y hora');
    return missing;
  });

  formClientRecipientCount = computed(() => this.formData().recipients.length);
  formExternalRecipientCount = computed(() => this.formData().externalRecipientIds.length);


  private resetForm(): void {
    const defaultSmtp = this.smtpConfigs().length > 0 ? this.smtpConfigs()[0].id : null;

    this.formData.set({
      id: null,
      title: '',
      content: '',
      sendDate: '',
      sendTime: '',
      channels: ['Email'],
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
    if (this.authService.isObserver() && modalType !== 'view' && modalType !== 'history') {
      return;
    }

    this.selectedCommunication.set(communication);
    this.viewDispatchFilter.set(null);
    this.viewShowContent.set(false);
    this.resetForm();

    let finalModalType = modalType;

    if (communication && (modalType === 'edit' || isResend)) {
      // Historical WhatsApp metadata remains visible in the detail, but editing
      // or cloning always creates an Email-only communication.
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
        channels: ['Email'],
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
    
    this.retryExcluded.set(new Set());
    this.retrySearch.set('');
    this.sendMode.set('now');
    this.sendScheduleDate.set('');
    this.sendScheduleTime.set('');
    if (communication && (modalType === 'view' || modalType === 'retry')) {
      this.commService.getCommunicationById(communication.id).subscribe({
        next: (fullComm) => {
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

  /**
   * Seguro contra envíos dobles: bloquea las acciones que crean o envían un comunicado mientras hay una
   * petición en curso y durante SUBMIT_COOLDOWN_MS después de cada una (también si falló), aunque el botón
   * se toque varias veces o se reabra el formulario.
   */
  readonly isSubmitting = signal(false);
  private lastSubmitAt = 0;

  private beginSubmit(): boolean {
    if (this.isSubmitting() || Date.now() - this.lastSubmitAt < SUBMIT_COOLDOWN_MS) return false;
    this.isSubmitting.set(true);
    this.lastSubmitAt = Date.now();
    return true;
  }

  /**
   * 409 DUPLICATE_COMMUNICATION de la API: el comunicado ya se creó o ya está en cola. Avisa con el mensaje del
   * servidor, cierra el formulario y recarga la lista para que se vea el que ya existe. Devuelve true si lo manejó.
   */
  private handleDuplicateError(err: unknown): boolean {
    const error = err as { status?: number; error?: { code?: string; message?: string } };
    if (error?.status !== 409 || error.error?.code !== 'DUPLICATE_COMMUNICATION') return false;
    this.showToast('Envío ya registrado', error.error.message || 'Este comunicado ya se envió.', 'alert-triangle', 'error');
    this.closeModal();
    this.loadCommunications();
    return true;
  }

  private endSubmit(): void {
    this.lastSubmitAt = Date.now();
    this.isSubmitting.set(false);
  }

  addCommunication(): void {
    const data = this.formData();
    if (!this.isFormValid()) { return; }
    if (!this.beginSubmit()) { return; }

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
      channels: ['Email'],
      type: finalType,
      sendDate: finalSendDate,
      sendTime: finalSendTime
    };

    this.commService.createCommunication(request, this.selectedFiles()).pipe(finalize(() => this.endSubmit())).subscribe({
      next: (newCommunication) => {
        this.communications.update(comms => [newCommunication, ...comms]);
        this.closeModal();
        this.showToast('¡Comunicado creado!', 'Se guardó correctamente', '📨', 'success');
        this.selectedFiles.set([]);
      },
      error: (err) => { if (!this.handleDuplicateError(err)) this.showToast('Error', 'No se pudo crear el comunicado', '❌', 'error'); }
    });
  }

  sendTestCommunication(): void {
    const data = this.formData();
    if (!this.isFormValid()) { return; }
    if (!this.beginSubmit()) { return; }

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
      channels: ['Email'],
      type: 'schedule',
      sendDate: finalSendDate,
      sendTime: finalSendTime,
      isTestMode: true,
      testEmailAddress: 'fsgbrunofranco@gmail.com'
    };

    this.commService.createCommunication(request, this.selectedFiles()).pipe(finalize(() => this.endSubmit())).subscribe({
      next: (newCommunication) => {
        this.communications.update(comms => [newCommunication, ...comms]);
        this.closeModal();
        this.showToast(
          '¡Prueba enviada!',
          'El envío se está procesando y llegará por Email a fsgbrunofranco@gmail.com.',
          'check-circle',
          'success');
      },
      error: (err) => {
        console.error(err);
        if (!this.handleDuplicateError(err)) this.showToast('Error', 'No se pudo enviar la prueba', 'alert-circle', 'error');
      }
    });
  }

  editCommunication(): void {
    const data = this.formData();
    const commId = data.id;

    if (!commId || !this.isFormValid()) { return; }
    if (!this.beginSubmit()) { return; }

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
      channels: ['Email'],
      recipients: data.recipients,
      externalRecipientIds: data.externalRecipientIds,
      smtpConfigId: data.smtpConfigId,
      isAccountStatement: data.isAccountStatement,
      isNextMonthStatement: data.isNextMonthStatement,
      sendToAllEmails: data.sendToAllEmails
    };

    this.commService.updateCommunication(commId, request).pipe(finalize(() => this.endSubmit())).subscribe({
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

  /** «Enviar» un guardado: el operador elige entre enviar ya o programar fecha y hora. */
  sendMode = signal<'now' | 'schedule'>('now');
  sendScheduleDate = signal('');
  sendScheduleTime = signal('');

  /** Mensaje de lo que falta para programar (vacío = listo). */
  getScheduleProblem(): string {
    if (this.sendMode() !== 'schedule') return '';
    const date = this.sendScheduleDate();
    const time = this.sendScheduleTime();
    if (!date || !time) return 'Elegí fecha y hora';
    const when = new Date(`${date}T${time}`);
    if (isNaN(when.getTime())) return 'Fecha u hora inválida';
    if (when.getTime() <= Date.now()) return 'Elegí un horario futuro';
    return '';
  }

  getTodayIso(): string {
    const n = new Date();
    return `${n.getFullYear()}-${String(n.getMonth() + 1).padStart(2, '0')}-${String(n.getDate()).padStart(2, '0')}`;
  }

  confirmSend(comm: ComunicacionDto): void {
    if (this.sendMode() === 'now') { this.handleSendCommunication(comm.id); return; }
    if (this.getScheduleProblem()) return;
    if (!this.beginSubmit()) { return; }
    const request: UpsertComunicacionRequest = {
      id: comm.id,
      title: comm.title,
      content: comm.content,
      type: 'schedule',
      sendDate: this.sendScheduleDate(),
      sendTime: this.sendScheduleTime(),
      channels: ['Email'],
      recipients: comm.sendToAllEmails ? [] : [...comm.recipients],
      externalRecipientIds: comm.sendToAllEmails ? [] : (comm.externalRecipients || []).map(r => r.id),
      smtpConfigId: comm.smtpConfigId ?? null,
      isAccountStatement: comm.isAccountStatement || false,
      isNextMonthStatement: comm.isNextMonthStatement || false,
      sendToAllEmails: comm.sendToAllEmails || false
    };
    this.commService.updateCommunication(comm.id, request).pipe(finalize(() => this.endSubmit())).subscribe({
      next: (updated) => {
        this.communications.update(list => list.map(c => c.id === comm.id ? updated : c));
        this.closeModal();
        this.showToast('Comunicado programado', 'Se enviará en el horario elegido.', 'clock', 'success');
      },
      error: () => this.showToast('Error', 'No se pudo programar el comunicado', 'alert-triangle', 'error')
    });
  }

  handleSendCommunication(communicationId: number): void {
    if (!this.beginSubmit()) { return; }
    this.commService.sendDraftNow(communicationId).pipe(finalize(() => this.endSubmit())).subscribe({
      next: (sentComm) => {
        this.communications.update(comms => comms.map(c => c.id === communicationId ? sentComm : c));
        this.closeModal();
        this.showToast('¡Comunicado enviado!', 'El envío se ha puesto en cola', '✅', 'success');
      },
      error: (err) => { if (!this.handleDuplicateError(err)) this.showToast('Error', 'No se pudo enviar', '❌', 'error'); }
    });
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

  /** Estado en «Ver detalle»: cápsula sólida (más precisa que la tarjeta: distingue «con errores»). */
  getDetailStatus(status: ComunicacionDto['status']): { text: string; tone: string; icon: string } {
    switch (status) {
      case 'Finished': return { text: 'Enviado', tone: 'is-finished', icon: 'check-circle' };
      case 'Finished w/ Errors': return { text: 'Enviado con errores', tone: 'is-warning', icon: 'alert-triangle' };
      case 'Failed': return { text: 'Fallido', tone: 'is-error', icon: 'alert-octagon' };
      case 'Scheduled': return { text: 'Programado', tone: 'is-scheduled', icon: 'clock' };
      case 'Draft': return { text: 'Borrador', tone: 'is-draft', icon: 'file-text' };
      default: return { text: status, tone: 'is-draft', icon: 'info' };
    }
  }

  /**
   * Ajusta la vista previa del contenido a su alto real (entre 10 y 36 rem): un texto corto no queda
   * en un marco enorme y un correo diseñado no se corta. El iframe es same-origin (sandbox sin scripts).
   */
  /** Barra de desplazamiento fina dentro de un iframe de vista previa (same-origin, sin scripts). */
  styleScroll(event: Event): void {
    try {
      const doc = (event.target as HTMLIFrameElement).contentDocument;
      if (doc?.head && !doc.getElementById('guarde-preview-scroll')) {
        const style = doc.createElement('style');
        style.id = 'guarde-preview-scroll';
        style.textContent = 'html{scrollbar-width:thin;scrollbar-color:#cbd5e1 transparent}'
          + '::-webkit-scrollbar{width:8px;height:8px}::-webkit-scrollbar-thumb{background:#cbd5e1;border-radius:8px}'
          + '::-webkit-scrollbar-track{background:transparent}';
        doc.head.appendChild(style);
      }
    } catch { /* sin acceso: queda la barra del navegador */ }
  }

  fitPreviewFrame(event: Event): void {
    const frame = event.target as HTMLIFrameElement;
    try {
      const doc = frame.contentDocument;
      if (!doc?.body) return;
      // Barra de desplazamiento fina dentro del marco (sólo si el contenido supera el alto máximo).
      if (!doc.getElementById('guarde-preview-scroll')) {
        const style = doc.createElement('style');
        style.id = 'guarde-preview-scroll';
        style.textContent = 'html{scrollbar-width:thin;scrollbar-color:#cbd5e1 transparent}'
          + '::-webkit-scrollbar{width:8px;height:8px}::-webkit-scrollbar-thumb{background:#cbd5e1;border-radius:8px}'
          + '::-webkit-scrollbar-track{background:transparent}';
        doc.head?.appendChild(style);
      }
      // scrollHeight nunca es menor que el alto visible: se mide con el marco colapsado.
      frame.style.height = '0px';
      const height = Math.max(doc.documentElement.scrollHeight, doc.body.scrollHeight);
      const rem = parseFloat(getComputedStyle(document.documentElement).fontSize) || 16;
      frame.style.height = `${Math.min(Math.max(height + 2, 10 * rem), 36 * rem)}px`;
    } catch {
      // Sin acceso al documento: queda el alto por defecto del CSS.
    }
  }

  getCommunicationPreview(content: string, _channel: string): string {
    return buildCommunicationPreviewText(content);
  }

  updateFormField<K extends keyof FormDataState>(field: K, value: FormDataState[K]) {
    this.formData.update(currentData => {
      const updated = {
        ...currentData,
        [field]: value,
        channels: ['Email'] as FormDataState['channels']
      };
      if (field === 'isAccountStatement' && value === true) {
        updated.title = 'ESTADO DE CUENTA';
        updated.externalRecipientIds = [];
        updated.sendToAllEmails = false;
        updated.channels = ['Email'];
      }
      return updated;
    });

    if (field === 'isAccountStatement' && value === true) {
      this.allExternalRecipients.update(recipients =>
        recipients.map(recipient => ({ ...recipient, selected: false })));
      this.filterExternalRecipients();
    }
  }

  /** Botón «Plantillas» del encabezado. El Observador las ve en sólo lectura. */
  openTemplatesManager(): void {
    this.templatesView.set('manage');
  }

  /** «Cargar plantilla» dentro del alta/edición de un comunicado. */
  openTemplatePicker(): void {
    if (this.authService.isObserver()) return;
    this.templatesView.set('pick');
  }

  closeTemplates(): void {
    this.templatesView.set(null);
  }

  /**
   * Plantilla elegida en el gestor. Desde «Cargar plantilla» completa el comunicado abierto; desde
   * «Usar en un comunicado» abre uno nuevo con ella. Destinatarios, servidor y adjuntos no cambian.
   */
  onTemplatePicked(picked: PickedCommunicationTemplate): void {
    const fromManager = this.templatesView() === 'manage';
    this.templatesView.set(null);
    if (fromManager) {
      if (this.authService.isObserver()) return;
      this.openModal('add');
    }
    this.applyTemplate(picked);
  }

  applyTemplate(picked: PickedCommunicationTemplate): void {
    this.formData.update(data => ({
      ...data,
      title: picked.template.subject,
      channels: ['Email'],
      isAccountStatement: false,
      isNextMonthStatement: false,
      content: picked.content
    }));
    this.isEditingLogisticasText.set(false);
    this.showToast(
      `Plantilla ${picked.template.name} cargada`,
      'Revisá el contenido, elegí los destinatarios y enviá una prueba antes del envío final.',
      'mail',
      'success'
    );
  }

  getLogisticasCopyContent(): string {
    return getLogisticasTemplateCopy(this.formData().content);
  }

  updateLogisticasCopyContent(copy: string | null): void {
    if (!this.isLogisticasTemplate()) return;
    this.formData.update(data => ({ ...data, content: updateLogisticasTemplateCopy(data.content, copy) }));
  }

  getCommunicationPreviewDocument(): SafeHtml {
    return this.buildTrustedPreviewDocument(this.selectedCommunication()?.content);
  }

  getFormContentPreviewDocument(): SafeHtml {
    return this.buildTrustedPreviewDocument(this.formData().content);
  }

  private readonly trustedPreviewCache = new Map<string, SafeHtml>();

  private buildTrustedPreviewDocument(content: string | null | undefined): SafeHtml {
    // srcdoc necesita un valor confiable para conservar los estilos de email.
    // El iframe permanece sin allow-scripts ni allow-forms, por lo que el
    // contenido se muestra aislado y no puede ejecutar acciones en la app.
    // Se devuelve SIEMPRE el mismo objeto para el mismo contenido: si cada
    // detección de cambios entregara uno nuevo, Angular reasignaría `srcdoc`,
    // el marco se recargaría, su `load` dispararía otra detección y el
    // contenido parpadearía sin fin («Ver contenido» se «bugueaba»).
    const key = content ?? '';
    const cached = this.trustedPreviewCache.get(key);
    if (cached) return cached;
    const trusted = this.sanitizer.bypassSecurityTrustHtml(buildCommunicationPreviewDocument(content));
    if (this.trustedPreviewCache.size >= 8) {
      this.trustedPreviewCache.delete(this.trustedPreviewCache.keys().next().value as string);
    }
    this.trustedPreviewCache.set(key, trusted);
    return trusted;
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
    if (this.authService.isObserver()) return;

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
    const isFinished = communication.status === 'Finished'
      || communication.status === 'Finished w/ Errors'
      || communication.status === 'Failed';
    const channel = communication.channel.toLocaleLowerCase();
    return isFinished && channel.includes('email') && !channel.includes('whatsapp');
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
      if (!this.canSendCommunicationByEmail(comm)) return [];
      return comm.recipients.map((name, idx) => ({
        dispatchId: -(idx + 1),
        clientId: -(idx + 1),
        clientName: name,
        channel: 'Email',
        status: 'Fallido',
        errorMessage: comm.errorMessage || 'Envío fallido o interrumpido',
        dispatchDate: comm.creationDate
      }));
    }
    return comm.dispatches.filter(d => this.isFailedDispatch(d) && d.channel.toLocaleLowerCase() === 'email');
  }

  canSendCommunicationByEmail(comm: ComunicacionDto): boolean {
    return comm.channel.toLocaleLowerCase().includes('email');
  }

  /**
   * Reenvío: la selección vive aparte de los despachos (ids que el operador DESmarcó; por defecto va
   * todo). Así no se muta el DTO y funciona también con la lista sin despachos, que se arma de nuevo
   * en cada lectura.
   */
  retryExcluded = signal<ReadonlySet<number>>(new Set());
  retrySearch = signal('');

  isRetrySelected(d: CommunicationDispatchDto): boolean {
    return !this.retryExcluded().has(d.dispatchId);
  }

  toggleRetry(d: CommunicationDispatchDto): void {
    this.retryExcluded.update(current => {
      const next = new Set(current);
      if (!next.delete(d.dispatchId)) next.add(d.dispatchId);
      return next;
    });
  }

  toggleAllRetrySelection(select: boolean): void {
    const comm = this.selectedCommunication();
    if (!comm) return;
    this.retryExcluded.set(select ? new Set() : new Set(this.getFailedDispatches(comm).map(d => d.dispatchId)));
  }

  getSelectedRetryCount(): number {
    const comm = this.selectedCommunication();
    if (!comm) return 0;
    return this.getFailedDispatches(comm).filter(d => this.isRetrySelected(d)).length;
  }

  /** Fallidos que coinciden con la búsqueda del modal de reenvío (nombre, motivo o canal). */
  getRetryRows(comm: ComunicacionDto): CommunicationDispatchDto[] {
    const term = this.retrySearch().trim().toLocaleLowerCase();
    const failed = this.getFailedDispatches(comm);
    if (!term) return failed;
    return failed.filter(d => `${d.clientName} ${d.errorMessage ?? ''}`.toLocaleLowerCase().includes(term));
  }

  /** Fallido = intentó y no llegó. «Pendiente» (programado o en cola) todavía no se intentó: no es un error. */
  isFailedDispatch(d: CommunicationDispatchDto): boolean {
    return !d.isTest && d.status !== 'Exitoso' && d.status !== 'Pendiente';
  }

  getPendingCount(dispatches: CommunicationDispatchDto[]): number {
    return dispatches.filter(d => !d.isTest && d.status === 'Pendiente').length;
  }

  getSuccessCount(dispatches: CommunicationDispatchDto[]): number {
    return dispatches.filter(d => !d.isTest && d.status === 'Exitoso').length;
  }

  getFailedCount(dispatches: CommunicationDispatchDto[]): number {
    return dispatches.filter(d => this.isFailedDispatch(d)).length;
  }

  getTestCount(dispatches: CommunicationDispatchDto[]): number {
    return dispatches.filter(d => d.isTest).length;
  }

  /**
   * «Ver detalle»: tramo elegido en la barra de resultado (null = el predeterminado:
   * fallidos si los hay, si no exitosos, si no prueba). Sólo cambia qué filas se ven.
   */
  viewDispatchFilter = signal<'failed' | 'success' | 'pending' | 'test' | null>(null);
  viewShowContent = signal(false);

  getViewFilter(dispatches: CommunicationDispatchDto[]): 'failed' | 'success' | 'pending' | 'test' {
    const picked = this.viewDispatchFilter();
    if (picked) return picked;
    if (this.getFailedCount(dispatches) > 0) return 'failed';
    if (this.getSuccessCount(dispatches) > 0) return 'success';
    if (this.getPendingCount(dispatches) > 0) return 'pending';
    return 'test';
  }

  getViewDispatches(dispatches: CommunicationDispatchDto[]): CommunicationDispatchDto[] {
    switch (this.getViewFilter(dispatches)) {
      case 'failed': return dispatches.filter(d => this.isFailedDispatch(d));
      case 'success': return dispatches.filter(d => !d.isTest && d.status === 'Exitoso');
      case 'pending': return dispatches.filter(d => !d.isTest && d.status === 'Pendiente');
      default: return dispatches.filter(d => d.isTest);
    }
  }

  /** Frase principal del detalle: lo primero que necesita saber el operador. */
  getViewHeadline(comm: ComunicacionDto): string {
    const dispatches = comm.dispatches || [];
    const failed = this.getFailedCount(dispatches);
    switch (comm.status) {
      case 'Finished': return 'Llegó a todos los destinatarios.';
      case 'Finished w/ Errors': return failed === 1
        ? '1 envío no llegó. Revisá el motivo y reintentá.'
        : `${failed} envíos no llegaron. Revisá el motivo y reintentá.`;
      case 'Failed': return failed > 0 ? 'El envío falló. Revisá el motivo y reintentá.' : 'El envío falló.';
      case 'Scheduled': return 'Todavía no se envió: está programado.';
      case 'Draft': return 'Borrador: todavía no se envió.';
      case 'Procesando': return 'Se está enviando.';
      default: return '';
    }
  }

  canRetryFromView(comm: ComunicacionDto): boolean {
    return !this.authService.isObserver()
      && !!comm.channel
      && this.canSendCommunicationByEmail(comm)
      && (comm.status === 'Failed' || comm.status === 'Finished w/ Errors')
      && this.getFailedCount(comm.dispatches || []) > 0;
  }

  getSmtpName(id: number | null | undefined): string {
    if (id == null) return '';
    return this.smtpConfigs().find(config => config.id === id)?.name || '';
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
    const selected = failed.filter(d => this.isRetrySelected(d));

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
    
    if (!this.beginSubmit()) { return; }
    this.commService.retrySelectedCommunication(
      comm.id,
      selectedClientIds,
      selectedExternalRecipientIds
    ).pipe(finalize(() => this.endSubmit())).subscribe({
      next: (res) => {
        this.showToast('Reintento Programado', 'Se enviará a ' + selected.length + ' destinatarios seleccionados.', 'check', 'success');
        this.closeModal();
        this.loadCommunications();
      },
      error: (err) => {
        console.error('Error en reintento', err);
        if (!this.handleDuplicateError(err)) this.showToast('Error', 'Hubo un error al iniciar el reintento.', 'alert-triangle', 'error');
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
          channels: ['Email'],
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
