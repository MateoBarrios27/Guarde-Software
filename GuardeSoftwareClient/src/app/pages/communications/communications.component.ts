import { Component, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent } from "../../shared/components/icon/icon.component";

// --- Type Definitions (English Code) ---

/** Represents a single communication channel */
interface Channel {
  id: number;
  name: 'Email' | 'WhatsApp';
  spanishLabel: 'Email' | 'WhatsApp';
  icon: string; // Lucide icon name
}

/** Represents a single communication record */
interface Communication {
  id: number;
  title: string;
  content: string;
  sendDate: string; // Date string (YYYY-MM-DD) or empty
  sendTime: string; // Time string (HH:MM) or empty
  channel: string; // 'Email', 'WhatsApp', or 'Email + WhatsApp'
  recipients: string[];
  status: 'Enviado' | 'Programado' | 'Borrador'; // 'Sent', 'Scheduled', 'Draft'
  creationDate: string;
}

/** State for the Add/Edit form */
interface FormDataState {
  id: number | null; // Used for editing
  title: string;
  content: string;
  sendDate: string;
  sendTime: string;
  channels: ('Email' | 'WhatsApp')[];
  recipients: string[];
  type: 'programar' | 'borrador'; // 'schedule' or 'draft'
}

/** State for the notification toast */
interface ToastState {
  show: boolean;
  message: string;
  description: string;
  icon: string;
  color: 'success' | 'error';
}

// --- Mock Data (English Code, Spanish Content) ---

const MOCK_COMMUNICATIONS: Communication[] = [
  {
    id: 1,
    title: 'Aumento de tarifas 2024',
    content: 'Estimados clientes, les informamos que a partir del próximo mes, debido a ajustes de mercado, aplicaremos un pequeño aumento. ¡Gracias por su comprensión!',
    sendDate: '2024-09-15',
    sendTime: '10:00',
    channel: 'Email',
    recipients: ['Todos los clientes'],
    status: 'Programado',
    creationDate: '2024-08-12'
  },
  {
    id: 2,
    title: 'Mantenimiento programado',
    content: 'Les informamos que el día sábado 20/09 de 09:00 a 11:00 realizaremos tareas de mantenimiento en el sistema.',
    sendDate: '2024-09-18',
    sendTime: '09:00',
    channel: 'WhatsApp',
    recipients: ['Clientes con deuda'],
    status: 'Programado',
    creationDate: '2024-08-10'
  },
  {
    id: 3,
    title: 'Recordatorio de pago',
    content: 'Su pago está vencido. Por favor contacte a la administración para evitar suspensiones de servicio.',
    sendDate: '',
    sendTime: '',
    channel: 'Email',
    recipients: ['Clientes morosos'],
    status: 'Borrador',
    creationDate: '2024-08-11'
  },
  {
    id: 4,
    title: 'Bienvenida nuevos clientes',
    content: '¡Bienvenidos a GuardeSoftware! Sus datos de acceso y un tutorial de inicio rápido han sido enviados a su correo.',
    sendDate: '2024-08-01',
    sendTime: '14:30',
    channel: 'Email',
    recipients: ['Juan Pérez', 'María García'],
    status: 'Enviado',
    creationDate: '2024-07-30'
  },
  {
    id: 5,
    title: 'Actualización de horarios',
    content: 'Nuevos horarios de atención: Lunes a Viernes 8:00 a 18:00. ¡Esperamos verles!',
    sendDate: '2024-07-15',
    sendTime: '12:00',
    channel: 'WhatsApp',
    recipients: ['Todos los clientes'],
    status: 'Enviado',
    creationDate: '2024-07-14'
  }
];

const CUSTOMER_OPTIONS = [
  'Todos los clientes',
  'Clientes con deuda',
  'Clientes morosos',
  'Clientes al día',
  'Juan Pérez',
  'María García',
  'Carlos López',
  'Ana Martínez'
];

const COMMUNICATION_CHANNELS: Channel[] = [
  { id: 1, name: 'Email', spanishLabel: 'Email', icon: 'Mail' },
  { id: 2, name: 'WhatsApp', spanishLabel: 'WhatsApp', icon: 'MessageSquare' }
];

// --- Utility function to generate Lucide-like SVG icons inline ---
const ICON_SVGS: { [key: string]: string } = {

};

@Component({
  selector: 'communications',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './communications.component.html'
  ,
  styleUrl: './communications.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CommunicationsComponent {
  // --- Signals (State Management) ---
  
  // List of all communications
  communications = signal<Communication[]>(MOCK_COMMUNICATIONS);
  
  // Data for the Add/Edit form
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
  
  // Currently open modal ('add', 'edit', 'view', 'delete-confirm', 'send-confirm', 'none')
  currentModal = signal<'add' | 'edit' | 'view' | 'delete-confirm' | 'send-confirm' | 'none'>('none');
  
  // Communication selected for view, edit, or confirmation
  selectedCommunication = signal<Communication | null>(null);

  // Toast notification state
  toast = signal<ToastState>({
    show: false,
    message: '',
    description: '',
    icon: '',
    color: 'success',
  });

  // --- Read-only data properties ---
  channels = COMMUNICATION_CHANNELS;
  customerOptions = CUSTOMER_OPTIONS;
  
  // --- Computed Signals for Filtering (Derived State) ---
  
  scheduledCommunications = computed(() => 
    this.communications().filter(c => c.status === 'Programado')
  );

  draftCommunications = computed(() => 
    this.communications().filter(c => c.status === 'Borrador')
  );

  pastCommunications = computed(() => 
    this.communications().filter(c => c.status === 'Enviado')
  );

  // Checks if form inputs are valid for submission
  isFormValid = computed(() => {
    const data = this.formData();
    let baseValid = data.title.trim().length > 0 && data.content.trim().length > 0 && data.channels.length > 0 && data.recipients.length > 0;
    
    if (data.type === 'programar') {
      return baseValid && data.sendDate.length > 0 && data.sendTime.length > 0;
    }
    
    return baseValid;
  });

  // --- Methods (Event Handlers and Logic) ---

  /** Resets the form data to its initial state. */
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

  /**
   * Displays a toast notification.
   * @param message The main message.
   * @param description Secondary description.
   * @param icon Emoji icon.
   * @param color 'success' or 'error'.
   */
  private showToast(message: string, description: string, icon: string, color: 'success' | 'error'): void {
    this.toast.set({ show: true, message, description, icon, color });
    setTimeout(() => this.toast.set({ ...this.toast(), show: false }), 4000);
  }

  /**
   * Opens a specific modal, optionally pre-filling the form for edit/resend.
   * @param modalType Type of modal to open.
   * @param communication Optional communication object to load.
   * @param isResend If true, loads communication data into a new 'add' form.
   */
  openModal(modalType: 'add' | 'edit' | 'view' | 'delete-confirm' | 'send-confirm', communication: Communication | null = null, isResend: boolean = false): void {
    this.selectedCommunication.set(communication);
    this.resetForm();

    if (communication && (modalType === 'edit' || isResend)) {
      // Convert channel string back to array for the form
      let channelsArray: ('Email' | 'WhatsApp')[] = [];
      if (communication.channel.includes('Email')) channelsArray.push('Email');
      if (communication.channel.includes('WhatsApp')) channelsArray.push('WhatsApp');

      this.formData.set({
        id: modalType === 'edit' ? communication.id : null,
        title: communication.title,
        content: communication.content,
        sendDate: isResend ? '' : communication.sendDate,
        sendTime: isResend ? '' : communication.sendTime,
        channels: channelsArray,
        recipients: [...communication.recipients], // Copy recipients
        type: isResend ? 'programar' : (communication.status === 'Programado' ? 'programar' : 'borrador')
      });
      this.currentModal.set('add'); // Force 'add' modal for resend
    } else if (communication && (modalType === 'view' || modalType === 'delete-confirm' || modalType === 'send-confirm')) {
      // No form data needed, just the selected communication
    }
    
    this.currentModal.set(modalType);
  }

  /** Closes the currently open modal and resets related state. */
  closeModal(): void {
    this.currentModal.set('none');
    this.selectedCommunication.set(null);
    this.resetForm();
  }

  /** Adds a new communication (either scheduled or draft). */
  addCommunication(): void {
    const data = this.formData();
    
    if (!this.isFormValid()) {
      this.showToast('Campos incompletos', 'Por favor completa todos los campos requeridos', '❌', 'error');
      return;
    }

    const channelString = data.channels.length === 2 ? 'Email + WhatsApp' : data.channels[0];
    const newId = Math.max(0, ...this.communications().map(c => c.id)) + 1;
    
    const newCommunication: Communication = {
      id: newId,
      title: data.title,
      content: data.content,
      sendDate: data.type === 'programar' ? data.sendDate : '',
      sendTime: data.type === 'programar' ? data.sendTime : '',
      channel: channelString,
      recipients: data.recipients,
      status: data.type === 'programar' ? 'Programado' : 'Borrador',
      creationDate: new Date().toISOString().split('T')[0]
    };
    
    this.communications.update(comms => [newCommunication, ...comms]);
    this.closeModal();

    this.showToast(
      '¡Comunicado creado!',
      data.type === 'programar' ? `Se enviará por ${channelString} el ${data.sendDate}` : 'Guardado como borrador',
      '📨',
      'success'
    );
  }

  /** Edits an existing communication. */
  editCommunication(): void {
    const data = this.formData();
    const commId = data.id;

    if (!commId || !this.isFormValid()) {
      this.showToast('Error de edición', 'Verifica los campos antes de guardar', '❌', 'error');
      return;
    }

    const channelString = data.channels.length === 2 ? 'Email + WhatsApp' : data.channels[0];

    this.communications.update(comms => comms.map(c =>
      c.id === commId ? {
        ...c,
        title: data.title,
        content: data.content,
        sendDate: data.type === 'programar' ? data.sendDate : '',
        sendTime: data.type === 'programar' ? data.sendTime : '',
        channel: channelString,
        recipients: data.recipients,
        status: data.type === 'programar' ? 'Programado' : 'Borrador'
      } : c
    ));
    
    this.closeModal();
    this.showToast('¡Comunicado actualizado!', 'Los cambios se guardaron correctamente', '✏️', 'success');
  }

  /**
   * Deletes a communication after confirmation.
   * @param communicationId ID of the communication to delete.
   */
  handleDeleteCommunication(communicationId: number): void {
    this.communications.update(comms => comms.filter(c => c.id !== communicationId));
    this.closeModal();
    this.showToast('Comunicado eliminado', 'El comunicado ha sido eliminado correctamente', '🗑️', 'success');
  }

  /**
   * Sends a draft communication immediately.
   * @param communicationId ID of the communication to send.
   */
  handleSendCommunication(communicationId: number): void {
    const communication = this.communications().find(c => c.id === communicationId);
    
    if (!communication) return;

    const sentDate = new Date().toISOString().split('T')[0];
    const sentTime = new Date().toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' });

    this.communications.update(comms => comms.map(c =>
      c.id === communicationId ? {
        ...c,
        status: 'Enviado',
        sendDate: sentDate,
        sendTime: sentTime
      } : c
    ));
    
    this.closeModal();
    this.showToast(
      '¡Comunicado enviado!',
      `Enviado a ${communication.recipients.length} destinatario(s) por ${communication.channel}`,
      '✅',
      'success'
    );
  }

  /**
   * Toggles the selection of a channel in the form data.
   * @param channelName 'Email' or 'WhatsApp'.
   */
  toggleChannel(channelName: 'Email' | 'WhatsApp'): void {
    const currentChannels = this.formData().channels;
    const newChannels = currentChannels.includes(channelName)
      ? currentChannels.filter(c => c !== channelName)
      : [...currentChannels, channelName];
    
    this.formData.update(data => ({ ...data, channels: newChannels }));
  }

  /**
   * Adds a recipient to the form data from the select element.
   * @param event The change event from the select element.
   */
  addRecipient(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    if (value && !this.formData().recipients.includes(value)) {
      this.formData.update(data => ({ ...data, recipients: [...data.recipients, value] }));
    }
    (event.target as HTMLSelectElement).value = ''; // Reset select placeholder
  }

  /**
   * Removes a recipient from the form data.
   * @param recipient The recipient name to remove.
   */
  removeRecipient(recipient: string): void {
    this.formData.update(data => ({ ...data, recipients: data.recipients.filter(d => d !== recipient) }));
  }
  
  // --- Template Helpers ---

  /**
   * Returns metadata for a status badge so the template can render it using components.
   * @param status Communication status.
   */
  getBadgeMeta(status: Communication['status']): { text: string; classes: string; icon?: string } {
    let colorClass = '';
    let icon: string | undefined;
    switch (status) {
      case 'Enviado': colorClass = 'bg-green-100 text-green-800'; icon = 'check-circle'; break;
      case 'Programado': colorClass = 'bg-blue-100 text-blue-800'; icon = 'clock'; break;
      case 'Borrador': colorClass = 'bg-gray-100 text-gray-800'; icon = 'file-text'; break;
      default: colorClass = 'bg-gray-100 text-gray-800';
    }
    return { text: status, classes: `inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${colorClass} min-w-[70px] justify-center`, icon };
  }

  /**
   * Returns channel icon metadata so template can render one or two <app-icon> elements.
   */
  getChannelMeta(channel: string): { icons: { name: string; classes?: string }[] } {
    const mail = { name: 'Mail', classes: 'text-blue-600' };
    const wa = { name: 'MessageSquare', classes: 'text-green-600' };
    if (channel === 'Email + WhatsApp') return { icons: [mail, wa] };
    if (channel === 'Email') return { icons: [mail] };
    if (channel === 'WhatsApp') return { icons: [wa] };
    return { icons: [] };
  }

  /**
   * Provides a truncated preview of the content.
   * @param content Full content string.
   * @param channel Channel type (for special WhatsApp prefix).
   * @returns Short preview string.
   */
  getCommunicationPreview(content: string, channel: string): string {
    const maxLength = channel.includes('WhatsApp') ? 50 : 80;
    let preview = content.length > maxLength ? content.substring(0, maxLength) + '...' : content;
    if (channel.includes('WhatsApp')) {
      return `📱 ${preview}`;
    }
    return preview;
  }

  /**
   * Helper function to get an inline SVG icon.
   * @param iconName Name of the Lucide icon.
   * @returns SVG HTML string.
   */
  getIconSvg(iconName: string, size: number = 20): string {
    const svg = ICON_SVGS[iconName];
    if (svg) {
      // Simple regex replacement to adjust size dynamically if needed, though most icons are pre-sized.
      return svg.replace(/width="\d+"/, `width="${size}"`).replace(/height="\d+"/, `height="${size}"`);
    }
    return '❓';
  }
}
