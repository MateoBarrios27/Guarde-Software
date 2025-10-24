import { Component, signal, computed, ChangeDetectionStrategy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent } from "../../shared/components/icon/icon.component";
import { CommunicationService } from '../../core/services/communication-service/communication.service';
// Your DTOs are the source of truth
import { ComunicacionDto, UpsertComunicacionRequest } from '../../core/dtos/communications/communicationDto';
import { ClientService } from '../../core/services/client-service/client.service';
import { catchError, debounceTime, distinctUntilChanged, of, Subject, switchMap } from 'rxjs';

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
  content: string;
  sendDate: string;
  sendTime: string;
  channels: ('Email' | 'WhatsApp')[];
  recipients: string[];
  type: 'programar' | 'borrador'; // This is for the form's radio button (Spanish UI)
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
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './communications.component.html',
  styleUrl: './communications.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CommunicationsComponent implements OnInit {

  // --- Signals (State Management) ---

  // This signal now holds the DTOs from the API
  communications = signal<ComunicacionDto[]>([]); 

  //Client search signals

  staticGroups = signal<string[]>([]); 
  // Hold the get results
  searchResults = signal<string[]>([]);
  // Shows or hidden the search
  isSearchFocused = signal(false);

  // --- Subject de RxJS para manejar el "debounce" ---
  private searchSubject = new Subject<string>();
  
  constructor(private commService: CommunicationService, private clientService: ClientService) {}

  ngOnInit(): void {
    this.loadCommunications();
    this.loadRecipientOptions();
    this.setupSearchDebounce();  
  }

  loadCommunications(): void {
    // Assumes your service method is 'getComunicaciones'
    this.commService.getCommunications().subscribe({ 
      next: (data) => {
        this.communications.set(data);
        console.log('Communications loaded:', data);
      },
      error: (err) => this.showToast('Error de Carga', 'No se pudieron cargar los datos', '❌', 'error')
    });
  }

  loadRecipientOptions(): void {
    this.clientService.getRecipientOptions().subscribe({
      next: (data) => {
        // Solo cargamos los grupos estáticos aquí
        // Filtramos los nombres de clientes que venían antes
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
        this.showToast('Error', 'No se pudieron cargar los grupos de destinatarios', '❌', 'error');
      }
    });
  }

  setupSearchDebounce(): void {
    this.searchSubject.pipe(
      debounceTime(300), // Espera 300ms después de que dejas de teclear
      distinctUntilChanged(), // Solo busca si el texto cambió
      switchMap(query => { // Cancela búsquedas anteriores y hace una nueva
        if (query.length < 2) {
          return of([]); // Devuelve vacío si es muy corto
        }
        return this.clientService.searchClients(query).pipe(
          catchError(() => of([])) // Si la API falla, devuelve vacío
        );
      })
    ).subscribe(results => {
      this.searchResults.set(results);
    });
  }

  // --- NUEVO MÉTODO ---
  // Se llama en CADA tecla presionada en el input
  onSearchInput(event: Event): void {
    const query = (event.target as HTMLInputElement).value;
    this.searchSubject.next(query);
  }
  
  // This signal is for the modal form
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
  
  currentModal = signal<'add' | 'edit' | 'view' | 'delete-confirm' | 'send-confirm' | 'none'>('none');
  
  // ✅ CORRECTION: This signal must hold the DTO, not the old interface
  selectedCommunication = signal<ComunicacionDto | null>(null);

  toast = signal<ToastState>({
    show: false,
    message: '',
    description: '',
    icon: '',
    color: 'success',
  });

  // --- Read-only data properties ---
  channels = COMMUNICATION_CHANNELS;
  recipientOptions = signal<string[]>([]);
  
  // --- Computed Signals for Filtering (Derived State) ---
  
  // ✅ CORRECTION: Filter using the ENGLISH status names from the API
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

  // This is valid, it checks the *form's* state
  isFormValid = computed(() => {
    const data = this.formData();
    let baseValid = data.title.trim().length > 0 && data.content.trim().length > 0 && data.channels.length > 0 && data.recipients.length > 0;
    
    if (data.type === 'programar') {
      return baseValid && data.sendDate.length > 0 && data.sendTime.length > 0;
    }
    
    return baseValid;
  });

  // --- Methods (Event Handlers and Logic) ---

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
  }

  private showToast(message: string, description: string, icon: string, color: 'success' | 'error'): void {
    this.toast.set({ show: true, message, description, icon, color });
    setTimeout(() => this.toast.set({ ...this.toast(), show: false }), 4000);
  }

  // ✅ CORRECTION: Parameter 'communication' is now the DTO
  openModal(modalType: 'add' | 'edit' | 'view' | 'delete-confirm' | 'send-confirm', communication: ComunicacionDto | null = null, isResend: boolean = false): void {
    this.selectedCommunication.set(communication);
    this.resetForm();

    let finalModalType = modalType;

    if (communication && (modalType === 'edit' || isResend)) {
      // Convert channel string (e.g., "Email + WhatsApp") back to an array
      let channelsArray: ('Email' | 'WhatsApp')[] = [];
      if (communication.channel.includes('Email')) {
        channelsArray.push('Email');
      }
      if (communication.channel.includes('WhatsApp')) {
        channelsArray.push('WhatsApp');
      }

      // Translate backend status (e.g., 'Scheduled') to form status ('programar')
      const formType = (communication.status === 'Scheduled' || communication.status === 'Processing') 
                       ? 'programar' 
                       : 'borrador';

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
      
      if (isResend) {
        finalModalType = 'add'; // If it's a resend, force the modal to 'add'
      }

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

    // Translate form type ('programar') to API type ('schedule')
    const request: UpsertComunicacionRequest = {
      id: null,
      title: data.title,
      content: data.content,
      sendDate: data.type === 'programar' ? data.sendDate : null,
      sendTime: data.type === 'programar' ? data.sendTime : null,
      channels: data.channels,
      recipients: data.recipients,
      type: data.type === 'programar' ? 'schedule' : 'draft' // Translate
    };

    this.commService.createCommunication(request).subscribe({
      next: (newCommunication) => {
        // Add the new communication (which has an English status) to the signal
        this.communications.update(comms => [newCommunication, ...comms]);
        this.closeModal();
        this.showToast(
          '¡Comunicado creado!', // UI in Spanish
          data.type === 'programar' ? `Se programó el envío` : 'Guardado como borrador', // UI in Spanish
          '📨', 'success'
        );
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
      content: data.content,
      sendDate: data.type === 'programar' ? data.sendDate : null,
      sendTime: data.type === 'programar' ? data.sendTime : null,
      channels: data.channels,
      recipients: data.recipients,
      type: data.type === 'programar' ? 'schedule' : 'draft' // Translate
    };

    this.commService.updateCommunication(commId, request).subscribe({
      next: (updatedComm) => {
        this.communications.update(comms => comms.map(c =>
          c.id === commId ? updatedComm : c
        ));
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
    // Assumes your service has a 'sendDraftNow' method
    this.commService.sendDraftNow(communicationId).subscribe({
      next: (sentComm) => {
        // API returns the updated communication, now set to 'Scheduled' or 'Processing'
        this.communications.update(comms => comms.map(c =>
          c.id === communicationId ? sentComm : c
        ));
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
    
    // Limpia el input y los resultados
    inputElement.value = ''; 
    this.searchResults.set([]);
    this.isSearchFocused.set(false);
  }

  removeRecipient(recipient: string): void {
    this.formData.update(data => ({ ...data, recipients: data.recipients.filter(d => d !== recipient) }));
  }
  
  // --- Template Helpers ---

  // ✅ CORRECTION: Parameter 'status' is now from the DTO
  getBadgeMeta(status: ComunicacionDto['status']): { text: string; classes: string; icon?: string } {
    let colorClass = '';
    let icon: string | undefined;
    let text: string; // User-facing text in Spanish

    switch (status) {
      case 'Finished': 
        colorClass = 'bg-green-100 text-green-800'; 
        icon = 'check-circle'; 
        text = 'Enviado'; // Spanish UI
        break;
      case 'Scheduled': 
        colorClass = 'bg-blue-100 text-blue-800'; 
        icon = 'clock'; 
        text = 'Programado'; // Spanish UI
        break;
      case 'Draft': 
        colorClass = 'bg-gray-100 text-gray-800'; 
        icon = 'file-text'; 
        text = 'Borrador'; // Spanish UI
        break;
      case 'Processing':
        colorClass = 'bg-yellow-100 text-yellow-800'; 
        icon = 'refresh-cw'; 
        text = 'Procesando'; // Spanish UI
        break;
      case 'Failed':
      case 'Finished w/ Errors':
        colorClass = 'bg-red-100 text-red-800'; 
        icon = 'alert-triangle'; 
        text = 'Error'; // Spanish UI
        break;
      default: 
        colorClass = 'bg-gray-100 text-gray-800';
        text = status; // Fallback
    }
    
    return { 
      text: text, 
      classes: `inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${colorClass} min-w-[70px] justify-center`, 
      icon 
    };
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
    const maxLength = channel.includes('WhatsApp') ? 50 : 80;
    let preview = content.length > maxLength ? content.substring(0, maxLength) + '...' : content;
    if (channel.includes('WhatsApp')) {
      return `📱 ${preview}`;
    }
    return preview;
  }

//  A generic helper to update a single field in the formData signal.
//  This is the correct way to update signals from form events.
updateFormField<K extends keyof FormDataState>(field: K, value: FormDataState[K]) {
  this.formData.update(currentData => ({
    ...currentData,
    [field]: value
  }));
}
}