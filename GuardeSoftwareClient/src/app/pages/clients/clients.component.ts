import { Component } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent } from '../../shared/components/icon/icon.component'; // Asegúrate de que la ruta sea correcta

interface Cliente {
  id: number;
  numeroIdentificacion: string;
  nombre: string;
  email: string[];
  telefono: string[];
  ciudad: string;
  baulera: string;
  documento: string;
  estado: string;
  precioRenta: number;
  m3Contratados: number;
  activo: boolean;
  [key: string]: any;
}

const clientesData = [
    { id: 1, numeroIdentificacion: 'PAG-017', nombre: 'Alejandro Torres', email: ['alejandro.torres@gmail.com'], telefono: ['+54 11 8888-9999'], ciudad: 'Buenos Aires', baulera: 'C-90', documento: 'FA', estado: 'Moroso', precioRenta: 4700, m3Contratados: 7.3, activo: true },
    { id: 2, numeroIdentificacion: 'PAG-004', nombre: 'Ana Martínez', email: ['ana.martinez@gmail.com'], telefono: ['+54 11 7777-8888'], ciudad: 'Quilmes', baulera: 'A-23', documento: 'FBN', estado: 'Pendiente', precioRenta: 2500, m3Contratados: 4, activo: true },
    { id: 3, numeroIdentificacion: 'PAG-009', nombre: 'Andrés Gonzalez', email: ['andres.gonzalez@gmail.com'], telefono: ['+54 11 5555-6666'], ciudad: 'Rosario', baulera: 'B-33', documento: 'FA', estado: 'Pendiente', precioRenta: 4200, m3Contratados: 5.5, activo: true },
    { id: 4, numeroIdentificacion: 'PAG-018', nombre: 'Beatriz Jiménez', email: ['beatriz.jimenez@gmail.com'], telefono: ['+54 11 2222-3333'], ciudad: 'CABA', baulera: 'B-45', documento: 'FBN', estado: 'Pendiente', precioRenta: 3100, m3Contratados: 3, activo: true },
    { id: 5, numeroIdentificacion: 'PAG-001', nombre: 'Juan Pérez', email: ['juan.perez@email.com'], telefono: ['+54 11 1234-5678'], ciudad: 'Springfield', baulera: 'A-01', documento: 'SF', estado: 'Al día', precioRenta: 5000, m3Contratados: 10, activo: true },
    // A ESTOS DOS LES FALTABAN LAS PROPIEDADES
    { id: 6, numeroIdentificacion: 'PAG-002', nombre: 'María García', email: ['maria.garcia@email.com'], telefono: ['+54 11 9876-5432'], ciudad: 'La Plata', baulera: 'B-15', documento: 'FB', estado: 'Moroso', precioRenta: 7500, m3Contratados: 15, activo: true },
    { id: 7, numeroIdentificacion: 'PAG-010', nombre: 'Lucía Fernández', email: ['lucia.fdz@email.com'], telefono: ['+54 11 4444-5555'], ciudad: 'Rosario', baulera: 'D-05', documento: 'SF', estado: 'Baja', precioRenta: 6000, m3Contratados: 12, activo: false },
];

@Component({
  selector: 'app-clients',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, CurrencyPipe],
  templateUrl: './clients.component.html',
})
export class ClientsComponent {
  public activeTab: 'clientes' | 'pagos' = 'clientes';
  public clientes: Cliente[] = clientesData;

  // Estados de Filtro, Ordenamiento y Paginación
  public searchClientes = '';
  public filterEstadoClientes = 'Todos';
  public showInactivos = false;
  public currentPageClientes = 1;
  public itemsPerPageClientes = 10;
  public itemsPerPageOptions = [10, 20, 50];
  public sortFieldClientes = 'nombre';
  public sortDirectionClientes: 'asc' | 'desc' = 'asc';
public readonly Math = Math;
  
  // Getters para datos derivados (equivalente a useMemo)
  get filteredClientes(): Cliente[] {
    return this.clientes.filter(cliente => {
      const searchMatch = this.searchClientes === '' || 
        Object.values(cliente).some(val => 
          String(val).toLowerCase().includes(this.searchClientes.toLowerCase())
        );
      const estadoMatch = this.filterEstadoClientes === 'Todos' || cliente.estado === this.filterEstadoClientes;
      const activoMatch = this.showInactivos ? !cliente.activo : cliente.activo;
      return searchMatch && estadoMatch && activoMatch;
    });
  }

  get sortedClientes(): Cliente[] {
    return [...this.filteredClientes].sort((a, b) => {
      const aValue = a[this.sortFieldClientes];
      const bValue = b[this.sortFieldClientes];
      if (aValue < bValue) return this.sortDirectionClientes === 'asc' ? -1 : 1;
      if (aValue > bValue) return this.sortDirectionClientes === 'asc' ? 1 : -1;
      return 0;
    });
  }

  get paginatedClientes(): Cliente[] {
    const startIndex = (this.currentPageClientes - 1) * this.itemsPerPageClientes;
    return this.sortedClientes.slice(startIndex, startIndex + this.itemsPerPageClientes);
  }

  // Métodos de acción
  handleSort(field: string) {
    if (this.sortFieldClientes === field) {
      this.sortDirectionClientes = this.sortDirectionClientes === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortFieldClientes = field;
      this.sortDirectionClientes = 'asc';
    }
    this.currentPageClientes = 1;
  }
  
  handleResetFilters() {
    this.searchClientes = '';
    this.filterEstadoClientes = 'Todos';
    this.showInactivos = false;
    this.currentPageClientes = 1;
    this.sortFieldClientes = 'nombre';
    this.sortDirectionClientes = 'asc';
  }

  // Helpers para estilos (igual que en tu código de React)
  getEstadoBadgeColor(estado: string): string {
    const colors: Record<string, string> = {
      'Al día': 'bg-green-100 text-green-800',
      'Moroso': 'bg-red-100 text-red-800',
      'Pendiente': 'bg-yellow-100 text-yellow-800',
      'Baja': 'bg-gray-200 text-gray-800',
    };
    return colors[estado] || 'bg-gray-100 text-gray-800';
  }

  getDocumentoBadgeColor(documento: string): string {
    const colors: Record<string, string> = {
      'SF': 'bg-blue-100 text-blue-800', 'FB': 'bg-green-100 text-green-800',
      'FA': 'bg-purple-100 text-purple-800', 'FBN': 'bg-orange-100 text-orange-800',
    };
    return colors[documento] || 'bg-gray-100 text-gray-800';
  }
}