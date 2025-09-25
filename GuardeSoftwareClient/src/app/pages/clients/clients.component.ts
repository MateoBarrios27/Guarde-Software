// import { Component } from '@angular/core';
// import { CommonModule } from '@angular/common';
// import { Client } from '../../core/models/client';
// import { ClientService } from '../../core/services/client.service';

// // Define an interface for strong typing of our client data
// type ClientStatus = 'Up to date' | 'Overdue' | 'Pending';

// // interface Client {
// //   id: string;
// //   name: string;
// //   email: string;
// //   phone: string;
// //   storageUnit: string;
// //   document: string;
// //   status: ClientStatus;
// //   nextPayment: string;
// //   avatar: {
// //     initial: string;
// //     color: string;
// //   };
// // }

// @Component({
//   selector: 'app-clients',
//   standalone: true,
//   imports: [CommonModule],
//   templateUrl: './clients.component.html',
// })
// export class ClientsComponent {

//  clients: Client[] = [];

//  constructor(private clientService: ClientService) {}

//   ngOnInit(): void {
//     this.clientService.getClients().subscribe({
//       next: (data) => {
//         this.clients = data;
//         console.log('Clientes cargados:', this.clients);
//       },
//       error: (err) => {
//         console.error('Error al obtener clientes:', err);
//       }
//     });
//   }
//   // Method to get the corresponding CSS class for each status
//   getStatusClass(status: ClientStatus): string {
//     switch (status) {
//       case 'Up to date':
//         return 'bg-green-100 text-green-800';
//       case 'Overdue':
//         return 'bg-red-100 text-red-800';
//       case 'Pending':
//         return 'bg-yellow-100 text-yellow-800';
//     }
//   }
// }

// src/app/pages/clients/clients.component.ts
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms'; // <-- ¡IMPORTANTE! Importa FormsModule

// --- Definimos nuestras interfaces (tipos de datos) ---
type ClientStatus = 'Al día' | 'Moroso' | 'Pendiente';

interface Client {
  id: number;
  nombre: string;
  numeroIdentificacion: string;
  email: string;
  telefono: string;
  baulera: string;
  documento: string;
  estado: ClientStatus;
  proximoPago: string | null;
  activo: boolean;
}

interface ClientStats {
  total: number;
  alDia: number;
  morosos: number;
  pendientes: number;
  dadosBaja: number;
}


@Component({
  selector: 'app-clients',
  standalone: true,
  imports: [CommonModule, FormsModule], // <-- ¡IMPORTANTE! Añade FormsModule aquí
  templateUrl: './clients.component.html',
})
export class ClientsComponent {
  // --- Estado de la Interfaz ---
  activeTab: 'clientes' | 'pagos' = 'clientes';
  searchTerm = '';
  statusFilter: ClientStatus | 'Todos' = 'Todos';
  showInactive = false;
  
  // --- Datos de Ejemplo ---
  stats: ClientStats = {
    total: 4,
    alDia: 2,
    morosos: 1,
    pendientes: 1,
    dadosBaja: 1,
  };

  allClients: Client[] = [
    { id: 1, nombre: 'Juan Pérez', numeroIdentificacion: 'PAG-001', email: 'juan.perez@email.com', telefono: '+54 11 1234-5678', baulera: 'A-01', documento: 'SF', estado: 'Al día', proximoPago: '2025-09-15', activo: true },
    { id: 2, nombre: 'María García', numeroIdentificacion: 'PAG-002', email: 'maria.garcia@email.com', telefono: '+54 11 9876-5432', baulera: 'B-15', documento: 'FB', estado: 'Moroso', proximoPago: '2025-07-01', activo: true },
    { id: 3, nombre: 'Carlos López', numeroIdentificacion: 'PAG-003', email: 'carlos.lopez@email.com', telefono: '+54 11 5555-1234', baulera: 'C-08', documento: 'FA', estado: 'Al día', proximoPago: '2025-09-01', activo: true },
    { id: 4, nombre: 'Ana Martínez', numeroIdentificacion: 'PAG-004', email: 'ana.martinez@email.com', telefono: '+54 11 7777-8888', baulera: 'A-23', documento: 'FBN', estado: 'Pendiente', proximoPago: '2025-08-20', activo: true },
    { id: 5, nombre: 'Lucía Fernández', numeroIdentificacion: 'PAG-005', email: 'lucia.fernandez@email.com', telefono: '+54 11 3333-4444', baulera: 'D-05', documento: 'SF', estado: 'Al día', proximoPago: '2025-10-01', activo: false },
  ];

  // --- Lógica de Filtro (propiedad computada) ---
  get filteredClients(): Client[] {
    return this.allClients.filter(client => {
      // Filtro por actividad (activo/inactivo)
      const activityMatch = this.showInactive ? !client.activo : client.activo;

      // Filtro por estado (Al día, Moroso, etc.)
      const statusMatch = this.statusFilter === 'Todos' || client.estado === this.statusFilter;

      // Filtro por término de búsqueda (nombre, email, etc.)
      const searchMatch = this.searchTerm.trim() === '' || 
        client.nombre.toLowerCase().includes(this.searchTerm.toLowerCase()) ||
        client.email.toLowerCase().includes(this.searchTerm.toLowerCase()) ||
        client.numeroIdentificacion.toLowerCase().includes(this.searchTerm.toLowerCase());

      return activityMatch && statusMatch && searchMatch;
    });
  }

  // --- Métodos de ayuda para los estilos ---
  getStatusBadgeClass(status: ClientStatus): string {
    switch (status) {
      case 'Al día': return 'bg-green-100 text-green-800';
      case 'Moroso': return 'bg-red-100 text-red-800';
      case 'Pendiente': return 'bg-yellow-100 text-yellow-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  }
}