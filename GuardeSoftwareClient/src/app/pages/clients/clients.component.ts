import { AfterViewInit, Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild } from '@angular/core';
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
import { ClientDetailDTO } from '../../core/dtos/client/ClientDetailDTO';

import { Subject, Observable } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { ClientDetailModalComponent } from "../../shared/components/client-detail-modal/client-detail-modal.component";
import Swal from 'sweetalert2';
import { ClientStatisticsDto } from '../../core/dtos/statistics/ClientStatisticsDto';
import { StatisticsService } from '../../core/services/statics-service/statics-service.service';
import { ɵɵDir } from "@angular/cdk/scrolling";
import { Warehouse } from '../../core/models/warehouse';
import { WarehouseService } from '../../core/services/warehouse-service/warehouse.service';
import { Router } from '@angular/router';
import { BillingType } from '../../core/models/billing-type.model';
import { BillingTypeService } from '../../core/services/billingType-service/billing-type.service';
import { PaymentMethod } from '../../core/models/payment-method';
import { PaymentMethodService } from '../../core/services/paymentMethod-service/payment-method.service';

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
    ClientDetailModalComponent,
    ɵɵDir
],
  templateUrl: './clients.component.html',
})
export class ClientsComponent implements OnInit, AfterViewInit, OnDestroy {

  // --- Table properties ---
  public activeTab: 'clientes' | 'pagos' = 'clientes';
  public clientes: TableClient[] = [];
  public totalClientes = 0;
  public isLoading = false;
  public estadisticas: ClientStatisticsDto = {
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
  public itemsPerPageClientes = 400;
  // public itemsPerPageOptions = [100];
  public sortFieldClientes = 'PaymentIdentifier';
  public sortDirectionClientes: 'asc' | 'desc' = 'asc';
  public readonly Math = Math;
  public activeCommentClient: TableClient | null = null;
  public isCommentPinned: boolean = false;
  private commentHoverTimer: any = null;
  private commentLeaveTimer: any = null;

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

  public isReactivationMode = false;

  public warehouses: Warehouse[] = [];
  public selectedWarehouseIds: number[] = [];
  public selectedQuickFilters: string[] = [];

  // --- Tags filter properties ---
  public billingTypes: BillingType[] = [];
  public paymentMethods: PaymentMethod[] = [];
  public ivaConditionsList: string[] = ['Consumidor Final', 'Monotributista', 'Responsable Inscripto', 'Exento', 'Sin asignar'];

  public selectedIvaConditions: string[] = [];
  public selectedBillingTypeIds: number[] = [];
  public selectedPaymentMethodIds: number[] = [];
  public showTagsPopover = false;

  @ViewChild('tagsPopoverRef') tagsPopoverRef!: ElementRef;
  @ViewChild('tagsButtonRef') tagsButtonRef!: ElementRef;

  totals = {
    previousBalance: 0,
    interestAmount: 0,
    currentRent: 0,
    balance: 0
  };

  @ViewChild('topAnchor') topAnchor!: ElementRef;
  @ViewChild('bottomAnchor') bottomAnchor!: ElementRef;
  
  pointingUp: boolean = false; 
  private scrollObserver!: IntersectionObserver;

  constructor(
    private clientService: ClientService, 
    private statisticsService: StatisticsService, 
    private warehouseService: WarehouseService, 
    private billingTypeService: BillingTypeService,
    private paymentMethodService: PaymentMethodService,
    private router: Router
  ) 
  {
    this.searchSubject.pipe(
      debounceTime(400),
      distinctUntilChanged() 
    ).subscribe(() => {
      this.currentPageClientes = 1; 
      this.loadClients();
    });
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.showTagsPopover) return;
    const clickedInsidePopover = this.tagsPopoverRef && this.tagsPopoverRef.nativeElement.contains(event.target);
    const clickedInsideButton = this.tagsButtonRef && this.tagsButtonRef.nativeElement.contains(event.target);
    if (!clickedInsidePopover && !clickedInsideButton) {
      this.showTagsPopover = false;
    }
  }

  goToPayment(clientId: number) {
    this.router.navigate(['/finances'], { 
      queryParams: { 
        autoOpenPayment: clientId, 
        returnTo: 'clients' 
      } 
    });
  }

  ngOnInit(): void {
    this.loadClients();
    this.loadStatistics();
    this.warehouseService.getWarehouses().subscribe(data => this.warehouses = data);
    this.billingTypeService.getBillingTypes().subscribe(data => this.billingTypes = data);
    this.paymentMethodService.getPaymentMethods().subscribe(data => this.paymentMethods = data);
  }

  public quickFiltersList = [
    { value: 'pagaron_este_mes', label: 'Pagaron este mes' },
    { value: 'no_pagaron_este_mes', label: 'No pagaron este mes' },
    { value: 'pagaron_meses_futuros', label: 'Pagaron meses futuros' },
    { value: 'intereses_impagos', label: 'Con intereses impagos' },
    { value: 'aumento_proximo_mes', label: 'Aumento próximo mes' }
  ];

  toggleTagsPopover(): void {
    this.showTagsPopover = !this.showTagsPopover;
  }

  toggleWarehouseId(id: number): void {
    const idx = this.selectedWarehouseIds.indexOf(id);
    if (idx > -1) {
      this.selectedWarehouseIds.splice(idx, 1);
    } else {
      this.selectedWarehouseIds.push(id);
    }
    this.currentPageClientes = 1;
    this.loadClients();
  }

  getWarehouseName(id: number): string {
    const w = this.warehouses.find(item => item.id === id);
    return w ? w.name : `Depósito (${id})`;
  }

  toggleQuickFilter(val: string): void {
    const idx = this.selectedQuickFilters.indexOf(val);
    if (idx > -1) {
      this.selectedQuickFilters.splice(idx, 1);
    } else {
      this.selectedQuickFilters.push(val);
    }
    this.currentPageClientes = 1;
    this.loadClients();
  }

  getQuickFilterLabel(val: string): string {
    const qf = this.quickFiltersList.find(q => q.value === val);
    return qf ? qf.label : val;
  }

  toggleIvaCondition(cond: string): void {
    const idx = this.selectedIvaConditions.indexOf(cond);
    if (idx > -1) {
      this.selectedIvaConditions.splice(idx, 1);
    } else {
      this.selectedIvaConditions.push(cond);
    }
    this.currentPageClientes = 1;
    this.loadClients();
  }

  toggleBillingTypeId(id: number): void {
    const idx = this.selectedBillingTypeIds.indexOf(id);
    if (idx > -1) {
      this.selectedBillingTypeIds.splice(idx, 1);
    } else {
      this.selectedBillingTypeIds.push(id);
    }
    this.currentPageClientes = 1;
    this.loadClients();
  }

  togglePaymentMethodId(id: number): void {
    const idx = this.selectedPaymentMethodIds.indexOf(id);
    if (idx > -1) {
      this.selectedPaymentMethodIds.splice(idx, 1);
    } else {
      this.selectedPaymentMethodIds.push(id);
    }
    this.currentPageClientes = 1;
    this.loadClients();
  }

  clearAllTags(): void {
    this.selectedWarehouseIds = [];
    this.selectedQuickFilters = [];
    this.selectedIvaConditions = [];
    this.selectedBillingTypeIds = [];
    this.selectedPaymentMethodIds = [];
    this.currentPageClientes = 1;
    this.loadClients();
  }

  get totalActiveTagsCount(): number {
    return (
      this.selectedWarehouseIds.length +
      this.selectedQuickFilters.length +
      this.selectedIvaConditions.length +
      this.selectedBillingTypeIds.length +
      this.selectedPaymentMethodIds.length
    );
  }

  getBillingTypeName(id: number): string {
    if (id === 0 || id === -1) return 'Sin factura';
    const bt = this.billingTypes.find(b => b.id === id);
    return bt ? bt.name : `Factura (${id})`;
  }

  getPaymentMethodName(id: number): string {
    if (id === 0 || id === -1) return 'Sin asignar';
    const pm = this.paymentMethods.find(p => p.id === id);
    return pm ? pm.name : `Método (${id})`;
  }

  ngAfterViewInit() {
    this.scrollObserver = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        if (entry.target === this.bottomAnchor.nativeElement) {
          this.pointingUp = entry.isIntersecting;
        }
      });
    }, { threshold: 0 });

    if (this.bottomAnchor) {
      this.scrollObserver.observe(this.bottomAnchor.nativeElement);
    }
  }

  ngOnDestroy() {
    if (this.scrollObserver) {
      this.scrollObserver.disconnect();
    }
  }

  toggleScroll() {
    const scrollContainer = document.getElementById('main-scroll');
    if (!scrollContainer) return;

    if (this.pointingUp) {
      scrollContainer.scrollTo({ top: 0, behavior: 'smooth' });
    } else {
      scrollContainer.scrollTo({ top: scrollContainer.scrollHeight, behavior: 'smooth' });
    }
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
      warehouseIds: this.selectedWarehouseIds.length > 0 ? this.selectedWarehouseIds : undefined,
      advancedFilters: this.selectedQuickFilters.length > 0 ? this.selectedQuickFilters : undefined,
      ivaConditions: this.selectedIvaConditions.length > 0 ? this.selectedIvaConditions : undefined,
      billingTypeIds: this.selectedBillingTypeIds.length > 0 ? this.selectedBillingTypeIds : undefined,
      preferredPaymentMethodIds: this.selectedPaymentMethodIds.length > 0 ? this.selectedPaymentMethodIds : undefined
    };

    this.clientService.getTableClients(request).subscribe({
      next: (result) => {
        this.clientes = result.items;

        this.totalClientes = result.totalCount;
        this.calculateTotals();
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

  calculateTotals(): void {
    this.totals = {
      previousBalance: 0,
      interestAmount: 0,
      currentRent: 0,
      balance: 0
    };

    // Recorremos el array 'clientes' que es el que se muestra en la tabla
    this.clientes.forEach(cliente => {
      this.totals.previousBalance += Number(cliente.previousBalance) || 0;
      this.totals.interestAmount += Number(cliente.interestAmount) || 0;
      this.totals.currentRent += Number(cliente.currentRent) || 0;
      this.totals.balance += Number(cliente.balance) || 0;
    });
  }

  handleSort(field: string): void {
    if (this.sortFieldClientes === field) {
      if (this.sortDirectionClientes === 'asc') {
        this.sortDirectionClientes = 'desc';
      } else {
        this.sortFieldClientes = 'PaymentIdentifier';
        this.sortDirectionClientes = 'asc';
      }
    } else {
      this.sortFieldClientes = field;
      this.sortDirectionClientes = 'asc';
    }

    this.loadClients();
  }

  onSearchChange(): void {
    this.searchSubject.next(this.searchClientes);
    this.calculateTotals();
  }

  onFilterChange(): void {
    this.currentPageClientes = 1; // Volver a pág 1
    this.loadClients();
    this.calculateTotals();
  }

  toggleInactivos(): void {
    this.showInactivos = !this.showInactivos;
    if (this.showInactivos) {
        this.filterEstadoClientes = 'Todos'; 
    }
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
    this.selectedWarehouseIds = [];
    this.selectedQuickFilters = [];
    this.selectedIvaConditions = [];
    this.selectedBillingTypeIds = [];
    this.selectedPaymentMethodIds = [];
    this.showInactivos = false;
    this.currentPageClientes = 1;
    this.sortFieldClientes = 'PaymentIdentifier';
    this.sortDirectionClientes = 'asc';
    
    this.loadClients();
  }
  
  getEstadoBadgeColor(estado: string): string {
    if (!estado) return 'bg-gray-100 text-gray-800';

    if (estado.startsWith('Moroso')) {
      return 'bg-red-100 text-red-800';
    }

    const colors: Record<string, string> = {
      'Al día': 'bg-green-100 text-green-800',
      'Pendiente': 'bg-yellow-100 text-yellow-800',
      'Baja': 'bg-gray-200 text-gray-800',
    };

    return colors[estado] || 'bg-gray-100 text-gray-800';
  }

  getEstadoIcon(estado: string): string {
    if (!estado) return 'help-circle';

    if (estado.startsWith('Moroso')) {
      return 'alert-triangle';
    }

    const icons: Record<string, string> = {
      'Al día': 'check-circle',
      'Pendiente': 'clock',
      'Baja': 'user-x',
    };

    return icons[estado] || 'help-circle';
  }

  getDisplayStatus(status: string): string {
    if (!status) return '';

    if (status.startsWith('Moroso N')) {
      const nivel = parseInt(status.replace('Moroso N', ''), 10);
      
      if (nivel > 3) {
        return 'Moroso N3';
      }
    }
    return status;
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
    this.isReactivationMode = false;
    this.clientToEdit = null;
    this.showNewClientModal = true;
  }

  openEditClientModal(clientId: number): void {
    this.isReactivationMode = false;
    this.fetchAndOpenModal(clientId);
  }

  private fetchAndOpenModal(clientId: number): void {
    this.clientService.getClientDetailById(clientId).subscribe((clientDetail) => {
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
    this.loadClients();
    this.loadStatistics();
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

  public openDeactivateClientModal(cliente: TableClient): void {
    let warningText = "El cliente será marcado como 'Dado de Baja'. Se finalizará su alquiler actual.";
    let iconType: 'warning' | 'info' = 'warning';

    if (cliente.lockers && cliente.lockers.length > 0 && cliente.lockers[0] !== '') {
       warningText += `\n\nLas siguientes bauleras serán liberadas y desasignadas: ${cliente.lockers.join(', ')}.`;
    }

    if (cliente.balance > 0) {
       const deuda = new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', maximumFractionDigits: 0 }).format(cliente.balance);
       warningText = `¡ATENCIÓN! El cliente tiene una deuda de ${deuda}.\n\n¿Estás seguro de darlo de baja sin saldar la deuda?`;
       if (cliente.lockers && cliente.lockers.length > 0 && cliente.lockers[0] !== '') {
          warningText += `\n\nAdemás, las siguientes bauleras serán liberadas y desasignadas: ${cliente.lockers.join(', ')}.`;
       }
    } else if (cliente.balance < 0) {
        warningText += "\n\nNota: El cliente tiene saldo a favor.";
    }

    Swal.fire({
      title: '¿Confirmar Baja?',
      text: warningText,
      icon: iconType,
      showCancelButton: true,
      confirmButtonColor: '#d33',
      cancelButtonColor: '#6B7280',
      confirmButtonText: 'Sí, dar de baja',
      cancelButtonText: 'Cancelar',
    }).then((result) => {
      if (result.isConfirmed) {
        this.isLoading = true;
        this.clientService.deactivateClient(cliente.id).subscribe({
          next: () => {
            this.isLoading = false;
            Swal.fire(
              '¡Dado de Baja!',
              'El cliente ha sido desactivado exitosamente.',
              'success'
            );
            this.loadClients();
          },
          error: (err) => {
            this.isLoading = false;
            console.error('Error al dar de baja:', err);
            const msg = err.error?.message || 'Ocurrió un error al intentar dar de baja.';
            Swal.fire('Error', msg, 'error');
          },
        });
      }
    });
  }

  loadStatistics(): void {
    this.statisticsService.getClientStatistics().subscribe({
      next: (stats) => {
        this.estadisticas = stats;
      },
      error: (err) => console.error('Error cargando estadísticas:', err)
    });
  }

  public onReactivateClient(cliente: TableClient): void {
    Swal.fire({
      title: '¿Reactivar Cliente?',
      html: `
        Vas a reactivar a <strong>${cliente.fullName}</strong>.<br><br>
        <ul style="text-align: left; font-size: 0.9em; margin-left: 20px;">
          <li>Se generará un <strong>nuevo Número de Identificación</strong>.</li>
          <li>Se abrirá el formulario para <strong>confirmar los datos y asignar bauleras</strong>.</li>
        </ul>
      `,
      icon: 'info',
      showCancelButton: true,
      confirmButtonColor: '#10b981', 
      cancelButtonColor: '#6B7280',
      confirmButtonText: 'Sí, configurar reactivación',
      cancelButtonText: 'Cancelar',
    }).then((result) => {
      if (result.isConfirmed) {
        
        this.isReactivationMode = true; 
        this.fetchAndOpenModal(cliente.id);
        
      }
    });
  }

  onClientDataUpdated(clientId: number): void {
    this.loadClients();

    this.loadStatistics();

    this.clientService.getClientDetailById(clientId).subscribe((clientDetail) => {
      this.clientToView = clientDetail;
    });
  }

  onCommentMouseEnter(cliente: TableClient): void {
    if (this.commentLeaveTimer) {
      clearTimeout(this.commentLeaveTimer);
      this.commentLeaveTimer = null;
    }

    if (this.activeCommentClient === cliente) return;

    if (this.commentHoverTimer) {
      clearTimeout(this.commentHoverTimer);
    }

    this.commentHoverTimer = setTimeout(() => {
      if (this.activeCommentClient && !this.isCommentPinned && this.activeCommentClient !== cliente) {
        this.closeComment(this.activeCommentClient);
      }
      this.activeCommentClient = cliente;
      this.isCommentPinned = false;
      setTimeout(() => {
        const activeTextarea = document.getElementById('client-comment-' + cliente.id) as HTMLTextAreaElement;
        if (activeTextarea) {
          activeTextarea.style.height = 'auto';
          activeTextarea.style.height = activeTextarea.scrollHeight + 'px';
        }
      }, 0);
    }, 400);
  }

  onCommentMouseLeave(cliente: TableClient): void {
    if (this.commentHoverTimer) {
      clearTimeout(this.commentHoverTimer);
      this.commentHoverTimer = null;
    }

    if (this.activeCommentClient === cliente && !this.isCommentPinned) {
      if (this.commentLeaveTimer) {
        clearTimeout(this.commentLeaveTimer);
      }
      this.commentLeaveTimer = setTimeout(() => {
        if (this.activeCommentClient === cliente && !this.isCommentPinned) {
          this.closeComment(cliente);
        }
      }, 200);
    }
  }

  pinComment(cliente: TableClient): void {
    if (this.commentLeaveTimer) {
      clearTimeout(this.commentLeaveTimer);
      this.commentLeaveTimer = null;
    }
    if (this.activeCommentClient === cliente) {
      this.isCommentPinned = true;
      setTimeout(() => {
        const activeTextarea = document.getElementById('client-comment-' + cliente.id) as HTMLTextAreaElement;
        if (activeTextarea && document.activeElement !== activeTextarea) {
          activeTextarea.focus();
        }
      }, 0);
    }
  }

  toggleComment(cliente: TableClient): void {
    if (this.commentHoverTimer) {
      clearTimeout(this.commentHoverTimer);
      this.commentHoverTimer = null;
    }
    if (this.commentLeaveTimer) {
      clearTimeout(this.commentLeaveTimer);
      this.commentLeaveTimer = null;
    }

    if (this.activeCommentClient === cliente) {
      if (this.isCommentPinned) {
        this.closeComment(cliente);
      } else {
        this.isCommentPinned = true;
        setTimeout(() => {
          const activeTextarea = document.getElementById('client-comment-' + cliente.id) as HTMLTextAreaElement;
          if (activeTextarea) {
            activeTextarea.focus();
          }
        }, 0);
      }
    } else {
      if (this.activeCommentClient) {
        this.closeComment(this.activeCommentClient);
      }
      this.activeCommentClient = cliente;
      this.isCommentPinned = true;
      setTimeout(() => {
        const activeTextarea = document.getElementById('client-comment-' + cliente.id) as HTMLTextAreaElement;
        if (activeTextarea) {
          activeTextarea.style.height = 'auto';
          activeTextarea.style.height = activeTextarea.scrollHeight + 'px';
          activeTextarea.focus();
        }
      }, 0);
    }
  }

  closeComment(cliente: TableClient): void {
    if (this.commentHoverTimer) {
      clearTimeout(this.commentHoverTimer);
      this.commentHoverTimer = null;
    }
    if (this.commentLeaveTimer) {
      clearTimeout(this.commentLeaveTimer);
      this.commentLeaveTimer = null;
    }
    this.activeCommentClient = null;
    this.isCommentPinned = false;
    this.onClientCommentChange(cliente);
  }

  deleteComment(cliente: TableClient): void {
    cliente.comment = '';
    cliente.commentUpdatedAt = new Date();
    this.closeComment(cliente);
  }

  onClientCommentInput(cliente: TableClient): void {
    cliente.commentUpdatedAt = new Date();
  }

  autoResizeTextarea(event: Event): void {
    const textarea = event.target as HTMLTextAreaElement;
    textarea.style.height = 'auto';
    textarea.style.height = textarea.scrollHeight + 'px';
  }

  onCommentEnter(event: Event, cliente: TableClient): void {
    event.preventDefault();
    this.closeComment(cliente);
  }

  onClientColorChange(cliente: TableClient): void {
    if (cliente.color && cliente.color.toLowerCase() === '#ffffff') {
      cliente.color = null as any;
    }
    this.clientService.updateClientColor(cliente.id, cliente.color).subscribe({
      error: () => Swal.fire('Error', 'No se pudo guardar el color del cliente', 'error')
    });
  }

  resetClientColor(cliente: TableClient): void {
    cliente.color = null as any;
    this.onClientColorChange(cliente);
  }

  getCellStyle(cliente: TableClient, position?: 'first' | 'last'): { [key: string]: string } {
    if (!cliente || !cliente.color) {
      return { 'border-bottom': '1px solid #e5e7eb' };
    }
    const color = cliente.color;
    const topBottomBorder = `1px solid ${color}B3`;
    const styles: { [key: string]: string } = {
      'border-top': topBottomBorder,
      'border-bottom': topBottomBorder
    };
    if (position === 'first') {
      styles['border-left'] = `3px solid ${color}`;
    } else if (position === 'last') {
      styles['border-right'] = topBottomBorder;
    }
    return styles;
  }

  @HostListener('document:click', ['$event'])
  onClickOutside(event: Event): void {
    if (this.activeCommentClient) {
      const target = event.target as HTMLElement;
      if (!target.closest('.note-popup-container') && !target.closest('.note-toggle-btn')) {
        this.closeComment(this.activeCommentClient);
      }
    }
  }

  onClientCommentChange(cliente: TableClient): void {
    if (!cliente.commentUpdatedAt) {
      cliente.commentUpdatedAt = new Date();
    }
    this.clientService.updateClientComment(cliente.id, cliente.comment).subscribe({
      error: () => Swal.fire('Error', 'No se pudo guardar el comentario del cliente', 'error')
    });
  }

  getFormattedUpdatedDate(date?: Date | string | null, fallbackItem?: any): string {
    const rawDate = date || (fallbackItem?.CommentUpdatedAt) || (fallbackItem?.comment_updated_at);
    if (!rawDate) return '';
    const d = new Date(rawDate);
    if (isNaN(d.getTime())) return '';
    const day = String(d.getDate()).padStart(2, '0');
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const year = d.getFullYear();
    const hours = String(d.getHours()).padStart(2, '0');
    const minutes = String(d.getMinutes()).padStart(2, '0');
    return `Modif: ${day}/${month}/${year} ${hours}:${minutes}`;
  }

  isFutureMonthOrLater(date?: Date | string | null): boolean {
    if (!date) return false;
    const d = new Date(date);
    if (isNaN(d.getTime())) return false;
    const now = new Date();
    const dVal = d.getFullYear() * 12 + d.getMonth();
    const nowVal = now.getFullYear() * 12 + now.getMonth();
    return dVal > nowVal;
  }
}

