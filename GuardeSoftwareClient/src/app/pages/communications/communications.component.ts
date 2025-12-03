import { Component, signal, computed, ChangeDetectionStrategy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent } from "../../shared/components/icon/icon.component";
import { CommunicationService } from '../../core/services/communication-service/communication.service';
import { ComunicacionDto, UpsertComunicacionRequest } from '../../core/dtos/communications/communicationDto';
import { ClientService } from '../../core/services/client-service/client.service';
import { catchError, debounceTime, distinctUntilChanged, of, Subject, switchMap } from 'rxjs';

// --- NUEVAS IMPORTACIONES ---
import { QuillModule } from 'ngx-quill';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

// --- Type Definitions (English Code) ---

/** Represents a single communication channel */
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
  content: string; // This content will now be HTML
  sendDate: string;
  sendTime: string;
  channels: ('Email' | 'WhatsApp')[];
  recipients: string[];
  type: 'programar' | 'borrador'; // This is for the form's radio button (Spanish UI)
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

const COMMUNICATION_CHANNELS: Channel[] = [
  { id: 1, name: 'Email', spanishLabel: 'Email', icon: 'Mail' },
  { id: 2, name: 'WhatsApp', spanishLabel: 'WhatsApp', icon: 'MessageSquare' }
];

@Component({
  selector: 'communications',
  standalone: true,
  // --- IMPORTANTE: Añadir QuillModule aquí ---
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
  selectedFiles = signal<File[]>([]);
  smtpConfigs = signal<any[]>([]);
  
  // --- Inyectar DomSanitizer ---
  constructor(
    private commService: CommunicationService, 
    private clientService: ClientService,
    private sanitizer: DomSanitizer // Necesario para el [innerHTML]
  ) {}

  ngOnInit(): void {
    this.loadCommunications();
    this.loadRecipientOptions();
    this.setupSearchDebounce();  
    this.loadSmtpConfigs();
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
    content: '', // Este será el HTML de Quill
    sendDate: '',
    sendTime: '',
    channels: [],
    recipients: [],
    type: 'programar',
    smtpConfigId: null
  });
  
  currentModal = signal<'add' | 'edit' | 'view' | 'delete-confirm' | 'send-confirm' | 'none'>('none');
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
    this.communications().filter(c => 
      c.status === 'Finished' || 
      c.status === 'Finished w/ Errors' ||
      c.status === 'Failed'
    )
  );

  // --- ACTUALIZADO: isFormValid (Revisa si Quill está vacío) ---
  isFormValid = computed(() => {
    const data = this.formData();
    
    // Quill vacío se guarda como "<p><br></p>" o null/undefined
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
      type: 'programar',
      smtpConfigId: null
    });
  }

  private showToast(message: string, description: string, icon: string, color: 'success' | 'error'): void {
    this.toast.set({ show: true, message, description, icon, color });
    setTimeout(() => this.toast.set({ ...this.toast(), show: false }), 4000);
  }

  openModal(modalType: 'add' | 'edit' | 'view' | 'delete-confirm' | 'send-confirm', communication: ComunicacionDto | null = null, isResend: boolean = false): void {
    this.selectedCommunication.set(communication);
    this.resetForm();

    let finalModalType = modalType;

    if (communication && (modalType === 'edit' || isResend)) {
      let channelsArray: ('Email' | 'WhatsApp')[] = [];
      if (communication.channel.includes('Email')) channelsArray.push('Email');
      if (communication.channel.includes('WhatsApp')) channelsArray.push('WhatsApp');

      const formType = (communication.status === 'Scheduled' || communication.status === 'Processing') 
        ? 'programar' 
        : 'borrador';

      this.formData.set({
        id: modalType === 'edit' ? communication.id : null,
        title: communication.title,
        content: communication.content, // El contenido ya es HTML
        sendDate: isResend ? '' : (communication.sendDate || ''),
        sendTime: isResend ? '' : (communication.sendTime || ''),
        channels: channelsArray,
        recipients: [...communication.recipients],
        type: isResend ? 'programar' : formType,
        smtpConfigId: communication.smtpConfigId || null
      });
      
      if (isResend) finalModalType = 'add';
    }
    
    this.currentModal.set(finalModalType);
  }

  closeModal(): void {
    this.currentModal.set('none');
    this.selectedCommunication.set(null);
    this.resetForm();
  }

  addCommunication(): void {
    const data = this.formData();
    if (!this.isFormValid()) { return; }

    // Aquí llamamos al servicio pasando Data + Archivos
    this.commService.createCommunication(data, this.selectedFiles()).subscribe({
      next: (newCommunication) => {
        this.communications.update(comms => [newCommunication, ...comms]);
        this.closeModal();
        this.showToast('¡Comunicado creado!', 'Se guardó correctamente', '📨', 'success');
        this.selectedFiles.set([]); // Limpiar archivos tras éxito
      },
      error: (err) => this.showToast('Error', 'No se pudo crear el comunicado', '❌', 'error')
    });
  }

  editCommunication(): void {
    const data = this.formData();
    const commId = data.id;
    if (!commId || !this.isFormValid()) { return; }

    const request: UpsertComunicacionRequest = {
      id: commId,
      title: data.title,
      content: data.content, // Se envía el HTML
      sendDate: data.type === 'programar' ? data.sendDate : null,
      sendTime: data.type === 'programar' ? data.sendTime : null,
      channels: data.channels,
      recipients: data.recipients,
      type: data.type === 'programar' ? 'schedule' : 'draft'
    };

    this.commService.updateCommunication(commId, request).subscribe({
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
    const currentChannels = this.formData().channels;
    const newChannels = currentChannels.includes(channelName)
      ? currentChannels.filter(c => c !== channelName)
      : [...currentChannels, channelName];
    this.formData.update(data => ({ ...data, channels: newChannels }));
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
  
  // --- Template Helpers ---

  getBadgeMeta(status: ComunicacionDto['status']): { text: string; classes: string; icon?: string } {
    let colorClass = '';
    let icon: string | undefined;
    let text: string;

    switch (status) {
      case 'Finished': 
        colorClass = 'bg-green-100 text-green-800'; icon = 'check-circle'; text = 'Enviado';
        break;
      case 'Scheduled': 
        colorClass = 'bg-blue-100 text-blue-800'; icon = 'clock'; text = 'Programado';
        break;
      case 'Draft': 
        colorClass = 'bg-gray-100 text-gray-800'; icon = 'file-text'; text = 'Borrador';
        break;
      case 'Processing':
        colorClass = 'bg-yellow-100 text-yellow-800'; icon = 'refresh-cw'; text = 'Procesando';
        break;
      case 'Failed':
      case 'Finished w/ Errors':
        colorClass = 'bg-red-100 text-red-800'; icon = 'alert-triangle'; text = 'Error';
        break;
      default: 
        colorClass = 'bg-gray-100 text-gray-800'; text = status;
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

  // --- ACTUALIZADO: getCommunicationPreview (Limpia el HTML) ---
  getCommunicationPreview(content: string, channel: string): string {
    // Quita las etiquetas HTML para el preview de la card
    const plainText = content.replace(/<[^>]*>/g, ' ').replace(/\s+/g, ' ').trim(); 
    const maxLength = channel.includes('WhatsApp') ? 50 : 80;
    let preview = plainText.length > maxLength ? plainText.substring(0, maxLength) + '...' : plainText;
    
    if (!preview) return "(Sin contenido)";

    if (channel.includes('WhatsApp')) {
      return `📱 ${preview}`;
    }
    return preview;
  }

  // Helper genérico para actualizar el signal del formulario
  updateFormField<K extends keyof FormDataState>(field: K, value: FormDataState[K]) {
    this.formData.update(currentData => ({
      ...currentData,
      [field]: value
    }));
  }

  // --- NUEVO: Método para sanitizar el HTML en la vista "Ver Detalles" ---
  getSanitizedHtmlContent(): SafeHtml {
    const html = this.selectedCommunication()?.content || '';
    // Confía en el HTML que viene de la base de datos (que fue generado por Quill)
    return this.sanitizer.bypassSecurityTrustHtml(html);
  }

  loadSmtpConfigs(): void {
    this.commService.getAllSmtpConfigs().subscribe({
        next: (configs) => {
            this.smtpConfigs.set(configs);
            // Opcional: Si quieres pre-seleccionar el primero
            // if (configs.length > 0) this.updateFormField('smtpConfigId', configs[0].id);
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

  retryCommunication(comm: ComunicacionDto): void {
      if(!confirm('¿Reintentar envíos fallidos?')) return;
      
      this.commService.retryCommunication(comm.id).subscribe({
          next: (updated) => {
             this.communications.update(list => list.map(c => c.id === updated.id ? updated : c));
             this.showToast('Procesando', 'Reintento iniciado', 'refresh-cw', 'success');
          },
          error: () => this.showToast('Error', 'Falló el reintento', 'alert-triangle', 'error')
      });
  }
}