import { Component, OnInit } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgxPaginationModule } from 'ngx-pagination';
import { IconComponent } from '../../shared/components/icon/icon.component';
import { PhonePipe } from '../../shared/pipes/phone.pipe';
import { CreateClientModalComponent } from '../../shared/components/create-client-modal/create-client-modal.component';

// --- Modelos y Servicios para la TABLA ---
import { TableClient } from '../../core/dtos/client/TableClientDto';
import { GetClientsRequest } from '../../core/dtos/client/GetClientsRequest';
import { ClientService } from '../../core/services/client-service/client.service';
import { CreateClientDTO } from '../../core/dtos/client/CreateClientDTO';
import { ClientDetailDTO } from '../../core/dtos/client/ClientDetailDTO'; // Asume que existe un DTO de Update

import { Subject, Observable } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { ClientDetailModalComponent } from "../../shared/components/client-detail-modal/client-detail-modal.component";
import Swal from 'sweetalert2';

@Component({
  selector: 'app-clients',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    IconComponent,
    CurrencyPipe,
    NgxPaginationModule,
    PhonePipe,
    CreateClientModalComponent,
    ClientDetailModalComponent
],
  templateUrl: './clients.component.html',
})
export class ClientsComponent implements OnInit {
  // --- Propiedades de la tabla ---
  public activeTab: 'clientes' | 'pagos' = 'clientes';
  public clientes: TableClient[] = [];
  public totalClientes = 0;
  public isLoading = false;
  public estadisticas = {
    total: 0,
    alDia: 0,
    morosos: 0,
    pendientes: 0,
    dadosBaja: 0,
  };
  public searchClientes = '';
  private searchSubject = new Subject<string>();
  public filterEstadoClientes = 'Todos';
  public showInactivos = false;
  public currentPageClientes = 1;
  public itemsPerPageClientes = 10;
  public itemsPerPageOptions = [10, 20, 50];
  public sortFieldClientes = 'FirstName';
  public sortDirectionClientes: 'asc' | 'desc' = 'asc';
  public readonly Math = Math;

  // --- Create Client properties  ---
  public showNewClientModal = false;
  public clientToEdit: ClientDetailDTO | null = null;

  // --- Detail Client properties  ---
  public showDetailClientModal = false;
  public clientToView: ClientDetailDTO | null = null;

  // --- Toast properties ---
  public showToast = false;
  public toastMessage = '';
  public toastType: 'success' | 'error' = 'success';

  public showDeactivateModal = false;
  public clientToDeactivateId: string | null = null;

  constructor(private clientService: ClientService) 
  {
    // La suscripción se hace en el constructor
    this.searchSubject.pipe(
      debounceTime(400), // Espera 400ms después de la última pulsación
      distinctUntilChanged() // No busca si el texto no ha cambiado
    ).subscribe(() => {
      this.currentPageClientes = 1; // Resetea a la primera página con cada nueva búsqueda
      this.loadClients();
    });
  }

  ngOnInit(): void {
    this.loadClients();
  }

  loadClients(): void {
    this.isLoading = true;

    const request: GetClientsRequest = {
      pageNumber: this.currentPageClientes,
      pageSize: this.itemsPerPageClientes,
      sortField: this.sortFieldClientes,
      sortDirection: this.sortDirectionClientes,
      searchTerm: this.searchClientes || undefined,
      statusFilter: this.filterEstadoClientes === 'Todos' ? undefined : this.filterEstadoClientes,
      active: !this.showInactivos,
    };

    this.clientService.getTableClients(request).subscribe({
      next: (result) => {
        this.clientes = result.items;

        this.totalClientes = result.totalCount;

        this.estadisticas.total = result.totalCount;

        this.isLoading = false;
      },

      error: (err) => {
        console.error('Error al cargar clientes:', err);

        this.isLoading = false;
      },
    });
  }

  get totalPages(): number {
    return Math.ceil(this.totalClientes / this.itemsPerPageClientes);
  }

  handleSort(field: string): void {
    if (this.sortFieldClientes === field) {
      this.sortDirectionClientes =
        this.sortDirectionClientes === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortFieldClientes = field;

      this.sortDirectionClientes = 'asc';
    }

    this.loadClients();
  }

  onSearchChange(): void {
    this.searchSubject.next(this.searchClientes);
  }

  onFilterChange(): void {
    this.currentPageClientes = 1; // Volver a pág 1
    this.loadClients();
  }

  toggleInactivos(): void {
    this.showInactivos = !this.showInactivos;
    
    // Opcional: Resetear filtro de estado al cambiar modo, para evitar confusión
    // (Ej: si estaba filtrando por "Al día" y cambio a inactivos, no veré nada)
    if (this.showInactivos) {
        this.filterEstadoClientes = 'Todos'; 
    }
    
    this.currentPageClientes = 1; // Siempre volver a pág 1 al cambiar filtro principal
    this.loadClients();
  }

  onItemsPerPageChange(): void {
    this.currentPageClientes = 1;

    this.loadClients();
  }

  onPageChange(newPage: number): void {
    if (newPage > 0 && newPage <= this.totalPages) {
      this.currentPageClientes = newPage;

      this.loadClients();
    }
  }

  handleResetFilters(): void {
    this.searchClientes = '';

    this.filterEstadoClientes = 'Todos';

    this.showInactivos = false;

    this.currentPageClientes = 1;

    this.sortFieldClientes = 'FirstName';

    this.sortDirectionClientes = 'asc';

    this.loadClients();
  } // --- Style Helpers ---

  getEstadoBadgeColor(estado: string): string {
    const colors: Record<string, string> = {
      'Al día': 'bg-green-100 text-green-800',
      Moroso: 'bg-red-100 text-red-800',
      Pendiente: 'bg-yellow-100 text-yellow-800',
      Baja: 'bg-gray-200 text-gray-800',
    };
    return colors[estado] || 'bg-gray-100 text-gray-800';
  }

  getEstadoIcon(estado: string): string {
    const icons: Record<string, string> = {
      'Al día': 'check-circle',
      Moroso: 'alert-triangle',
      Pendiente: 'clock',
      Baja: 'user-x',
    };
    return icons[estado] || 'help-circle'; // Un ícono por defecto
  }

  getDocumentoBadgeColor(documento: string): string {
    const colors: Record<string, string> = {
      SF: 'bg-blue-100 text-blue-800',
      FB: 'bg-green-100 text-green-800',
      FA: 'bg-purple-100 text-purple-800',
      FBN: 'bg-orange-100 text-orange-800',
    };

    return colors[documento] || 'bg-gray-100 text-gray-800';
  }

  // --- Métodos de Interacción con el Modal ---
  openNewClientModal(): void {
    this.clientToEdit = null;
    this.showNewClientModal = true;
  }

  openEditClientModal(clientId: number): void {
    this.clientService
      .getClientDetailById(clientId)
      .subscribe((clientDetail) => {
        console.log('Client detail fetched:', clientDetail);
        this.clientToEdit = clientDetail;
        this.showNewClientModal = true;
      });
  }

  closeNewClientModal(): void {
    this.showNewClientModal = false;
    this.clientToEdit = null;
  }

  onClientSaveSuccess(): void {
    this.showToastNotification('¡Cliente guardado exitosamente!', 'success');
    this.closeNewClientModal();
    this.loadClients(); // Refresca la tabla
  }

  private showToastNotification(
    message: string,
    type: 'success' | 'error'
  ): void {
    this.toastMessage = message;
    this.toastType = type;
    this.showToast = true;
    setTimeout(() => {
      this.showToast = false;
    }, 3000);
  }

  getSortIcon(field: string): string {
    if (this.sortFieldClientes !== field) {
      return 'arrow-up-down'; // Ícono neutral para columnas no activas
    }
    return this.sortDirectionClientes === 'asc' ? 'arrow-up' : 'arrow-down';
  }

  // --- Methods for Detail Client Modal ---
  openDetailClientModal(clientId: number): void {
    this.clientService
      .getClientDetailById(clientId)
      .subscribe((clientDetail) => {
        this.clientToView = clientDetail;
        this.showDetailClientModal = true;
      });
  }

  closeDetailClientModal(): void {
    this.showDetailClientModal = false;
    this.clientToView = null;
  }

  public openDeactivateClientModal(clientId: number): void {

  Swal.fire({
    title: '¿Estás seguro?',
    text: "El cliente será marcado como 'Dado de Baja'. Esta acción se puede revertir, pero deberás hacerlo manualmente.",
    icon: 'warning',
    showCancelButton: true,
    confirmButtonColor: '#d33', // Red to confirm
    cancelButtonColor: '#6B7280', // Grey to cancel
    confirmButtonText: 'Sí, dar de baja',
    cancelButtonText: 'Cancelar',
  }).then((result) => {
    // 2. Check if the user confirmed the action
    if (result.isConfirmed) {
      // 3. Implements the deactivation logic here

      // this.clientService.deactivateClient(clientId).subscribe({
      //   next: () => {
      //     // 4. Show success toast
      //     this.toastMessage = 'Cliente dado de baja exitosamente.';
      //     this.toastType = 'success';
      //     this.showToast = true;

      //     this.loadClients(); // Reload the clients list
      //   },
      //   error: (err) => {
      //     // 5. Error
      //     console.error('Error al dar de baja al cliente:', err);
      //     this.toastMessage = 'Error al intentar dar de baja al cliente.';
      //     this.toastType = 'error';
      //     this.showToast = true;
      //   },
      // });
    }
  });
}
}
