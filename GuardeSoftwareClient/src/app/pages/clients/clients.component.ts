import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Client } from '../../core/models/client';
import { ClientService } from '../../core/services/client.service';

// Define an interface for strong typing of our client data
type ClientStatus = 'Up to date' | 'Overdue' | 'Pending';

// interface Client {
//   id: string;
//   name: string;
//   email: string;
//   phone: string;
//   storageUnit: string;
//   document: string;
//   status: ClientStatus;
//   nextPayment: string;
//   avatar: {
//     initial: string;
//     color: string;
//   };
// }

@Component({
  selector: 'app-clients',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './clients.component.html',
})
export class ClientsComponent {

 clients: Client[] = [];

 constructor(private clientService: ClientService) {}

  ngOnInit(): void {
    this.clientService.getClients().subscribe({
      next: (data) => {
        this.clients = data;
        console.log('Clientes cargados:', this.clients);
      },
      error: (err) => {
        console.error('Error al obtener clientes:', err);
      }
    });
  }
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