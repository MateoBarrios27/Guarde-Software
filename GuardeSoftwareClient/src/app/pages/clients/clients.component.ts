import { Component, OnInit } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent } from '../../shared/components/icon/icon.component';
import { GetClientsRequest } from '../../core/dtos/client/GetClientsRequest';
import { ClientService } from '../../core/services/client-service/client.service';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { TableClient } from '../../core/dtos/client/TableClientDto';
import { NgxPaginationModule } from 'ngx-pagination';
import { PhonePipe } from '../../shared/pipes/phone.pipe';

@Component({
  selector: 'app-clients',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, CurrencyPipe, NgxPaginationModule, PhonePipe],
  templateUrl: './clients.component.html',
})
export class ClientsComponent implements OnInit {

  public activeTab: 'clientes' | 'pagos' = 'clientes';
  public clientes: TableClient[] = [];
  public totalClientes = 0;
  public isLoading = false;
  

  public estadisticas = { total: 0, alDia: 0, morosos: 0, pendientes: 0, dadosBaja: 0 };

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

  constructor(private clientService: ClientService) {}

  ngOnInit(): void {
    this.loadClients();
    
    this.searchSubject.pipe(
      debounceTime(400),
      distinctUntilChanged()
    ).subscribe(() => {
      this.currentPageClientes = 1;
      this.loadClients();
    });
  }

  loadClients(): void {
    this.isLoading = true;
    const request: GetClientsRequest = {
      pageNumber: this.currentPageClientes,
      pageSize: this.itemsPerPageClientes,
      sortField: this.sortFieldClientes,
      sortDirection: this.sortDirectionClientes,
      searchTerm: this.searchClientes || undefined,
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
      }
    });
  }

  get totalPages(): number {
    return Math.ceil(this.totalClientes / this.itemsPerPageClientes);
  }

  handleSort(field: string): void {
    if (this.sortFieldClientes === field) {
      this.sortDirectionClientes = this.sortDirectionClientes === 'asc' ? 'desc' : 'asc';
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
    this.currentPageClientes = 1;
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
  }

  // --- Style Helpers ---
  getEstadoBadgeColor(estado: string): string {
    const colors: Record<string, string> = { 'Al día': 'bg-green-100 text-green-800', 'Moroso': 'bg-red-100 text-red-800', 'Pendiente': 'bg-yellow-100 text-yellow-800', 'Baja': 'bg-gray-200 text-gray-800' };
    return colors[estado] || 'bg-gray-100 text-gray-800';
  }

  getDocumentoBadgeColor(documento: string): string {
    const colors: Record<string, string> = { 'SF': 'bg-blue-100 text-blue-800', 'FB': 'bg-green-100 text-green-800', 'FA': 'bg-purple-100 text-purple-800', 'FBN': 'bg-orange-100 text-orange-800' };
    return colors[documento] || 'bg-gray-100 text-gray-800';
  }
}