import { Component, signal, computed, ChangeDetectionStrategy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent } from "../../shared/components/icon/icon.component";
import { CommunicationService } from '../../core/services/communication-service/communication.service';
// --- DTOs Actualizados ---
import { ComunicacionDto, UpsertComunicacionRequest, AttachmentDto } from '../../core/dtos/communications/communicationDto';
import { ClientService } from '../../core/services/client-service/client.service';
import { catchError, debounceTime, distinctUntilChanged, of, Subject, switchMap, finalize } from 'rxjs';

import { QuillModule } from 'ngx-quill';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

// --- Type Definitions (English Code) ---

interface Channel {
  id: number;
  name: 'Email' | 'WhatsApp';
  spanishLabel: 'Email' | 'WhatsApp';
  icon: string;
}

interface FormDataState {
  id: number | null;
  title: string;
  content: string; 
  sendDate: string;
  sendTime: string;
  channels: ('Email' | 'WhatsApp')[];
  recipients: string[];
  type: 'programar' | 'borrador';
}

interface ToastState {
  show: boolean;
  message: string;
  description: string;
  icon: string;
  color: 'success' | 'error';
}

interface MailServer {
  id: string;
  name: string;
  description: string;
}

const COMMUNICATION_CHANNELS: Channel[] = [
  { id: 1, name: 'Email', spanishLabel: 'Email', icon: 'Mail' },
  { id: 2, name: 'WhatsApp', spanishLabel: 'WhatsApp', icon: 'MessageSquare' }
];

@Component({
  selector: 'communications',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, QuillModule],
  templateUrl: './communications.component.html',
  styleUrl: './communications.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CommunicationsComponent implements OnInit {

  // --- Signals (State Management) ---
  communications = signal<ComunicacionDto[]>([]); 
  staticGroups = signal<string[]>([]); 
  searchResults = signal<string[]>([]);
  isSearchFocused = signal(false);
  private searchSubject = new Subject<string>();

  // --- Signals para adjuntos y reintentos ---
  attachments = signal<File[]>([]); // Archivos NUEVOS
  existingAttachments = signal<AttachmentDto[]>([]); // Archivos que YA ESTÁN en el VPS
  isSubmitting = signal(false); // Para deshabilitar botones al enviar
  
  mailServers = signal<MailServer[]>([]);
  selectedMailServer = signal<string>('');
  
  constructor(
    private commService: CommunicationService, 
    private clientService: ClientService,
    private sanitizer: DomSanitizer 
  ) {}

  ngOnInit(): void {
    this.loadCommunications();
    this.loadRecipientOptions();
    this.setupSearchDebounce(); 
    this.loadMailServers();
  }

  loadCommunications(): void {
    this.commService.getCommunications().subscribe({ 
      next: (data) => this.communications.set(data),
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
        this.staticGroups.set([ 'Todos los clientes', 'Clientes morosos', 'Clientes al día' ]);
      }
    });
  }

  loadMailServers(): void {
    const servers: MailServer[] = [
      { id: 'default', name: 'Servidor Principal (Default)', description: 'Límite de 500 envíos/hora' },
      { id: 'backup-01', name: 'Servidor Secundario (Backup)', description: 'Límite de 1000 envíos/hora' }
    ];
    this.mailServers.set(servers);
    this.selectedMailServer.set(servers[0]?.id || '');
  }

  setupSearchDebounce(): void {
    this.searchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(query => {
        if (query.length < 2) return of([]);
        return this.clientService.searchClients(query).pipe(catchError(() => of([])));
      })
    ).subscribe(results => this.searchResults.set(results));
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
    type: 'programar'
  });
  
  currentModal = signal<'add' | 'edit' | 'view' | 'delete-confirm' | 'send-confirm' | 'retry-confirm' | 'none'>('none');
  selectedCommunication = signal<ComunicacionDto | null>(null);

  toast = signal<ToastState>({
    show: false,
    message: '',
    description: '',
    icon: '',
    color: 'success',
  });

  channels = COMMUNICATION_CHANNELS;
  
  // --- Computed Signals ---
  
  scheduledCommunications = computed(() => 
    this.communications().filter(c => c.status === 'Scheduled' || c.status === 'Processing')
  );

  draftCommunications = computed(() => 
    this.communications().filter(c => c.status === 'Draft')
  );

  pastCommunications = computed(() => 
    this.communications().filter(c => c.status === 'Finished')
  );

  failedCommunications = computed(() =>
    this.communications().filter(c => c.status === 'Finished w/ Errors' || c.status === 'Failed')
  );

  isFormValid = computed(() => {
    const data = this.formData();
    const isContentEmpty = !data.content || data.content.trim() === '<p><br></p>' || data.content.trim() === '';
    let baseValid = data.title.trim().length > 0 && 
                    !isContentEmpty && 
                    data.channels.length > 0 && 
                    data.recipients.length > 0;
    
    if (data.type === 'programar') {
      return baseValid && data.sendDate.length > 0 && data.sendTime.length > 0;
    }
    return baseValid;
  });

  // --- Methods ---

  private resetForm(): void {
    this.formData.set({
      id: null,
      title: '',
      content: '',
      sendDate: '',
      sendTime: '',
      channels: [],
      recipients: [],
      type: 'programar'
    });
    this.attachments.set([]); 
    this.existingAttachments.set([]); // Limpia también los existentes
  }

  private showToast(message: string, description: string, icon: string, color: 'success' | 'error'): void {
    this.toast.set({ show: true, message, description, icon, color });
    setTimeout(() => this.toast.set({ ...this.toast(), show: false }), 4000);
  }

  openModal(
    modalType: 'add' | 'edit' | 'view' | 'delete-confirm' | 'send-confirm' | 'retry-confirm', 
    communication: ComunicacionDto | null = null, 
    isResend: boolean = false
  ): void {
    this.selectedCommunication.set(communication);
    this.resetForm();

    let finalModalType = modalType;

    if (communication && (modalType === 'edit' || isResend)) {
      let channelsArray: ('Email' | 'WhatsApp')[] = [];
      if (communication.channel.includes('Email')) channelsArray.push('Email');
      if (communication.channel.includes('WhatsApp')) channelsArray.push('WhatsApp');

      const formType = (communication.status === 'Scheduled' || communication.status === 'Processing') ? 'programar' : 'borrador';

      this.formData.set({
        id: modalType === 'edit' ? communication.id : null,
        title: communication.title,
        content: communication.content,
        sendDate: isResend ? '' : (communication.sendDate || ''),
        sendTime: isResend ? '' : (communication.sendTime || ''),
        channels: channelsArray,
        recipients: [...communication.recipients],
        type: isResend ? 'programar' : formType
      });
      
      // --- NUEVO: Cargar los adjuntos existentes (si no es 'reenviar') ---
      if (modalType === 'edit' && communication.attachments) {
        this.existingAttachments.set(communication.attachments);
      }
      
      if (isResend) finalModalType = 'add';
    }
    
    this.currentModal.set(finalModalType);
  }

  closeModal(): void {
    this.currentModal.set('none');
    this.selectedCommunication.set(null);
    this.resetForm();
  }

  // --- NUEVO: Método unificado para construir el FormData ---
  private buildCommunicationFormData(): FormData {
    const data = this.formData();
    const requestDto: UpsertComunicacionRequest = {
      id: data.id, // El backend debe manejar esto (ignorar en POST, usar en PUT)
      title: data.title,
      content: data.content,
      sendDate: data.type === 'programar' ? data.sendDate : null,
      sendTime: data.type === 'programar' ? data.sendTime : null,
      channels: data.channels,
      recipients: data.recipients,
      type: data.type === 'programar' ? 'schedule' : 'draft',
      // Mapea los adjuntos existentes que quedan para que el backend sepa cuáles NO borrar
      attachmentsToRemove: [] // Lógica de 'removeExistingAttachment' debe llenar esto
    };

    const httpFormData = new FormData();
    // Adjunta el DTO como JSON
    httpFormData.append('comunicadoDto', JSON.stringify(requestDto));

    // Adjunta solo los archivos NUEVOS
    for (const file of this.attachments()) {
      httpFormData.append('files', file, file.name);
    }
    return httpFormData;
  }

  addCommunication(): void {
    if (!this.isFormValid() || this.isSubmitting()) { return; }
    this.isSubmitting.set(true);

    const httpFormData = this.buildCommunicationFormData();

    this.commService.createCommunication(httpFormData).pipe(
      finalize(() => this.isSubmitting.set(false))
    ).subscribe({
      next: (newCommunication) => {
        this.communications.update(comms => [newCommunication, ...comms]);
        this.closeModal();
        this.showToast('¡Comunicado creado!', this.formData().type === 'programar' ? `Se programó el envío` : 'Guardado como borrador', '📨', 'success');
      },
      error: (err) => this.showToast('Error', 'No se pudo crear el comunicado', '❌', 'error')
    });
  }

  editCommunication(): void {
    const commId = this.formData().id;
    if (!commId || !this.isFormValid() || this.isSubmitting()) { return; }
    this.isSubmitting.set(true);

    const httpFormData = this.buildCommunicationFormData();

    this.commService.updateCommunication(commId, httpFormData).pipe(
      finalize(() => this.isSubmitting.set(false))
    ).subscribe({
      next: (updatedComm) => {
        this.communications.update(comms => comms.map(c => c.id === commId ? updatedComm : c));
        this.closeModal();
        this.showToast('¡Comunicado actualizado!', 'Los cambios se guardaron', '✏️', 'success');
      },
      error: (err) => this.showToast('Error', 'No se pudo actualizar', '❌', 'error')
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

  // --- ACTUALIZADO: handleSendCommunication (Ahora es simple) ---
  handleSendCommunication(communicationId: number): void {
    // Ya no se envían adjuntos aquí. Solo se da la orden de enviar.
    this.isSubmitting.set(true);
    this.commService.sendDraftNow(communicationId).pipe(
      finalize(() => this.isSubmitting.set(false))
    ).subscribe({
      next: (sentComm) => {
        this.communications.update(comms => comms.map(c => c.id === communicationId ? sentComm : c));
        this.closeModal();
        this.showToast('¡Comunicado enviado!', 'El envío se ha puesto en cola', '✅', 'success');
      },
      error: (err) => this.showToast('Error', 'No se pudo enviar', '❌', 'error')
    });
  }

  handleRetryFailedSends(): void {
    const commId = this.selectedCommunication()?.id;
    if (!commId || this.isSubmitting()) return;

    const serverId = this.selectedMailServer();
    if (!serverId) {
      this.showToast('Error', 'Debes seleccionar un servidor de correo', '❌', 'error');
      return;
    }
    this.isSubmitting.set(true);
    
    this.commService.retryFailedSends(commId, serverId).pipe(
      finalize(() => this.isSubmitting.set(false))
    ).subscribe({
      next: (updatedComm) => {
        this.communications.update(comms => comms.map(c => c.id === commId ? updatedComm : c));
        this.closeModal();
        this.showToast('Reenvío iniciado', `Se reintentará el envío a los fallidos/pendientes.`, '📨', 'success');
      },
      error: (err) => this.showToast('Error', 'No se pudo iniciar el reenvío', '❌', 'error')
    });
  }

  toggleChannel(channelName: 'Email' | 'WhatsApp'): void {
    const currentChannels = this.formData().channels;
    const newChannels = currentChannels.includes(channelName)
      ? currentChannels.filter(c => c !== channelName)
      : [...currentChannels, channelName];
    this.formData.update(data => ({ ...data, channels: newChannels }));

    // Si quitan "Email", limpiar adjuntos
    if (!newChannels.includes('Email')) {
      this.attachments.set([]);
      this.existingAttachments.set([]);
    }
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
  
  // --- Métodos para manejar adjuntos ---
  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files) return;
    const newFiles = Array.from(input.files);
    this.attachments.update(currentFiles => [...currentFiles, ...newFiles]);
    input.value = '';
  }

  // Quita un archivo NUEVO (de la lista 'attachments')
  removeNewAttachment(fileName: string): void {
    this.attachments.update(currentFiles => 
      currentFiles.filter(file => file.name !== fileName)
    );
  }

  // Quita un archivo EXISTENTE (de la lista 'existingAttachments')
  removeExistingAttachment(fileName: string): void {
    const commId = this.formData().id;
    if (!commId) {
      // Si el comunicado aún no se crea, solo quítalo de la UI
      this.existingAttachments.update(files => files.filter(f => f.fileName !== fileName));
      return;
    }

    // Si el comunicado ya existe, llama a la API para borrarlo
    this.commService.deleteAttachment(commId, fileName).subscribe({
      next: () => {
        this.existingAttachments.update(files => files.filter(f => f.fileName !== fileName));
        this.showToast('Adjunto eliminado', `${fileName} ha sido eliminado.`, '🗑️', 'success');
      },
      error: () => {
        this.showToast('Error', `No se pudo eliminar ${fileName}`, '❌', 'error');
      }
    });
  }


  formatFileSize(bytes: number, decimals = 2): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const dm = decimals < 0 ? 0 : decimals;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
  }

  // --- Template Helpers ---

  getBadgeMeta(status: ComunicacionDto['status']): { text: string; classes: string; icon?: string } {
    let colorClass = '', icon: string | undefined, text: string;
    switch (status) {
      case 'Finished': colorClass = 'bg-green-100 text-green-800'; icon = 'check-circle'; text = 'Enviado'; break;
      case 'Scheduled': colorClass = 'bg-blue-100 text-blue-800'; icon = 'clock'; text = 'Programado'; break;
      case 'Draft': colorClass = 'bg-gray-100 text-gray-800'; icon = 'file-text'; text = 'Borrador'; break;
      case 'Processing': colorClass = 'bg-yellow-100 text-yellow-800'; icon = 'refresh-cw'; text = 'Procesando'; break;
      case 'Failed': case 'Finished w/ Errors':
        colorClass = 'bg-red-100 text-red-800'; icon = 'alert-triangle'; text = 'Error'; break;
      default: colorClass = 'bg-gray-100 text-gray-800'; text = status;
    }
    return { text, classes: `inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${colorClass} min-w-[70px] justify-center`, icon };
  }

  getChannelMeta(channel: string): { icons: { name: string; classes?: string }[] } {
    const mail = { name: 'Mail', classes: 'text-blue-600' };
    const wa = { name: 'MessageSquare', classes: 'text-green-600' };
    if (channel === 'Email + WhatsApp') return { icons: [mail, wa] };
    if (channel === 'Email') return { icons: [mail] };
    if (channel === 'WhatsApp') return { icons: [wa] };
    return { icons: [] };
  }

  getCommunicationPreview(content: string, channel: string): string {
    const plainText = content?.replace(/<[^>]*>/g, ' ').replace(/\s+/g, ' ').trim() || ''; 
    const maxLength = channel.includes('WhatsApp') ? 50 : 80;
    let preview = plainText.length > maxLength ? plainText.substring(0, maxLength) + '...' : plainText;
    if (!preview) return "(Sin contenido)";
    if (channel.includes('WhatsApp')) return `📱 ${preview}`;
    return preview;
  }

  updateFormField<K extends keyof FormDataState>(field: K, value: FormDataState[K]) {
    this.formData.update(currentData => ({ ...currentData, [field]: value }));
  }

  getSanitizedHtmlContent(): SafeHtml {
    const html = this.selectedCommunication()?.content || '';
    return this.sanitizer.bypassSecurityTrustHtml(html);
  }
}