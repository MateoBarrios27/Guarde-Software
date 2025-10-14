// src/app/features/communications/mock-data.ts
import { Communication, ClientOption } from './../../core/models/communications';

export const MOCK_COMMUNICATIONS: Communication[] = [
  // Your `comunicadosData` translated to English and using the new interface
  { id: 1, title: 'Rate Increase 2024', content: 'Dear customers, we inform you that starting next month...', sendDate: '2024-09-15', sendTime: '10:00', channel: 'Email', recipients: ['All clients'], status: 'Scheduled', createdAt: '2024-08-12' },
  { id: 2, title: 'Scheduled Maintenance', content: 'We inform you that on Saturday 09/20 we will perform maintenance tasks...', sendDate: '2024-09-18', sendTime: '09:00', channel: 'WhatsApp', recipients: ['Clients with debt'], status: 'Scheduled', createdAt: '2024-08-10' },
  { id: 3, title: 'Payment Reminder', content: 'Your payment is overdue. Please contact administration.', sendDate: null, sendTime: null, channel: 'Email', recipients: ['Overdue clients'], status: 'Draft', createdAt: '2024-08-11' },
  { id: 4, title: 'Welcome new clients', content: 'Welcome to GuardeSoftware. Your access details are...', sendDate: '2024-08-01', sendTime: '14:30', channel: 'Email', recipients: ['John Doe', 'Mary Smith'], status: 'Sent', createdAt: '2024-07-30' },
  { id: 5, title: 'Updated Business Hours', content: 'New business hours: Monday to Friday 8:00 AM to 6:00 PM...', sendDate: '2024-07-15', sendTime: '12:00', channel: 'WhatsApp', recipients: ['All clients'], status: 'Sent', createdAt: '2024-07-14' }
];

export const MOCK_CLIENT_OPTIONS: ClientOption[] = [
  // Your `clientesOptions`
  { id: 'all', name: 'All clients' },
  { id: 'debt', name: 'Clients with debt' },
  { id: 'overdue', name: 'Overdue clients' },
  { id: 'paid_up', name: 'Paid-up clients' },
  { id: 'client_1', name: 'John Doe' },
  { id: 'client_2', name: 'Mary Smith' },
];