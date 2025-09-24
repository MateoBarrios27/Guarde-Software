import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

// Define an interface for strong typing of our client data
type ClientStatus = 'Up to date' | 'Overdue' | 'Pending';

interface Client {
  id: string;
  name: string;
  email: string;
  phone: string;
  storageUnit: string;
  document: string;
  status: ClientStatus;
  nextPayment: string;
  avatar: {
    initial: string;
    color: string;
  };
}

@Component({
  selector: 'app-clients',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './clients.component.html',
})
export class ClientsComponent {
  // Sample data matching the reference image
  clients: Client[] = [
    {
      id: 'PAG-001',
      name: 'John Perez',
      email: 'john.perez@email.com',
      phone: '+54 11 1234-5678',
      storageUnit: 'A-01',
      document: 'SF',
      status: 'Up to date',
      nextPayment: '2024-09-15',
      avatar: { initial: 'J', color: 'bg-blue-500' }
    },
    {
      id: 'PAG-002',
      name: 'Maria Garcia',
      email: 'maria.garcia@email.com',
      phone: '+54 11 9876-5432',
      storageUnit: 'B-15',
      document: 'FB',
      status: 'Overdue',
      nextPayment: '2024-07-01',
      avatar: { initial: 'M', color: 'bg-purple-500' }
    },
    {
      id: 'PAG-003',
      name: 'Carlos Lopez',
      email: 'carlos.lopez@email.com',
      phone: '+54 11 5555-1234',
      storageUnit: 'C-08',
      document: 'FA',
      status: 'Up to date',
      nextPayment: '2024-09-01',
      avatar: { initial: 'C', color: 'bg-green-500' }
    },
    {
      id: 'PAG-004',
      name: 'Ana Martinez',
      email: 'ana.martinez@email.com',
      phone: '+54 11 7777-8888',
      storageUnit: 'A-23',
      document: 'FBN',
      status: 'Pending',
      nextPayment: '2024-08-20',
      avatar: { initial: 'A', color: 'bg-orange-500' }
    }
  ];

  // Method to get the corresponding CSS class for each status
  getStatusClass(status: ClientStatus): string {
    switch (status) {
      case 'Up to date':
        return 'bg-green-100 text-green-800';
      case 'Overdue':
        return 'bg-red-100 text-red-800';
      case 'Pending':
        return 'bg-yellow-100 text-yellow-800';
    }
  }
}