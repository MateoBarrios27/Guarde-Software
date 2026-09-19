import { AfterViewInit, Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { Locker, LockerClientSummary } from '../../core/models/locker';
import { LockerService } from '../../core/services/locker-service/locker.service';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Warehouse } from '../../core/models/warehouse';
import { WarehouseService } from '../../core/services/warehouse-service/warehouse.service';
import { NgxPaginationModule } from 'ngx-pagination';
import { IconComponent } from '../../shared/components/icon/icon.component';
import { LockerUpdateDTO } from '../../core/dtos/locker/LockerUpdateDTO';
import Swal from '../../shared/services/ui-alert.service';
import { DeleteConfirmationService } from '../../shared/services/delete-confirmation.service';

// --- NUEVAS IMPORTACIONES ---
import { LockerType } from '../../core/models/locker-type';
import { LockerTypeService } from '../../core/services/lockerType-service/locker-type.service';
import { CreateLockerDTO } from '../../core/dtos/locker/CreateLockerDTO';
import { Subscription } from 'rxjs';
import { DataRefreshService } from '../../core/services/data-refresh-service/data-refresh.service';
import { ClientService } from '../../core/services/client-service/client.service';
import { Client } from '../../core/models/client';
import { NavigationCancel, NavigationEnd, NavigationError, NavigationStart, Router } from '@angular/router';
import { CdkConnectedOverlay, CdkOverlayOrigin, ConnectedPosition, Overlay, OverlayModule } from '@angular/cdk/overlay';

type LockerFilterGroup = 'warehouse' | 'status' | 'lockerType';
type LockerFilterState = 'none' | 'include' | 'exclude';
interface LockerClientTarget {
  client: LockerClientSummary;
  lockerId: number;
}

@Component({
  selector: 'app-lockers',
  standalone: true,
  imports: [CommonModule, FormsModule, NgxPaginationModule, IconComponent, OverlayModule],
  templateUrl: './lockers.component.html',
  styleUrls: ['./lockers.component.css'],
  host: {
    class: 'block w-full min-w-0'
  }
})
export class LockersComponent implements OnInit, AfterViewInit, OnDestroy {
  lockers: Locker[] = [];
  selectedLocker: Locker | null = null;
  
  warehouses: Warehouse[] = [];
  lockerTypes: LockerType[] = [];

  // --- Modal de Actualización ---
  lockerUpdate: LockerUpdateDTO = {
    identifier: '',
    status: '',
    features: '',
    lockerTypeId: 0,
    warehouseId: 0,
    isFreeSpace: false
  };
  lockerOriginal: LockerUpdateDTO = { ...this.lockerUpdate }; 
  showUpdateLockerModal = false;
  idLockerUpdated = 0;
  warehouseId = 0; 
  selectedLockerForEdit: Locker | null = null;

  // --- Modal de Creación  ---
  public showCreateLockerModal = false;
  public newLocker: CreateLockerDTO = this.getDefaultNewLocker();

  // filtros
  searchTerm = '';
  selectedWarehouse = '';
  selectedStatus = '';

  // sorting
  public sortField: string = 'identifier';
  public sortDirection: 'asc' | 'desc' = 'asc';

  // popover filters
  public showTagsPopover = false;
  public statusList: string[] = ['DISPONIBLE', 'OCUPADO', 'POR LIBERARSE', 'MANTENIMIENTO'];
  public selectedWarehouseIds: number[] = [];
  public excludedWarehouseIds: number[] = [];
  public selectedStatuses: string[] = [];
  public excludedStatuses: string[] = [];
  public selectedLockerTypeIds: number[] = [];
  public excludedLockerTypeIds: number[] = [];

  clients: Client[] = [];
  clientStatsOrigin: CdkOverlayOrigin | null = null;
  clientStatsTarget: LockerClientTarget | null = null;
  clientStatsOpen = false;
  @ViewChild('clientStatsOverlay') private clientStatsOverlay?: CdkConnectedOverlay;
  private clientStatsNavigationPending = false;
  readonly clientStatsScrollStrategy;
  readonly clientStatsPositions: ConnectedPosition[] = [
    { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 0 },
    { originX: 'start', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: 0 },
    { originX: 'end', originY: 'bottom', overlayX: 'end', overlayY: 'top', offsetY: 0 },
    { originX: 'end', originY: 'top', overlayX: 'end', overlayY: 'bottom', offsetY: 0 },
  ];
  private clientStatsOpenTimer?: ReturnType<typeof setTimeout>;
  private clientStatsCloseTimer?: ReturnType<typeof setTimeout>;

  @ViewChild('tagsPopoverRef') tagsPopoverRef!: ElementRef;
  @ViewChild('tagsButtonRef') tagsButtonRef!: ElementRef;

  page: number = 1;
  itemsPerPage: number = 500; 

  // --- VARIABLES PARA EL BOTÓN FLOTANTE ---
  @ViewChild('bottomAnchor') bottomAnchor!: ElementRef;
  pointingUp: boolean = false;
  private scrollObserver!: IntersectionObserver;
  private readonly dataRefreshSubscription = new Subscription();

  constructor(
    private lockerService: LockerService,
    private warehouseService: WarehouseService,
    private lockerTypeService: LockerTypeService,
    private clientService: ClientService,
    private deleteConfirmation: DeleteConfirmationService,
    private dataRefresh: DataRefreshService,
    private router: Router,
    overlay: Overlay,
  ) {
    this.clientStatsScrollStrategy = overlay.scrollStrategies.close();
    this.dataRefreshSubscription.add(this.router.events.subscribe(event => {
      if (event instanceof NavigationStart) {
        this.clientStatsNavigationPending = true;
        this.closeClientStats();
      } else if (event instanceof NavigationEnd || event instanceof NavigationCancel || event instanceof NavigationError) {
        this.clientStatsNavigationPending = false;
      }
    }));
  }

  ngOnInit(): void {
    this.dataRefreshSubscription.add(
      this.dataRefresh.watch(['lockers', 'clients', 'catalog'], 'lockers').subscribe(event => {
        if (event.domains.includes('catalog')) {
          this.loadWarehouses();
          this.loadLockerTypes();
        }
        if (event.domains.includes('lockers') || event.domains.includes('clients')) {
          this.loadLockers();
        }
        if (event.domains.includes('clients')) {
          this.loadClientDirectory();
        }
      }),
    );
    this.loadLockers();
    this.loadWarehouses();
    this.loadLockerTypes();
    this.loadClientDirectory();
  }

  ngAfterViewInit() {
    this.scrollObserver = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        if (entry.isIntersecting) {
          this.pointingUp = true;
        } else {
          this.pointingUp = false;
        }
      });
    }, { threshold: 0 });

    if (this.bottomAnchor) {
      this.scrollObserver.observe(this.bottomAnchor.nativeElement);
    }
  }

  ngOnDestroy() {
    this.closeClientStats();
    this.dataRefreshSubscription.unsubscribe();
    if (this.scrollObserver) {
      this.scrollObserver.disconnect();
    }
  }  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.showTagsPopover) return;
    const clickedInsidePopover = this.tagsPopoverRef && this.tagsPopoverRef.nativeElement.contains(event.target);
    const clickedInsideButton = this.tagsButtonRef && this.tagsButtonRef.nativeElement.contains(event.target);
    if (!clickedInsidePopover && !clickedInsideButton) {
      this.showTagsPopover = false;
    }
  }

  toggleTagsPopover(): void {
    this.showTagsPopover = !this.showTagsPopover;
  }

  toggleWarehouseId(id: number): void {
    const next = this.cycleFilterSelection(id, this.selectedWarehouseIds, this.excludedWarehouseIds);
    this.selectedWarehouseIds = next.included;
    this.excludedWarehouseIds = next.excluded;
    this.page = 1;
  }

  toggleStatus(status: string): void {
    const next = this.cycleFilterSelection(status, this.selectedStatuses, this.excludedStatuses);
    this.selectedStatuses = next.included;
    this.excludedStatuses = next.excluded;
    this.page = 1;
  }

  toggleLockerTypeId(id: number): void {
    const next = this.cycleFilterSelection(id, this.selectedLockerTypeIds, this.excludedLockerTypeIds);
    this.selectedLockerTypeIds = next.included;
    this.excludedLockerTypeIds = next.excluded;
    this.page = 1;
  }

  private cycleFilterSelection<T>(value: T, included: T[], excluded: T[]): { included: T[]; excluded: T[] } {
    if (included.includes(value)) {
      return { included: included.filter(item => item !== value), excluded: [...excluded, value] };
    }
    if (excluded.includes(value)) {
      return { included: [...included], excluded: excluded.filter(item => item !== value) };
    }
    return { included: [...included, value], excluded: [...excluded] };
  }

  getFilterTagState(group: LockerFilterGroup, value: string | number): LockerFilterState {
    const collections = this.getFilterTagCollections(group);
    if (collections.included.includes(value)) return 'include';
    if (collections.excluded.includes(value)) return 'exclude';
    return 'none';
  }

  getFilterTagClasses(group: LockerFilterGroup, value: string | number): string {
    const state = this.getFilterTagState(group, value);
    if (state === 'include') return 'bg-blue-600 text-white border-blue-600 font-medium shadow-sm';
    if (state === 'exclude') return 'bg-red-600 text-white border-red-600 font-medium shadow-sm';
    return 'bg-white text-gray-700 border-gray-300 hover:bg-gray-50';
  }

  getActiveFilterTagClasses(group: LockerFilterGroup, value: string | number): string {
    return this.getFilterTagState(group, value) === 'exclude'
      ? 'bg-red-50 text-red-700 border-red-200'
      : 'bg-blue-50 text-blue-700 border-blue-200';
  }

  getFilterTagTitle(group: LockerFilterGroup, value: string | number): string {
    const label = this.getFilterTagLabel(group, value);
    const state = this.getFilterTagState(group, value);
    if (state === 'include') return `${label}: incluido. Segundo click para excluirlo.`;
    if (state === 'exclude') return `${label}: excluido. Tercer click para desmarcarlo.`;
    return `${label}: primer click para incluirlo.`;
  }

  clearFilterTag(group: LockerFilterGroup, value: string | number): void {
    const collections = this.getFilterTagCollections(group);
    const included = collections.included.filter(item => item !== value);
    const excluded = collections.excluded.filter(item => item !== value);
    if (group === 'warehouse') {
      this.selectedWarehouseIds = included as number[];
      this.excludedWarehouseIds = excluded as number[];
    } else if (group === 'status') {
      this.selectedStatuses = included as string[];
      this.excludedStatuses = excluded as string[];
    } else {
      this.selectedLockerTypeIds = included as number[];
      this.excludedLockerTypeIds = excluded as number[];
    }
    this.page = 1;
  }

  private getFilterTagCollections(group: LockerFilterGroup): { included: (string | number)[]; excluded: (string | number)[] } {
    if (group === 'warehouse') return { included: this.selectedWarehouseIds, excluded: this.excludedWarehouseIds };
    if (group === 'status') return { included: this.selectedStatuses, excluded: this.excludedStatuses };
    return { included: this.selectedLockerTypeIds, excluded: this.excludedLockerTypeIds };
  }

  private getFilterTagLabel(group: LockerFilterGroup, value: string | number): string {
    if (group === 'warehouse') return `Depósito ${this.getWarehouseName(Number(value))}`;
    if (group === 'status') return `Estado ${String(value)}`;
    return `Tipo ${this.getLockerTypeName(Number(value))}`;
  }

  clearAllTags(): void {
    this.selectedWarehouseIds = [];
    this.excludedWarehouseIds = [];
    this.selectedStatuses = [];
    this.excludedStatuses = [];
    this.selectedLockerTypeIds = [];
    this.excludedLockerTypeIds = [];
    this.page = 1;
  }

  get totalActiveTagsCount(): number {
    return (
      this.selectedWarehouseIds.length +
      this.excludedWarehouseIds.length +
      this.selectedStatuses.length +
      this.excludedStatuses.length +
      this.selectedLockerTypeIds.length +
      this.excludedLockerTypeIds.length
    );
  }

  handleSort(field: string): void {
    if (this.sortField === field) {
      if (this.sortDirection === 'asc') {
        this.sortDirection = 'desc';
      } else {
        this.sortField = 'identifier';
        this.sortDirection = 'asc';
      }
    } else {
      this.sortField = field;
      this.sortDirection = 'asc';
    }
  }

  getSortIcon(field: string): string {
    if (this.sortField !== field) {
      return 'arrow-up-down';
    }
    return this.sortDirection === 'asc' ? 'arrow-up' : 'arrow-down';
  }

  toggleScroll() {
    const scrollContainer = document.getElementById('main-scroll');
    if (!scrollContainer) return;

    if (this.pointingUp) {
      // 1. Si apunta arriba: Subimos al tope de un saque
      scrollContainer.scrollTo({ top: 0, behavior: 'smooth' });
    } else {
      // 2. Si apunta abajo: Bajamos en bloques (ej: 70 bauleras = ~3850px)
      const scrollAmount = 3850;
      scrollContainer.scrollBy({ top: scrollAmount, behavior: 'smooth' });
    }
  }

  loadLockers(): void {
    this.lockerService.getLockers().subscribe({
      next: (data) => { this.lockers = data; },
      error: (err) => { console.error('Error cargando lockers', err); }
    });
  }

  loadWarehouses(): void {
    this.warehouseService.getWarehouses().subscribe({
      next: (data) => { this.warehouses = data; },
      error: (err) => { console.error('Error al cargar warehouses', err); }
    });
  }

  loadLockerTypes(): void {
    this.lockerTypeService.getLockerTypes().subscribe({
      next: (data) => { this.lockerTypes = data; },
      error: (err) => { console.error('Error al cargar locker types', err); }
    });
  }
  
  async deleteLocker(locker: Locker): Promise<void> {
    if (!locker.id) return;

    if(locker.status == 'OCUPADO') {
      Swal.fire({
          icon: 'warning',
          title: 'Error al eliminar locker',
          text: 'No puedes dar de baja un locker actualmente ocupado.',
          confirmButtonText: 'Entendido',
          confirmButtonColor: '#2563eb'
        });
      return;
    }

    const confirmed = await this.deleteConfirmation.confirm({
      headerTitle: 'Confirmar Baja',
      title: '¿Seguro que quieres dar de baja la baulera?',
      message: 'Se dará de baja la baulera',
      highlightedText: `${this.getWarehouseName(locker.warehouseId)} · ${locker.identifier}`,
      messageSuffix: '.',
      confirmText: 'Confirmar Baja'
    });
    if (!confirmed) return;

    this.lockerService.deleteLocker(locker.id).subscribe({
      next: () => {
        Swal.fire({
          title: 'Baulera eliminada',
          text: 'La baulera fue dada de baja correctamente.',
          icon: 'success',
          confirmButtonText: 'Aceptar',
          confirmButtonColor: '#2563eb'
        });
        setTimeout(() => this.loadLockers(), 100);
      },
      error: (err) => {
        console.error('Error deleting locker', err);
        Swal.fire({
          title: 'Error',
          text: 'Hubo un problema al dar de baja la baulera.',
          icon: 'error',
          confirmButtonText: 'Aceptar',
          confirmButtonColor: '#2563eb'
        });
      }
    });
  }

  loadClientDirectory(): void {
    this.clientService.getClients().subscribe({
      next: data => {
        this.clients = data;
      },
      error: err => console.error('Error cargando datos de clientes para bauleras', err)
    });
  }

  get ocupados(): number {
    return this.filteredLockers.filter(l => l.status === 'OCUPADO').length;
  }
  get libres(): number {
    return this.filteredLockers.filter(l => l.status === 'DISPONIBLE').length;
  }
  get mantenimiento(): number {
    return this.filteredLockers.filter(l => l.status === 'MANTENIMIENTO').length;
  }
  get porLiberarse(): number {
    return this.filteredLockers.filter(l => l.status === 'POR LIBERARSE').length;
  }

  get filteredLockers(): Locker[] {
    const filtered = this.lockers.filter(item => {
      const warehouseMatch = (this.selectedWarehouseIds.length === 0 || this.selectedWarehouseIds.includes(item.warehouseId)) &&
                             !this.excludedWarehouseIds.includes(item.warehouseId) &&
                             (!this.selectedWarehouse || item.warehouseId.toString() === this.selectedWarehouse);

      const statusMatch = (this.selectedStatuses.length === 0 || this.selectedStatuses.includes(item.status)) &&
                          !this.excludedStatuses.includes(item.status) &&
                          (!this.selectedStatus || item.status === this.selectedStatus);

      const lockerTypeMatch = (this.selectedLockerTypeIds.length === 0 || this.selectedLockerTypeIds.includes(item.lockerTypeId)) &&
                              !this.excludedLockerTypeIds.includes(item.lockerTypeId);

      const searchLower = this.searchTerm.toLowerCase().trim();
      const searchMatch = !this.searchTerm || 
        item.identifier.toLowerCase().includes(searchLower) ||
        (item.features && item.features.toLowerCase().includes(searchLower)) ||
        this.getLockerClientNames(item).toLowerCase().includes(searchLower);

      return warehouseMatch && statusMatch && lockerTypeMatch && searchMatch;
    });

    return filtered.sort((a, b) => {
      let comparison = 0;

      if (this.sortField === 'identifier') {
        comparison = (a.identifier || '').localeCompare(b.identifier || '', undefined, { numeric: true, sensitivity: 'base' });
      } else if (this.sortField === 'warehouse') {
        comparison = this.getWarehouseName(a.warehouseId).localeCompare(this.getWarehouseName(b.warehouseId));
      } else if (this.sortField === 'lockerType') {
        comparison = this.getLockerTypeName(a.lockerTypeId).localeCompare(this.getLockerTypeName(b.lockerTypeId));
      } else if (this.sortField === 'features') {
        comparison = (a.features || '').localeCompare(b.features || '');
      } else if (this.sortField === 'status') {
        const weightA = this.getStatusSortWeight(a.status);
        const weightB = this.getStatusSortWeight(b.status);
        comparison = weightA !== weightB ? weightA - weightB : (a.status || '').localeCompare(b.status || '');
      } else if (this.sortField === 'clientName') {
        comparison = this.getLockerClientNames(a).localeCompare(this.getLockerClientNames(b), 'es');
      }

      if (comparison === 0 && this.sortField !== 'identifier') {
        comparison = (a.identifier || '').localeCompare(b.identifier || '', undefined, { numeric: true, sensitivity: 'base' });
      }

      return this.sortDirection === 'asc' ? comparison : -comparison;
    });
  }

  getFilteredLockers(): Locker[] {
    return this.filteredLockers;
  }

  getLockerClients(locker: Locker): LockerClientSummary[] {
    if (locker.clients?.length) return locker.clients;

    if (!locker.isFreeSpace && locker.clientName) {
      const match = this.clients.find(client => client.fullName.trim().toLowerCase() === locker.clientName!.trim().toLowerCase());
      return [{ id: match?.id ?? 0, fullName: locker.clientName, paymentIdentifier: match?.paymentIdentifier ?? 0 }];
    }

    return (locker.clientNames || '')
      .split(',')
      .map(name => name.trim())
      .filter(Boolean)
      .map(name => {
        const match = this.clients.find(client => client.fullName.trim().toLowerCase() === name.toLowerCase());
        return { id: match?.id ?? 0, fullName: name, paymentIdentifier: match?.paymentIdentifier ?? 0 };
      });
  }

  getLockerClientNames(locker: Locker): string {
    return this.getLockerClients(locker).map(client => client.fullName).join(', ');
  }

  trackByLockerClient(_index: number, client: LockerClientSummary): number | string {
    return client.id || client.fullName;
  }

  get otherClientLockers(): Locker[] {
    const target = this.clientStatsTarget;
    if (!target) return [];

    return this.lockers.filter(locker => {
      if (locker.id === target.lockerId) return false;
      return this.getLockerClients(locker).some(client =>
        client.id > 0 && target.client.id > 0
          ? client.id === target.client.id
          : client.fullName.trim().toLocaleLowerCase('es-AR') === target.client.fullName.trim().toLocaleLowerCase('es-AR')
      );
    });
  }

  showClientStats(client: LockerClientSummary, lockerId: number, origin: CdkOverlayOrigin): void {
    if (!client.id || !this.canShowClientStats(origin)) return;
    this.keepClientStatsOpen();
    clearTimeout(this.clientStatsOpenTimer);
    if (this.clientStatsTarget?.client.id === client.id && this.clientStatsTarget.lockerId === lockerId && this.clientStatsOpen) return;
    this.clientStatsOpen = false;
    this.clientStatsOpenTimer = setTimeout(() => {
      if (!this.canShowClientStats(origin)) return;
      this.clientStatsTarget = { client, lockerId };
      this.clientStatsOrigin = origin;
      this.clientStatsOpen = true;
    }, 250);
  }

  keepClientStatsOpen(): void {
    clearTimeout(this.clientStatsCloseTimer);
  }

  scheduleClientStatsClose(): void {
    clearTimeout(this.clientStatsOpenTimer);
    this.keepClientStatsOpen();
    this.clientStatsCloseTimer = setTimeout(() => this.closeClientStats(), 180);
  }

  closeClientStats(): void {
    clearTimeout(this.clientStatsOpenTimer);
    clearTimeout(this.clientStatsCloseTimer);
    this.clientStatsOpen = false;
    this.clientStatsTarget = null;
    this.clientStatsOverlay?.overlayRef?.detach();
  }

  openClientInClients(client: LockerClientSummary): void {
    if (!client.id) return;
    this.closeClientStats();
    this.router.navigate(['/clients'], { queryParams: { clientId: client.id } });
  }

  private canShowClientStats(origin: CdkOverlayOrigin): boolean {
    return !this.clientStatsNavigationPending
      && this.router.url.split(/[?#]/)[0] === '/lockers'
      && origin.elementRef.nativeElement.isConnected;
  }

  private getStatusSortWeight(status: string): number {
    switch (status) {
      case 'DISPONIBLE':
        return 1;
      case 'MANTENIMIENTO':
        return 2;
      case 'OCUPADO':
        return 3;
      case 'POR LIBERARSE':
        return 4;
      default:
        return 5;
    }
  }

  getWarehouseName(id: number): string {
    const w = this.warehouses.find(w => w.id === id);
    return w ? w.name : 'N/A';
  }

  // --- NUEVOS MÉTODOS VISUALES ---
  getLockerTypeName(id: number): string {
    const lt = this.lockerTypes.find(lt => lt.id === id);
    return lt ? lt.name : 'N/A';
  }

  getStatusColor(status: string): string {
    switch (status) {
      case 'DISPONIBLE':
        return 'bg-green-100 text-green-800';
      case 'OCUPADO':
        return 'bg-red-100 text-red-800';
      case 'MANTENIMIENTO':
        return 'bg-yellow-100 text-yellow-800';
      case 'POR LIBERARSE':
        return 'bg-yellow-200 text-yellow-900 border border-yellow-300';
      default:
        return 'bg-gray-100 text-gray-800';
    }
  }

  // --- Métodos Modal Editar (Corregidos) ---
  closeUpdateLockerModal() { this.showUpdateLockerModal = false; }

  openUpdateLockerModal(item: Locker){
    this.selectedLockerForEdit = item;
    this.lockerUpdate = {
      identifier : item.identifier,
      status : item.status,
      features: item.features,
      lockerTypeId: item.lockerTypeId,
      warehouseId: item.warehouseId,
      isFreeSpace: item.isFreeSpace ?? false
    };
    this.lockerOriginal = { ...this.lockerUpdate };
    this.idLockerUpdated = item.id;
    this.warehouseId = item.warehouseId;
    this.showUpdateLockerModal = true;
  }

  saveLockerUpdated(id: number, dto: LockerUpdateDTO): void {
     if (!dto.identifier || dto.identifier.trim() === '' || !dto.warehouseId || dto.warehouseId === 0 || !dto.lockerTypeId || dto.lockerTypeId === 0 || !dto.status || dto.status.trim() === '') {
       Swal.fire({
            icon: 'warning',
            title: 'Error actualizando baulera',
            text: 'Debes completar todos los campos obligatorios antes de guardar.',
            confirmButtonText: 'Entendido',
            confirmButtonColor: '#2563eb'
          });
       return;
     }

     const hasChanged = dto.identifier !== this.lockerOriginal.identifier ||
                        dto.status !== this.lockerOriginal.status ||
                        dto.features !== this.lockerOriginal.features ||
                        dto.lockerTypeId !== this.lockerOriginal.lockerTypeId ||
                        dto.warehouseId !== this.lockerOriginal.warehouseId ||
                        dto.isFreeSpace !== this.lockerOriginal.isFreeSpace;

     if (!hasChanged) {
       Swal.fire({
            icon: 'info',
            title: 'Sin cambios',
            text: 'No se detectaron cambios para actualizar.',
            confirmButtonText: 'Entendido',
            confirmButtonColor: '#2563eb'
          });
       return;
     }

     const isAssignedToClient = Boolean(
       (this.selectedLockerForEdit?.rentalId && this.selectedLockerForEdit.rentalId > 0) ||
       (this.selectedLockerForEdit?.clientName && this.selectedLockerForEdit.clientName.trim() !== '') ||
       (this.selectedLockerForEdit?.clientNames && this.selectedLockerForEdit.clientNames.trim() !== '') ||
       this.lockerOriginal.status === 'OCUPADO'
     );

     const staysIndividual = !this.lockerOriginal.isFreeSpace && !dto.isFreeSpace;

     if (staysIndividual && isAssignedToClient && dto.status === 'DISPONIBLE' && this.lockerOriginal.status !== 'DISPONIBLE') {
       const clientNameText = this.selectedLockerForEdit?.clientName && this.selectedLockerForEdit.clientName.trim() !== ''
         ? `al cliente <b>${this.selectedLockerForEdit.clientName}</b>`
         : 'a un cliente';

       Swal.fire({
         title: '¿Confirmar cambio a disponible?',
         html: `<p class="text-gray-700 mb-2">Estás poniendo en estado <b>DISPONIBLE</b> una baulera que actualmente le pertenece ${clientNameText}.</p>
                <p class="text-gray-700">Si confirmas, la baulera quedará disponible y se le quitará al cliente.</p>`,
         icon: 'warning',
         showCancelButton: true,
         confirmButtonText: 'Confirmar y quitar',
         cancelButtonText: 'Cancelar',
         buttonsStyling: false,
         customClass: {
           confirmButton: 'bg-blue-600 text-white px-4 py-2 rounded-md hover:bg-blue-700 transition-all duration-150',
           cancelButton: 'bg-gray-200 text-gray-800 px-4 py-2 rounded-md hover:bg-gray-300 transition-all duration-150 ml-4',
           actions: 'flex justify-center gap-4 mt-4',
           popup: 'rounded-xl shadow-lg'
         }
       }).then((result) => {
         if (result.isConfirmed) {
           this.executeLockerUpdate(id, dto);
         }
       });
       return;
     }

     this.executeLockerUpdate(id, dto);
  }

  private executeLockerUpdate(id: number, dto: LockerUpdateDTO): void {
     this.lockerService.updateLocker(id, dto).subscribe({
       next: () => {
         Swal.fire({
                  title: 'Baulera actualizada',
                  text: 'la baulera fue actualizada correctamente.',
                  icon: 'success',
                  confirmButtonText: 'Aceptar',
                  confirmButtonColor: '#2563eb'
                });
         this.closeUpdateLockerModal();
         setTimeout(() => this.loadLockers(), 100); 
       },
       error: (err) => {
         console.error('Error locker update', err)
         const backendMessage = typeof err?.error === 'string'
           ? err.error
           : err?.error?.message;
         Swal.fire({
                  title: 'Error',
                  text: backendMessage || 'Hubo un problema al actualizar la baulera.',
                  icon: 'error',
                  confirmButtonText: 'Aceptar',
                  confirmButtonColor: '#2563eb'
                });
       }
     });
  }

  
  private getDefaultNewLocker(): CreateLockerDTO {
    return {
      identifier: '',
      warehouseId: 0,
      lockerTypeId: 0,
      features: '',
      status: 'DISPONIBLE',
      isFreeSpace: false
    };
  }

  blurInput(event: Event): void {
    (event.target as HTMLElement).blur();
  }

  openCreateLockerModal(): void {
    this.newLocker = this.getDefaultNewLocker();
    this.showCreateLockerModal = true;
  }

  closeCreateLockerModal(): void {
    this.showCreateLockerModal = false;
  }

  saveNewLocker(): void {
    // --- Validación (Corregida) ---
    // CAMBIO: Validamos contra 0 en lugar de !this.newLocker.warehouseId
    if (!this.newLocker.identifier || !this.newLocker.warehouseId || this.newLocker.warehouseId === 0 || !this.newLocker.lockerTypeId || this.newLocker.lockerTypeId === 0) {
      Swal.fire({
        icon: 'error',
        title: 'Campos incompletos',
        text: 'Por favor, completa el Identificador, Depósito y Tipo de Locker.',
        confirmButtonColor: '#2563eb'
      });
      return;
    }

    this.lockerService.createLocker(this.newLocker).subscribe({
      next: () => {
        Swal.fire({
          title: 'Baulera Creada',
          text: `La baulera "${this.newLocker.identifier}" fue creada exitosamente.`,
          icon: 'success',
          confirmButtonText: 'Aceptar',
          confirmButtonColor: '#2563eb'
        });
        this.closeCreateLockerModal();
        this.loadLockers(); // Recargar la lista
      },
      error: (err) => {
        console.error('Error creando locker', err);
        Swal.fire({
          title: 'Error',
          text: 'Hubo un problema al crear la baulera. ' + (err.error?.message || ''),
          icon: 'error',
          confirmButtonText: 'Aceptar',
          confirmButtonColor: '#2563eb'
        });
      }
    });
  }
}

