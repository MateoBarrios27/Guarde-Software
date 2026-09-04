import { AfterViewInit, ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, HostListener, NgZone, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgxPaginationModule } from 'ngx-pagination';
import { IconComponent } from '../../shared/components/icon/icon.component';
import { PhonePipe } from '../../shared/pipes/phone.pipe';
import { CreateClientModalComponent } from '../../shared/components/create-client-modal/create-client-modal.component';

// --- Modelos y Servicios para la TABLA ---
import { TableClient } from '../../core/dtos/client/TableClientDto';
import { GetClientsRequest } from '../../core/dtos/client/GetClientsRequest';
import { ClientDepartureProportionalPreview, ClientService } from '../../core/services/client-service/client.service';
import { ClientDetailDTO } from '../../core/dtos/client/ClientDetailDTO';

import { Subject, Observable, firstValueFrom } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { ClientDetailModalComponent } from "../../shared/components/client-detail-modal/client-detail-modal.component";
import { ClientStatisticsDto } from '../../core/dtos/statistics/ClientStatisticsDto';
import { StatisticsService } from '../../core/services/statics-service/statics-service.service';
import { ɵɵDir } from "@angular/cdk/scrolling";
import { Warehouse } from '../../core/models/warehouse';
import { WarehouseService } from '../../core/services/warehouse-service/warehouse.service';
import { Router, ActivatedRoute } from '@angular/router';
import { BillingType } from '../../core/models/billing-type.model';
import { BillingTypeService } from '../../core/services/billingType-service/billing-type.service';
import { PaymentMethod } from '../../core/models/payment-method';
import { PaymentMethodService } from '../../core/services/paymentMethod-service/payment-method.service';
import { LockerTypeService } from '../../core/services/lockerType-service/locker-type.service';
import { OfflineService } from '../../core/services/offline-service/offline.service';
import { IndexedDbService } from '../../core/services/offline-service/indexed-db.service';
import Swal from '../../shared/services/ui-alert.service';
import { ToastNotificationComponent } from '../../shared/components/toast-notification/toast-notification.component';

@Component({
  selector: 'app-clients',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    IconComponent,
    NgxPaginationModule,
    CreateClientModalComponent,
    ClientDetailModalComponent,
    ToastNotificationComponent
  ],
  templateUrl: './clients.component.html',
  styleUrl: './clients.component.css',
  host: {
    class: 'block w-full min-w-0'
  },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClientsComponent implements OnInit, AfterViewInit, OnDestroy {

  // --- Table properties ---
  public activeTab: 'clientes' | 'pagos' = 'clientes';
  public clientes: TableClient[] = [];
  public totalClientes = 0;
  public isLoading = true;
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
  public itemsPerPageClientes = 10000;
  // public itemsPerPageOptions = [50, 100, 200, 400];
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
  public selectedLockerTypeIds: number[] = [];
  public showTagsPopover = false;

  public activeDepartureClient: TableClient | null = null;
  public departureAction: 'SE_VA' | 'SE_QUEDA' | 'DAR_DE_BAJA' = 'SE_VA';
  public departureChargeProportional = false;
  public departureRemoveNextMonthDebit = false;
  public departureRestoreProportional = true;
  public departureDate = '';
  public departurePendingSurchargeAction: 'forgive' | 'immediate' = 'forgive';
  public departureFormError = '';
  public departureSubmitting = false;
  public departureProportionalPreview: ClientDepartureProportionalPreview | null = null;
  public departureProportionalPreviewLoading = false;
  private departurePreviewRequestId = 0;

  @ViewChild('tagsPopoverRef') tagsPopoverRef!: ElementRef;
  @ViewChild('tagsButtonRef') tagsButtonRef!: ElementRef;

  totals = {
    previousBalance: 0,
    interestAmount: 0,
    currentRent: 0,
    balance: 0,
    activePaymentIdentifiers: 0,
    currentRentWithActivePaymentIdentifiers: 0,
    balanceWithActivePaymentIdentifiers: 0
  };

  @ViewChild('topAnchor') topAnchor!: ElementRef;
  @ViewChild('bottomAnchor') bottomAnchor!: ElementRef;
  
  pointingUp: boolean = false; 
  private scrollObserver!: IntersectionObserver;
  private supportingDataLoadScheduled = false;
  private clientIdToPositionFromQuery: number | null = null;
  private detailClientIdFromQuery: number | null = null;

  constructor(
    private clientService: ClientService, 
    private statisticsService: StatisticsService, 
    private warehouseService: WarehouseService, 
    private billingTypeService: BillingTypeService,
    private paymentMethodService: PaymentMethodService,
    private lockerTypeService: LockerTypeService,
    private router: Router,
    private route: ActivatedRoute,
    public offlineService: OfflineService,
    private idb: IndexedDbService,
    private cdr: ChangeDetectorRef,
    private ngZone: NgZone
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
    const target = event.target as HTMLElement;

    if (this.showTagsPopover) {
      const clickedInsidePopover = this.tagsPopoverRef && this.tagsPopoverRef.nativeElement.contains(target);
      const clickedInsideButton = this.tagsButtonRef && this.tagsButtonRef.nativeElement.contains(target);
      if (!clickedInsidePopover && !clickedInsideButton) {
        this.showTagsPopover = false;
      }
    }

    if (this.activeDepartureClient && !target.closest('[data-departure-popover], [data-departure-trigger]')) {
      this.closeDeparturePopover();
    }
    if (this.showTagsPopover || this.activeDepartureClient) {
      this.cdr.markForCheck();
    }
  }

  public isClientLeaving(cliente: TableClient): boolean {
    return cliente.departureStatus === 'SE_VA' || cliente.status === 'SE VA';
  }

  public toggleDeparturePopover(cliente: TableClient): void {
    if (this.activeDepartureClient?.id === cliente.id) {
      this.closeDeparturePopover();
      return;
    }

    this.activeDepartureClient = cliente;
    this.departureAction = this.isClientLeaving(cliente) ? 'SE_QUEDA' : 'SE_VA';
    this.departureChargeProportional = false;
    this.departureRemoveNextMonthDebit = false;
    this.departureRestoreProportional = true;
    // La fecha debe ser elegida explícitamente cuando se activa el proporcional.
    this.departureDate = '';
    this.departurePendingSurchargeAction = 'forgive';
    this.departureFormError = '';
    this.departureSubmitting = false;
    this.clearDepartureProportionalPreview();
    this.cdr.markForCheck();
  }

  public closeDeparturePopover(): void {
    this.activeDepartureClient = null;
    this.departureFormError = '';
    this.departureSubmitting = false;
    this.clearDepartureProportionalPreview();
    this.cdr.markForCheck();
  }

  public selectDepartureAction(action: 'SE_VA' | 'SE_QUEDA' | 'DAR_DE_BAJA'): void {
    this.departureAction = action;
    this.departureFormError = '';
    this.clearDepartureProportionalPreview();
    if (action === 'SE_QUEDA') {
      this.departureChargeProportional = false;
      this.departureRemoveNextMonthDebit = false;
      this.departureRestoreProportional = true;
    } else {
      this.departureRestoreProportional = false;
    }
    this.cdr.markForCheck();
  }

  public onDepartureProportionalChange(event: Event): void {
    this.departureChargeProportional = (event.target as HTMLInputElement).checked;
    this.departureFormError = '';
    if (this.departureChargeProportional) {
      // El proporcional reemplaza al débito mensual completo del mes siguiente.
      this.departureRemoveNextMonthDebit = true;
      this.loadDepartureProportionalPreview();
    } else {
      this.clearDepartureProportionalPreview();
    }
    this.cdr.markForCheck();
  }

  public onDepartureDateChange(value: string): void {
    this.departureDate = value;
    this.departureFormError = '';
    if (this.departureChargeProportional) {
      this.loadDepartureProportionalPreview();
    } else {
      this.clearDepartureProportionalPreview();
    }
  }

  public getNextMonthDateInputMin(): string {
    const today = new Date();
    return this.toDateInputValue(new Date(today.getFullYear(), today.getMonth() + 1, 1));
  }

  public getNextMonthDateInputMax(): string {
    const today = new Date();
    return this.toDateInputValue(new Date(today.getFullYear(), today.getMonth() + 2, 0));
  }

  public getDepartureProportionalDays(): number {
    const date = this.parseDateInput(this.departureDate);
    return date ? date.getDate() : 0;
  }

  public getDepartureProportionalAmount(cliente: TableClient): number {
    if (this.activeDepartureClient?.id !== cliente.id) return 0;
    return this.departureProportionalPreview?.proportionalAmount ?? 0;
  }

  private async loadDepartureProportionalPreview(): Promise<void> {
    const cliente = this.activeDepartureClient;
    const departureDate = this.departureDate;
    if (!cliente || !this.departureChargeProportional || !departureDate) {
      this.clearDepartureProportionalPreview();
      this.cdr.markForCheck();
      return;
    }

    const requestId = ++this.departurePreviewRequestId;
    this.departureProportionalPreview = null;
    this.departureProportionalPreviewLoading = true;
    this.cdr.markForCheck();

    try {
      const preview = await firstValueFrom(
        this.clientService.getDepartureProportionalPreview(cliente.id, departureDate)
      );
      if (requestId !== this.departurePreviewRequestId) return;
      this.departureProportionalPreview = preview;
    } catch (error) {
      if (requestId !== this.departurePreviewRequestId) return;
      console.error('Error al calcular el proporcional de salida:', error);
      this.departureFormError = 'No se pudo calcular el proporcional para esa fecha.';
    } finally {
      if (requestId === this.departurePreviewRequestId) {
        this.departureProportionalPreviewLoading = false;
        this.cdr.markForCheck();
      }
    }
  }

  private clearDepartureProportionalPreview(): void {
    this.departurePreviewRequestId++;
    this.departureProportionalPreview = null;
    this.departureProportionalPreviewLoading = false;
  }

  public applyDepartureFromPopover(event: Event): void {
    event.stopPropagation();
    const cliente = this.activeDepartureClient;
    if (!cliente || this.departureSubmitting) return;

    if (this.departureAction !== 'SE_QUEDA' && this.departureChargeProportional) {
      const selectedDate = this.parseDateInput(this.departureDate);
      const minDate = this.parseDateInput(this.getNextMonthDateInputMin());
      const maxDate = this.parseDateInput(this.getNextMonthDateInputMax());
      if (!selectedDate || !minDate || !maxDate || selectedDate < minDate || selectedDate > maxDate) {
        this.departureFormError = 'Elegí una fecha válida dentro del mes siguiente.';
        this.cdr.markForCheck();
        return;
      }
    }

    const action = this.departureAction;
    const departureDate = this.departureChargeProportional ? this.departureDate : undefined;
    this.submitDepartureAction(
      cliente,
      action,
      this.departureChargeProportional,
      this.departureRemoveNextMonthDebit,
      this.departurePendingSurchargeAction,
      departureDate
    );
  }

  private toDateInputValue(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private parseDateInput(value: string): Date | null {
    if (!value) return null;
    const [year, month, day] = value.split('-').map(Number);
    if (!year || !month || !day) return null;
    return new Date(year, month - 1, day);
  }

  private submitDepartureAction(
    cliente: TableClient,
    action: 'SE_VA' | 'SE_QUEDA' | 'DAR_DE_BAJA',
    chargeProportional: boolean,
    removeNextMonthDebit: boolean,
    pendingSurchargeAction?: 'forgive' | 'immediate',
    departureDate?: string
  ): void {
    this.departureSubmitting = true;
    this.departureFormError = '';
    this.cdr.markForCheck();
    this.clientService.applyDepartureAction(cliente.id, {
      action,
      chargeProportional,
      removeNextMonthDebit,
      restoreProportional: action === 'SE_QUEDA' && this.departureRestoreProportional,
      departureDate,
      pendingSurchargeAction
    }).subscribe({
      next: () => {
        this.departureSubmitting = false;
        this.closeDeparturePopover();
        const successText = action === 'SE_VA'
          ? 'El cliente quedó marcado como SE VA y sus bauleras quedaron POR LIBERARSE.'
          : action === 'SE_QUEDA'
            ? 'El cliente quedó como SE QUEDA y sus bauleras volvieron a OCUPADO.'
            : 'El cliente fue dado de baja y sus bauleras fueron liberadas.';
        this.showToastNotification(successText, 'success');
        this.loadClients();
      },
      error: (err) => {
        this.departureSubmitting = false;
        this.departureFormError = err.error?.message || 'Ocurrió un error al actualizar la situación del cliente.';
        this.cdr.markForCheck();
      },
    });
  }

  goToPayment(clientId: number) {
    this.router.navigate(['/finances'], { 
      queryParams: { 
        autoOpenPayment: clientId, 
        returnTo: 'clients',
        searchTerm: this.searchClientes || ''
      } 
    });
  }

  public lockerTypes: any[] = [];

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      const searchTerm = params['searchTerm'];
      if (searchTerm) {
        this.searchClientes = searchTerm;
      }

      const clientId = Number(params['clientId'] ?? 0);
      const detailClientId = Number(params['detailClientId'] ?? 0);
      this.clientIdToPositionFromQuery = clientId > 0 ? clientId : null;
      this.detailClientIdFromQuery = detailClientId > 0 ? detailClientId : null;
      this.handleClientNavigationQuery();
      
      void this.loadClients().finally(() => {
        this.scheduleSupportingDataLoad();
        this.handleClientNavigationQuery();
      });
    });

    this.loadStatistics();
  }

  private handleClientNavigationQuery(): void {
    const positionClientId = this.clientIdToPositionFromQuery;
    const detailClientId = this.detailClientIdFromQuery;
    if (!positionClientId && !detailClientId) return;

    this.clientIdToPositionFromQuery = null;
    this.detailClientIdFromQuery = null;
    if (detailClientId) {
      this.openDetailClientModal(detailClientId);
    } else if (positionClientId) {
      this.positionClientInTable(positionClientId);
    }
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { clientId: null, detailClientId: null },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }

  private positionClientInTable(clientId: number): void {
    const rowId = `client-row-${clientId}`;
    const tryPosition = (attempt = 0): void => {
      const row = document.getElementById(rowId);
      if (row) {
        row.scrollIntoView({ behavior: 'smooth', block: 'center' });
        row.classList.add('ring-2', 'ring-blue-400', 'bg-blue-50');
        window.setTimeout(() => row.classList.remove('ring-2', 'ring-blue-400', 'bg-blue-50'), 2200);
        return;
      }
      if (attempt < 8) window.setTimeout(() => tryPosition(attempt + 1), 100);
    };
    tryPosition();
  }

  private scheduleSupportingDataLoad(): void {
    if (this.supportingDataLoadScheduled) return;
    this.supportingDataLoadScheduled = true;

    const load = () => {
      this.warehouseService.getWarehouses().subscribe(data => { this.warehouses = data; this.cdr.markForCheck(); });
      this.billingTypeService.getBillingTypes().subscribe(data => { this.billingTypes = data; this.cdr.markForCheck(); });
      this.paymentMethodService.getPaymentMethods().subscribe(data => { this.paymentMethods = data; this.cdr.markForCheck(); });
      this.lockerTypeService.getLockerTypes().subscribe(data => { this.lockerTypes = data; this.cdr.markForCheck(); });
    };

    if ('requestIdleCallback' in window) {
      window.requestIdleCallback(load, { timeout: 2500 });
    } else {
      setTimeout(load, 500);
    }
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

  toggleLockerTypeId(id: number): void {
    const idx = this.selectedLockerTypeIds.indexOf(id);
    if (idx > -1) {
      this.selectedLockerTypeIds.splice(idx, 1);
    } else {
      this.selectedLockerTypeIds.push(id);
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
    this.selectedLockerTypeIds = [];
    this.currentPageClientes = 1;
    this.loadClients();
  }

  get totalActiveTagsCount(): number {
    return (
      this.selectedWarehouseIds.length +
      this.selectedQuickFilters.length +
      this.selectedIvaConditions.length +
      this.selectedBillingTypeIds.length +
      this.selectedPaymentMethodIds.length +
      this.selectedLockerTypeIds.length
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

  getLockerTypeName(id: number): string {
    const lt = this.lockerTypes.find(t => t.id === id);
    return lt ? lt.name : `Tipo (${id})`;
  }
  ngAfterViewInit() {
    setTimeout(() => {
      const scrollContainer = document.getElementById('main-scroll');
      if (scrollContainer) {
        scrollContainer.addEventListener('scroll', this.onScroll.bind(this));
        this.onScroll({ target: scrollContainer } as any); // Initialize
      }
    }, 100);
  }

  onScroll(event: Event) {
    const target = event.target as HTMLElement;
    // Si estamos cerca del fondo, la flecha apunta hacia arriba
    const isAtBottom = target.scrollTop + target.clientHeight >= target.scrollHeight - 100;
    this.pointingUp = isAtBottom;
  }

  ngOnDestroy() {
    const scrollContainer = document.getElementById('main-scroll');
    if (scrollContainer) {
      scrollContainer.removeEventListener('scroll', this.onScroll.bind(this));
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

   async loadClients(): Promise<void> {
    this.isLoading = true;
    this.cdr.markForCheck();

    if (!this.offlineService.isOnline) {
      // Load from IndexedDB cache
      const cached = await this.idb.getCachedClients();
      
      // We map the cached Client model (which is simple) to the TableClient format as best as possible
      let filtered = cached.map(c => ({
        id: c.id,
        fullName: c.fullName,
        paymentIdentifier: c.paymentIdentifier ?? 0,
        balance: c.balance ?? 0,
        currentRent: c.currentRent ?? 0,
        previousBalance: c.previousBalance ?? 0,
        pendingSurcharge: c.pendingSurcharge ?? 0,
        departureStatus: c.departureStatus ?? null,
        interestAmount: c.interestAmount ?? 0,
        lastGeneratedMonthYear: c.lastGeneratedMonthYear,
        color: c.color,
        status: c.status ?? (c.active ? 'Al día' : 'Baja'),
        nextPaymentDay: c.nextPaymentDay ? new Date(c.nextPaymentDay) : null,
        active: c.active ?? true,
        phone1: '',
        email: '',
        lockers: [],
        registrationDate: new Date(),
        documentType: '',
        documentNumber: '',
        comment: '',
        commentUpdatedAt: new Date(),
        billingTypeId: 0,
        ivaCondition: '',
        preferredPaymentMethodId: c.preferredPaymentMethodId ?? 0,
        dni: '',
        cuit: ''
      } as unknown as TableClient));

      // Apply basic search filter if present
      if (this.searchClientes) {
        const term = this.searchClientes.toLowerCase();
        filtered = filtered.filter(c => 
          c.fullName.toLowerCase().includes(term) || 
          (c.paymentIdentifier?.toString().includes(term) ?? false)
        );
      }

      this.clientes = filtered;
      this.totalClientes = filtered.length;
      this.precomputeClientProps();
      this.calculateTotals();
      this.isLoading = false;
      this.cdr.markForCheck();
      return;
    }

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
      preferredPaymentMethodIds: this.selectedPaymentMethodIds.length > 0 ? this.selectedPaymentMethodIds : undefined,
      lockerTypeIds: this.selectedLockerTypeIds.length > 0 ? this.selectedLockerTypeIds : undefined
    };

    try {
      const result = await firstValueFrom(this.clientService.getTableClients(request));
      this.clientes = result.items;
      this.totalClientes = result.totalCount;
      this.precomputeClientProps();
      this.calculateTotals();
      this.isLoading = false;
      this.cdr.markForCheck();
    } catch (err) {
      console.error('Error al cargar clientes:', err);
      this.isLoading = false;
      this.cdr.markForCheck();
    }
  }

  get totalPages(): number {
    return Math.ceil(this.totalClientes / this.itemsPerPageClientes);
  }

  calculateTotals(): void {
    this.totals = {
      previousBalance: 0,
      interestAmount: 0,
      currentRent: 0,
      balance: 0,
      activePaymentIdentifiers: 0,
      currentRentWithActivePaymentIdentifiers: 0,
      balanceWithActivePaymentIdentifiers: 0
    };

    // Recorremos el array 'clientes' que es el que se muestra en la tabla
    this.clientes.forEach(cliente => {
      this.totals.previousBalance += Number(cliente.previousBalance) || 0;
      this.totals.interestAmount += Number(cliente.interestAmount) || 0;
      this.totals.currentRent += Number(cliente.currentRent) || 0;
      this.totals.balance += Number(cliente.balance) || 0;

      if (cliente.active !== false && cliente.status !== 'Baja') {
        this.totals.activePaymentIdentifiers += Number(cliente.paymentIdentifier) || 0;
      }
    });

    this.totals.currentRentWithActivePaymentIdentifiers =
      this.totals.currentRent + this.totals.activePaymentIdentifiers - 0.02;
    this.totals.balanceWithActivePaymentIdentifiers =
      this.totals.balance - this.totals.activePaymentIdentifiers;
  }

  trackByClientId(index: number, cliente: TableClient): number {
    return cliente.id;
  }

  private precomputeClientProps(): void {
    const now = new Date();
    const nowVal = now.getFullYear() * 12 + now.getMonth();
    for (const c of this.clientes) {
      // Precompute isFutureMonth
      if (c.nextPaymentDay) {
        const d = new Date(c.nextPaymentDay);
        const dVal = d.getFullYear() * 12 + d.getMonth();
        c._isFutureMonth = dVal > nowVal;
      } else {
        c._isFutureMonth = false;
      }
      // Precompute color styles
      if (c.color) {
        c._bgColor = c.color + '15';
        c._colorLight = c.color + 'B3';
      } else {
        c._bgColor = '';
        c._colorLight = null;
      }
    }
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

    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { searchTerm: this.searchClientes || null },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
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
      this.cdr.markForCheck();
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
    this.cdr.markForCheck();
    setTimeout(() => {
      this.showToast = false;
      this.cdr.markForCheck();
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
        this.cdr.markForCheck();
      });
  }

  closeDetailClientModal(): void {
    this.showDetailClientModal = false;
    this.clientToView = null;
  }

  loadStatistics(): void {
    this.statisticsService.getClientStatistics().subscribe({
      next: (stats) => {
        this.estadisticas = stats;
        this.cdr.markForCheck();
      },
      error: (err) => console.error('Error cargando estadísticas:', err)
    });
  }

  public async onReactivateClient(cliente: TableClient): Promise<void> {
    const { default: Swal } = await import('../../shared/services/ui-alert.service');
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
      confirmButtonColor: '#2563eb',
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
      this.cdr.markForCheck();
    });
  }

  onCommentMouseEnter(cliente: TableClient): void {
    this.ngZone.runOutsideAngular(() => {
      if (this.commentLeaveTimer) {
        clearTimeout(this.commentLeaveTimer);
        this.commentLeaveTimer = null;
      }

      if (this.activeCommentClient === cliente) return;

      if (this.commentHoverTimer) {
        clearTimeout(this.commentHoverTimer);
      }

      this.commentHoverTimer = setTimeout(() => {
        this.ngZone.run(() => {
          if (this.activeCommentClient && !this.isCommentPinned && this.activeCommentClient !== cliente) {
            this.closeComment(this.activeCommentClient);
          }
          this.activeCommentClient = cliente;
          this.isCommentPinned = false;
          this.cdr.markForCheck();
          setTimeout(() => {
            const activeTextarea = document.getElementById('client-comment-' + cliente.id) as HTMLTextAreaElement;
            if (activeTextarea) {
              activeTextarea.style.height = 'auto';
              activeTextarea.style.height = activeTextarea.scrollHeight + 'px';
            }
          }, 0);
        });
      }, 400);
    });
  }

  onCommentMouseLeave(cliente: TableClient): void {
    this.ngZone.runOutsideAngular(() => {
      if (this.commentHoverTimer) {
        clearTimeout(this.commentHoverTimer);
        this.commentHoverTimer = null;
      }

      if (this.activeCommentClient === cliente && !this.isCommentPinned) {
        if (this.commentLeaveTimer) {
          clearTimeout(this.commentLeaveTimer);
        }
        this.commentLeaveTimer = setTimeout(() => {
          this.ngZone.run(() => {
            if (this.activeCommentClient === cliente && !this.isCommentPinned) {
              this.closeComment(cliente);
              this.cdr.markForCheck();
            }
          });
        }, 200);
      }
    });
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
    // Recompute precomputed color styles
    if (cliente.color) {
      cliente._bgColor = cliente.color + '15';
      cliente._colorLight = cliente.color + 'B3';
    } else {
      cliente._bgColor = '';
      cliente._colorLight = null;
    }
    this.cdr.markForCheck();
    this.clientService.updateClientColor(cliente.id, cliente.color).subscribe({
      error: () => { void this.showClientError('No se pudo guardar el color del cliente'); }
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
      error: () => { void this.showClientError('No se pudo guardar el comentario del cliente'); }
    });
  }

  private async showClientError(message: string): Promise<void> {
    const { default: Swal } = await import('../../shared/services/ui-alert.service');
    await Swal.fire('Error', message, 'error');
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

