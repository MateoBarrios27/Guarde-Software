import { Component, signal, computed, ChangeDetectionStrategy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent } from "../../shared/components/icon/icon.component";
import { CommunicationService } from '../../core/services/communication-service/communication.service';
import { ComunicacionDto, UpsertComunicacionRequest } from '../../core/dtos/communications/communicationDto';
import { ClientService } from '../../core/services/client-service/client.service';
import { catchError, debounceTime, distinctUntilChanged, of, Subject, switchMap } from 'rxjs';
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
export class CommunicationsComponent implements OnInit {

  communications = signal<ComunicacionDto[]>([]); 
  staticGroups = signal<string[]>([]); 
  searchResults = signal<string[]>([]);
  isSearchFocused = signal(false);
  private searchSubject = new Subject<string>();
  selectedFiles = signal<File[]>([]);
  smtpConfigs = signal<any[]>([]);

  showRecipientModal = signal(false);
  allClients = signal<ClientSelectorItem[]>([]); 
  filteredClients = signal<ClientSelectorItem[]>([]); 
  recipientSearchTerm = signal('');
  
  selectedCount = computed(() => this.formData().recipients.length);
  currentSort = signal<'name' | 'status'>('name');

  selectedSummary = computed(() => {
      const recipients = this.formData().recipients;
      const count = recipients.length;
      
      if (count === 0) return '';
      
      const names = recipients.slice(0, 2);
      return `${names.join(', ')} ${count > 2 ? `(+${count - 2} más)` : ''}`;
  });
  

  constructor(
    private commService: CommunicationService, 
    private clientService: ClientService,
    private sanitizer: DomSanitizer
  ) {}

  ngOnInit(): void {
    this.loadCommunications();
    this.loadRecipientOptions();
    this.setupSearchDebounce();  
    this.loadSmtpConfigs();
    this.loadClientsForSelector();
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
    content: '', 
    sendDate: '',
    sendTime: '',
    channels: [],
    recipients: [],
    type: 'enviar_ahora',
    smtpConfigId: null,
    isAccountStatement: false,
    isNextMonthStatement: false
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

  isFormValid = computed(() => {
    const data = this.formData();
    
    const isContentEmpty = !data.content || data.content.trim() === '<p><br></p>' || data.content.trim() === '';
    const contentIsValid = data.isAccountStatement || !isContentEmpty;

    let baseValid = data.title.trim().length > 0 && 
                    contentIsValid && 
                    data.channels.length > 0 && 
                    data.recipients.length > 0;
    
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
      isNextMonthStatement: false
    });
  }

  private showToast(message: string, description: string, icon: string, color: 'success' | 'error'): void {
    this.toast.set({ show: true, message, description, icon, color });
    setTimeout(() => this.toast.set({ ...this.toast(), show: false }), 4000);
  }

  openModal(
    modalType: 'add' | 'edit' | 'view' | 'delete-confirm' | 'send-confirm', 
    communication: ComunicacionDto | null = null, 
    isResend: boolean = false // Este flag ahora servirá para "Clonar"
  ): void {
    this.selectedCommunication.set(communication);
    this.resetForm();

    let finalModalType = modalType;

    if (communication && (modalType === 'edit' || isResend)) {
      // 1. Recuperar canales
      let channelsArray: ('Email' | 'WhatsApp')[] = [];
      if (communication.channel.includes('Email')) channelsArray.push('Email');
      if (communication.channel.includes('WhatsApp')) channelsArray.push('WhatsApp');

      // 2. Determinar tipo de formulario
      let formType: 'programar' | 'borrador' | 'enviar_ahora' = 'borrador';
      
      // Si es un comunicado que ya estaba programado, mantenemos su estado
      if (communication.status === 'Scheduled' || communication.status === 'Processing') {
        formType = 'programar';
      } 
      // Si es un reintento por error, por defecto queremos que se envíe ahora
      else if (communication.status === 'Failed' || communication.status === 'Finished w/ Errors') {
        formType = 'enviar_ahora';
      } 
      // Si estamos editando un borrador, lo mantenemos como borrador
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
        recipients: [...communication.recipients],
        
        // <--- CAMBIO AQUÍ: Si clonamos (isResend), forzamos 'enviar_ahora'. Si no, usamos la lógica de arriba.
        type: isResend ? 'enviar_ahora' : formType,
        
        smtpConfigId: communication.smtpConfigId || null,
        isAccountStatement: communication.isAccountStatement || false,
        isNextMonthStatement: communication.isNextMonthStatement || false
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

      // Asegurar formato HH:mm usando la zona horaria local
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
      // Asegurar formato YYYY-MM-DD usando la zona horaria local
      const year = now.getFullYear();
      const month = String(now.getMonth() + 1).padStart(2, '0');
      const day = String(now.getDate()).padStart(2, '0');
      finalSendDate = `${year}-${month}-${day}`; 

      // Asegurar formato HH:mm usando la zona horaria local
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
      isNextMonthStatement: data.isNextMonthStatement
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
    const currentChannels = this.formData().channels;
    const isAdding = !currentChannels.includes(channelName);
    
    const newChannels = isAdding
      ? [...currentChannels, channelName]
      : currentChannels.filter(c => c !== channelName);

    let newIsAccountStatement = this.formData().isAccountStatement;
    
    if (channelName === 'WhatsApp' && isAdding) {
      newIsAccountStatement = false;
    }

    this.formData.update(data => ({ 
      ...data, 
      channels: newChannels,
      isAccountStatement: newIsAccountStatement
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

  getCommunicationPreview(content: string, channel: string): string {
  if (!content) return '';

  const tempDiv = document.createElement('div');
  tempDiv.innerHTML = content;
  const text = tempDiv.textContent || tempDiv.innerText || '';

  return text.length > 150 ? text.substring(0, 150) + '...' : text;
}

  updateFormField<K extends keyof FormDataState>(field: K, value: FormDataState[K]) {
    this.formData.update(currentData => ({
      ...currentData,
      [field]: value
    }));
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
    this.openModal('edit', comm);
    this.showToast('Modo Reintento', 'Edita la configuración y guarda para reintentar a los fallidos.', 'edit', 'success');
  }

  // --- new methods for client selector ---
  loadClientsForSelector(): void {
    this.commService.getClientsForSelector().subscribe({
        next: (data: any[]) => {
            const mapped = data.map(c => {
                let status: 'Moroso' | 'Pendiente' | 'AlDia' = 'AlDia';
                
                if (c.maxUnpaidMonths > 0) {
                    status = 'Moroso';
                } else if (c.balance > 0 && c.maxUnpaidMonths === 0) {
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
                    status: status,
                    selected: false
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
    
    if (currentRecipients.length > 0) {
        this.allClients.update(list => list.map(c => ({
            ...c,
            selected: currentRecipients.includes(c.fullName)
        })));
    }
    
    this.filterList();
    this.showRecipientModal.set(true);
  }

  applyFilter(type: 'Todos' | 'Morosos' | 'Pendientes' | 'AlDia' | 'Ninguno'): void {
      this.allClients.update(list => list.map(c => {
          let shouldSelect = c.selected; 
          
          switch(type) {
              case 'Todos': 
                  shouldSelect = true; 
                  break;
              case 'Ninguno': 
                  shouldSelect = false; 
                  break;
              case 'Morosos': 
                  if (c.status === 'Moroso') shouldSelect = true; 
                  break;
              case 'Pendientes': 
                  if (c.status === 'Pendiente') shouldSelect = true; 
                  break;
              case 'AlDia': 
                  if (c.status === 'AlDia') shouldSelect = true; 
                  break;
          }
          
          return { ...c, selected: shouldSelect };
      }));

      this.filterList(); 
  }

  toggleSort(): void {
      this.currentSort.update(current => current === 'name' ? 'status' : 'name');
      this.filterList(); 
  }

  

  onSearch(term: string): void {
      this.recipientSearchTerm.set(term);
      this.filterList();
  }

  filterList(): void {
      const term = this.recipientSearchTerm().toLowerCase();
      let list = this.allClients();
      
      if (term) {
          list = list.filter(c => 
              c.fullName.toLowerCase().includes(term) || 
              (c.email && c.email.toLowerCase().includes(term))
          );
      }

      const sortType = this.currentSort();
      
      list.sort((a, b) => {
          if (sortType === 'status') {
              const priority: Record<string, number> = { 'Moroso': 1, 'Pendiente': 2, 'AlDia': 3 };
              
              const pA = priority[a.status] || 99;
              const pB = priority[b.status] || 99;
              
              if (pA !== pB) return pA - pB;
          }

          return a.fullName.localeCompare(b.fullName);
      });

      this.filteredClients.set([...list]);
  }

  toggleSelection(id: number): void {
      this.allClients.update(list => list.map(c => 
          c.id === id ? { ...c, selected: !c.selected } : c
      ));
      this.filterList();
  }

  confirmSelection(): void {
      const selectedNames = this.allClients()
          .filter(c => c.selected)
          .map(c => c.fullName);
      
      this.updateFormField('recipients', selectedNames);
      this.showRecipientModal.set(false);
  }
}