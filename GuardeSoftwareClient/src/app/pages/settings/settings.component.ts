import { Component, OnInit, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common'; // Importar DatePipe
import { FormsModule } from '@angular/forms';
import { User } from '../../core/models/user';
import { PaymentMethod } from '../../core/models/payment-method';
import { UserService } from '../../core/services/user-service/user.service';
import { PaymentMethodService } from '../../core/services/paymentMethod-service/payment-method.service';
import { CreateUserDTO } from '../../core/dtos/user/CreateUserDTO';
import { IconComponent } from '../../shared/components/icon/icon.component';
import { UserTypeService } from '../../core/services/userType-service/user-type.service';
import { UserType } from '../../core/models/user-type';
import { UpdatePaymentMethodDTO } from '../../core/dtos/paymentMethod/UpdatePaymentMethodDTO';
import { CreatePaymentMethodDTO } from '../../core/dtos/paymentMethod/CreatePaymentMethodDTO';
import { UpdateUserDTO } from '../../core/dtos/user/UpdateUserDTO';
import { BillingTypeService } from '../../core/services/billingType-service/billing-type.service';
import { BillingType } from '../../core/models/billing-type.model';
import { CreateBillingTypeDTO } from '../../core/dtos/billingType/create-billing-type.dto';
import Swal from '../../shared/services/ui-alert.service';
import { UpdateBillingTypeDTO } from '../../core/dtos/billingType/update-billing-type.dto';
import { MonthlyIncreaseService } from '../../core/services/monthlyIncrease-service/monthly-increase.service';
import { MonthlyIncreaseSetting } from '../../core/models/monthly-increase-setting';
import { CreateMonthlyIncreaseDto } from '../../core/dtos/monthlyIncrease/CreateMonthlyIncreaseDto';
import { UpdateMonthlyIncreaseDto } from '../../core/dtos/monthlyIncrease/UpdateMonthlyIncreaseDto';
import { SmtpConfig } from '../../core/models/smtp-config';
import { CommunicationService } from '../../core/services/communication-service/communication.service';
import { Warehouse } from '../../core/models/warehouse';
import { WarehouseService } from '../../core/services/warehouse-service/warehouse.service';
import { CreateWarehouseDto } from '../../core/dtos/warehouse/CreateWarehouseDto';
import { UpdateWarehouseDto } from '../../core/dtos/warehouse/UpdateWarehouseDto';
import { LockerTypeService } from '../../core/services/lockerType-service/locker-type.service';
import { LockerType } from '../../core/models/locker-type';
import { CreateLockerTypeDto } from '../../core/dtos/lockerType/CreateLockerTypeDto';
import { ɵɵDir } from "@angular/cdk/scrolling";
import { AuthService } from '../../core/services/auth-service/auth.service';
import { CreateAlertModalComponent } from '../../shared/components/create-alert-modal/create-alert-modal.component';
import { SyncService } from '../../core/services/offline-service/sync.service';
import { DeleteConfirmationService } from '../../shared/services/delete-confirmation.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, CreateAlertModalComponent],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.css'
})
export class SettingsComponent implements OnInit {

  constructor(
    private userService: UserService,
    private paymentMethodService: PaymentMethodService,
    private userTypeService: UserTypeService,
    private warehouseService: WarehouseService,
    private billingTypeService: BillingTypeService,
    private monthlyIncreaseService: MonthlyIncreaseService,
    private communicationService: CommunicationService,
    private lockerTypeService: LockerTypeService,
    public authService: AuthService,
    private syncService: SyncService,
    private deleteConfirmation: DeleteConfirmationService
  ) {}

  activeSection: string = 'usuarios';
  isCreateAlertOpen: boolean = false;

  openCreateAlertModal() {
    this.isCreateAlertOpen = true;
  }

  closeCreateAlertModal() {
    this.isCreateAlertOpen = false;
  }

  users : User[] = [];
  userTypes: UserType[] = [];
  paymentMethods : PaymentMethod [] = [];
  billingTypes: BillingType[] = [];

  // --- Propiedades de Usuario ---
  userCreated: CreateUserDTO = {
    userName: '',
    firstName: '',
    lastName: '',
    password: '',
    userTypeId: 2,
  }

  userEdit: User = {
    id: 0,
    userName: '',
    firstName: '',
    lastName: '',
    userTypeId: 0,
  }

  userUpdated: UpdateUserDTO = {
    userName: '',
    firstName: '',
    lastName: '',
    userTypeId: 0,
  }

  SelectedUserId = 0;
  showCreateUserModal = false;
  showEditUserModal = false;

  // --- Propiedades de Medios de Pago ---
  paymentMethodUpdate: UpdatePaymentMethodDTO = {
    name: '',
    commission: 0,
  }
  SelectedPaymentMethodId = 0;
  SelectedPaymentMethodName = '';
  showUpdatePaymentMethodModal = false;
  createPaymentMethodDto: CreatePaymentMethodDTO = {
    name: '',
    commission: 0,
  }
  showCreatePaymentMethod = false;

  // --- Propiedades de Tipos de Factura ---
  showCreateBillingTypeModal = false;
  showEditBillingTypeModal = false;
  newBillingType: CreateBillingTypeDTO = { name: '' };
  editingBillingType: BillingType = { id: 0, name: '' };
  originalBillingTypeName: string = '';

  // --- Propiedades de Aumentos Mensuales ---
  monthlyIncreases: MonthlyIncreaseSetting[] = [];
  showCreateIncreaseModal = false;
  showEditIncreaseModal = false;
  newIncrease: CreateMonthlyIncreaseDto = { effectiveDate: '', percentage: 0 };
  editingIncrease: MonthlyIncreaseSetting = { id: 0, effectiveDate: new Date(), percentage: 0 };
  originalIncreasePercentage: number = 0;

  // --- Propiedades SMTP ---
  smtpConfigs = signal<SmtpConfig[]>([]);
  isModalOpen = signal(false);
  showSmtpPassword = false;

  currentConfig = signal<SmtpConfig>({
    id: null,
    name: '',
    host: '',
    port: 465,
    email: '',
    password: '',
    useSsl: true,
    enableBcc: false,
    bccEmail: 'estadodecuenta@abono.com.ar' // Default value
  });

  // --- LockerTypes properties ---
  lockerTypes: LockerType[] = [];
  newLockerType: CreateLockerTypeDto = { name: '', m3: 0 };
  editingLockerType: LockerType = { id: 0, name: '', m3: 0 };
  showCreateLockerTypeModal = false;
  showEditLockerTypeModal = false;

  // --- Warehouses properties ---
  warehouses: Warehouse[] = [];

  showCreateWarehouseModal = false;
  showEditWarehouseModal = false;
  newWarehouse: CreateWarehouseDto = { name: '', address: '' };
  editingWarehouse: Warehouse = { id: 0, name: '', address: ''};

  ngOnInit(): void {
    const isAdmin = this.authService.isAdmin ? this.authService.isAdmin() : false; 
    
    this.configSections = this.configSections.filter(section => !section.adminOnly || isAdmin);

    if (!this.configSections.some(s => s.id === this.activeSection)) {
      this.activeSection = this.configSections[0].id;
    }

    this.loadUsers();
    this.loadPaymentMethods();
    this.loadUserTypes();
    this.loadBillingTypes();
    this.loadMonthlyIncreases();
    this.loadConfigs();
    this.loadWarehouses();
    this.loadLockerTypes();
  }

  // --- Métodos de Carga ---
  loadMonthlyIncreases(): void {
    this.monthlyIncreaseService.getSettings().subscribe({
      next: (data) => {
        this.monthlyIncreases = data;
      },
      error: (err) => {
        console.error('Error al cargar aumentos mensuales', err);
        Swal.fire('Error', 'No se pudieron cargar las configuraciones de aumentos', 'error');
      },
    });
  }

  loadBillingTypes(): void {
    this.billingTypeService.getBillingTypes().subscribe({
      next: (data) => {
        this.billingTypes = data;
      },
      error: (err) => {
        console.error('Error al cargar tipos de factura', err);
        Swal.fire('Error', 'No se pudieron cargar los tipos de factura', 'error');
      },
    });
  }

  loadUserTypes(): void{
    this.userTypeService.getUserTypes().subscribe({
      next: (data) => {
        this.userTypes = data;
      },
      error: (err) => console.log('error al obtener tipos de usuario',err)
    });
  }

  loadUsers(): void{
    this.userService.getUsers().subscribe({
      next: (data) =>{
        this.users = data;
      },
      error: (err) => {
        console.error('error: ', err)
      }
    });
  }

  loadPaymentMethods(): void{
    this.paymentMethodService.getPaymentMethods().subscribe({
      next: (data) => {
        this.paymentMethods = data;
      },
      error: (err) => {
        console.error('error: ',err);
      }
    });
  }

  loadLockerTypes(): void{
    this.lockerTypeService.getLockerTypes().subscribe({
      next: (data) => {
        this.lockerTypes = data;
      },
      error: (err) => {
        console.error('error: ',err);
      }
    });
  }

  

  // --- Navegación ---
  configSections = [
    { id: 'usuarios', title: 'Usuarios', icon: '👤', adminOnly: true },
    { id: 'medios-pago', title: 'Medios de Pago', icon: '💳' },
    { id: 'facturacion', title: 'Facturación', icon: '📄' },
    { id: 'locker-types', title: 'Tipos de Bauleras', icon: '🗄️' },
    { id: 'depositos', title: 'Depósitos', icon: '🏢' },
    // { id: 'aumentos', title: 'Aumentos Mensuales', icon: '📈' },
    { id: 'smtp', title: 'Configuración de Mails', icon: '✉️' },
    { id: 'offline', title: ' Modo Offline', icon: '💾' }
    // { id: 'datos', title: 'Datos', icon: '🗄️' }
  ];

  setActive(section: string) {
    this.activeSection = section;
  }

  getUserTypeName(userTypeId: number): string {
    if (!userTypeId || this.userTypes.length === 0) {
      return 'Desconocido';
    }
    const type = this.userTypes.find(t => t.id === userTypeId);
    return type ? type.name : 'Desconocido';
  }

  // --- Métodos de Gestión de Usuarios (Actualizados a Swal) ---
  closeCreateUserModal() { this.showCreateUserModal = false; }
  
  openCreateUserModal(){ 
    this.userCreated = { // Resetear
      userName: '', firstName: '', lastName: '', password: '', userTypeId: 2,
    };
    this.showCreateUserModal = true;     
  }
 
  saveCreateUser(dto: CreateUserDTO){
    dto.userName = dto.userName?.trim() || '';
    dto.firstName = dto.firstName?.trim() || '';
    dto.lastName = dto.lastName?.trim() || '';
    dto.password = dto.password?.trim() || '';

    if (!dto.userName || !dto.firstName || !dto.lastName || !dto.password || !dto.userTypeId || dto.userTypeId <= 0) {
      Swal.fire('Campos incompletos', 'Por favor, completa todos los campos requeridos.', 'warning');
      return;
    }
    if (/\s/.test(dto.userName)) {
      Swal.fire('Error', 'El nombre de usuario no puede contener espacios.', 'warning');
      return;
    }

    this.userService.createUser(dto).subscribe({
      next: () => {
        Swal.fire('Éxito', 'Usuario creado correctamente.', 'success');
        this.loadUsers(); 
        this.closeCreateUserModal();
      },
      error: (err) => {
        console.error('Error al crear usuario:', err);
        Swal.fire('Error', 'No se pudo crear el usuario.', 'error');
      }
    });
  }

  async deleteUser(id: number): Promise<void>{
    if (!id || id <= 0) {
      Swal.fire('Error', 'ID de usuario inválido.', 'error');
      return;
    }

    const confirmed = await this.deleteConfirmation.confirm({
      message: 'Esta acción eliminará este usuario y no se puede deshacer.'
    });
    if (!confirmed) return;

    this.userService.deleteUser(id).subscribe({
      next: () => {
        Swal.fire('Eliminado', 'El usuario ha sido eliminado.', 'success');
        this.users = this.users.filter(u => u.id !== id);
      },
      error: () => {
        Swal.fire('Error', 'No se pudo eliminar el usuario.', 'error');
      }
    });
  }

  closeEditUserModal() { this.showEditUserModal = false; }
  
  openEditModalModal(item: User){
    this.userEdit = { ...item }; // Copiar objeto
    this.showEditUserModal = true; 
  }

  SaveUpdateUserModal(item: User){
    if (!item || !item.userName?.trim() || !item.firstName?.trim() || !item.lastName?.trim() || !item.userTypeId) {
      Swal.fire('Campos incompletos', 'Por favor, completa todos los campos requeridos.', 'warning');
      return;
    }

    this.userUpdated = {
      userTypeId: item.userTypeId,
      userName: item.userName.trim(),
      firstName: item.firstName.trim(),
      lastName: item.lastName.trim(),
    }
    this.SelectedUserId = item.id;

    this.userService.updateUser(this.SelectedUserId, this.userUpdated).subscribe({
      next: () => {
        Swal.fire('Éxito', 'Usuario actualizado correctamente.', 'success');
        this.loadUsers();
        this.showEditUserModal = false; 
      },
      error: (err) => {
        console.log('error actualizando el usuario.', err);
        Swal.fire('Error', 'No se pudo actualizar el usuario.', 'error');
      }
    });
  }

  changeUserPassword(user: User): void {
    Swal.fire({
      title: `Cambiar contraseña de ${user.userName}`,
      input: 'password',
      inputLabel: 'Nueva contraseña',
      inputPlaceholder: 'Escribe la nueva contraseña',
      inputAttributes: {
        autocapitalize: 'off',
        autocorrect: 'off'
      },
      showCancelButton: true,
      confirmButtonText: 'Guardar',
      cancelButtonText: 'Cancelar',
      confirmButtonColor: '#2563EB',
      cancelButtonColor: '#6B7280',
      preConfirm: (newPassword) => {
        if (!newPassword || !newPassword.trim()) {
          Swal.showValidationMessage('La contraseña no puede estar vacía');
          return false;
        }
        return newPassword.trim();
      }
    }).then((result) => {
      if (result.isConfirmed && result.value) {
        this.userService.changePassword(user.id, result.value).subscribe({
          next: () => {
            Swal.fire('Éxito', 'Contraseña actualizada correctamente.', 'success');
          },
          error: (err) => {
            console.error('Error al cambiar contraseña:', err);
            const msg = err.error ? (typeof err.error === 'string' ? err.error : err.error.message) : 'No se pudo cambiar la contraseña.';
            Swal.fire('Error', msg || 'No se pudo cambiar la contraseña.', 'error');
          }
        });
      }
    });
  }

  // --- Métodos de Medios de Pago (Actualizados a Swal) ---
  closeUpdatePaymentMethodModal() { this.showUpdatePaymentMethodModal = false; }

  openUpdatePaymentModal(item: PaymentMethod){
    this.paymentMethodUpdate = {
      name: item.name,
      commission : item.commission,
    }
    this.SelectedPaymentMethodId = item.id;
    this.SelectedPaymentMethodName = item.name;
    this.showUpdatePaymentMethodModal = true;
  }
    
  updatePaymentMethod(id: number,dto: UpdatePaymentMethodDTO){
    if (!id) {
      Swal.fire('Error', 'Debes seleccionar un método de pago.', 'warning');
      return;
    }
    if (dto.commission === undefined || dto.commission < 0 || dto.commission > 100) {
      Swal.fire('Valor incorrecto', 'La comisión debe ser un valor entre 0 y 100.', 'warning');
      return;
    }

    this.paymentMethodService.UpdatePaymentMethod(id,dto).subscribe({
      next: () => {
        Swal.fire('Éxito', 'Método de pago actualizado.', 'success');
        this.loadPaymentMethods();
        this.closeUpdatePaymentMethodModal();
      },
      error: (err) =>{
        console.log('error al actualizar el metodo de pago: ',err);
        Swal.fire('Error', 'No se pudo actualizar el método de pago.', 'error');
      } 
    });
  }

  closeCreatePaymentMethodModal() { 
    this.createPaymentMethodDto = {
      name: '',
      commission: 0,
    }
    this.showCreatePaymentMethod = false; 
  }

  openCreatePaymentMethod(){ 
    this.showCreatePaymentMethod = true;
  }

  createPaymentMethod(dto: CreatePaymentMethodDTO){
    dto.name = dto.name?.trim() || '';

    if (!dto.name) {
      Swal.fire('Campo requerido', 'Debes ingresar un nombre de método de pago.', 'warning');
      return;
    }
    if (dto.commission === undefined || dto.commission < 0 || dto.commission > 100) {
      Swal.fire('Valor incorrecto', 'La comisión debe ser un valor entre 0 y 100.', 'warning');
      return;
    }

    this.paymentMethodService.createPaymentMethod(dto).subscribe({
      next: () => {
        Swal.fire('Éxito', 'Método de pago creado con éxito.', 'success');
        this.loadPaymentMethods();
        this.closeCreatePaymentMethodModal();
      },
      error: (err) => {
        console.log('error al crear metodo de pago', err);
        Swal.fire('Error', 'No se pudo crear el método de pago.', 'error');
      }
    });
  }

  async deletePaymentMethod(id: number): Promise<void>{
    if (!id) {
      console.log('id de medio de pago no valido.');
      return;
    }

    const confirmed = await this.deleteConfirmation.confirm({
      message: 'Esta acción eliminará este método de pago.'
    });
    if (!confirmed) return;

    this.paymentMethodService.deletePaymentMethod(id).subscribe({
      next: () => {
        Swal.fire('Eliminado', 'El método de pago ha sido eliminado.', 'success');
        this.loadPaymentMethods();
      },
      error: () => {
        console.log('error al eliminar el metodo de pago');
        Swal.fire('Error', 'No se pudo eliminar el método de pago.', 'error');
      }
    });
  }
 
  // --- Métodos de Tipos de Factura (Billing Types) ---
  openCreateBillingTypeModal(): void {
    this.newBillingType = { name: '' }; 
    this.showCreateBillingTypeModal = true;
  }
  
  closeCreateBillingTypeModal(): void {
    this.showCreateBillingTypeModal = false;
  }

  saveNewBillingType(): void {
    if (!this.newBillingType.name || this.newBillingType.name.trim() === '') {
      Swal.fire('Error', 'El nombre no puede estar vacío', 'warning');
      return;
    }
    this.billingTypeService.createBillingType(this.newBillingType).subscribe({
      next: (created) => {
        Swal.fire('Creado', `Tipo de factura "${created.name}" creado con éxito.`, 'success');
        this.loadBillingTypes(); 
        this.closeCreateBillingTypeModal();
      },
      error: (err) => {
        console.error('Error al crear tipo de factura', err);
        Swal.fire('Error', 'No se pudo crear el tipo de factura', 'error');
      }
    });
  }

  openEditBillingTypeModal(billingType: BillingType): void {
    this.editingBillingType = { ...billingType }; 
    this.originalBillingTypeName = billingType.name; 
    this.showEditBillingTypeModal = true;
  }

  closeEditBillingTypeModal(): void {
    this.showEditBillingTypeModal = false;
  }

  saveUpdatedBillingType(): void {
    if (!this.editingBillingType.name || this.editingBillingType.name.trim() === '') {
      Swal.fire('Error', 'El nombre no puede estar vacío', 'warning');
      return;
    }
    if (this.editingBillingType.name.trim() === this.originalBillingTypeName) {
      Swal.fire('Sin cambios', 'No se detectaron cambios en el nombre.', 'info');
      this.closeEditBillingTypeModal();
      return;
    }

    const dto: UpdateBillingTypeDTO = { name: this.editingBillingType.name.trim() };
    this.billingTypeService.updateBillingType(this.editingBillingType.id, dto).subscribe({
      next: () => {
        Swal.fire('Actualizado', 'Tipo de factura actualizado con éxito.', 'success');
        this.loadBillingTypes();
        this.closeEditBillingTypeModal();
      },
      error: (err) => {
        console.error('Error al actualizar tipo de factura', err);
        Swal.fire('Error', 'No se pudo actualizar el tipo de factura', 'error');
      }
    });
  }

  async deleteBillingType(billingType: BillingType): Promise<void> {
    const confirmed = await this.deleteConfirmation.confirm({
      message: 'Esta acción eliminará el tipo de factura',
      highlightedText: billingType.name,
      messageSuffix: 'Esta acción no se puede revertir.'
    });
    if (!confirmed) return;

    this.billingTypeService.deleteBillingType(billingType.id).subscribe({
      next: () => {
        Swal.fire('Eliminado', 'El tipo de factura ha sido eliminado.', 'success');
        this.loadBillingTypes();
      },
      error: (err) => {
        console.error('Error al eliminar tipo de factura', err);
        const errorMsg = err.error?.message || 'No se pudo eliminar. Es posible que esté en uso por algún cliente.';
        Swal.fire('Error', errorMsg, 'error');
      }
    });
  }

  // --- Métodos de Aumentos Mensuales ---
  openCreateIncreaseModal(): void {
    const nextMonth = new Date();
    nextMonth.setMonth(nextMonth.getMonth() + 1);
    const nextMonthString = nextMonth.toISOString().split('T')[0].substring(0, 7);
    
    this.newIncrease = { effectiveDate: nextMonthString, percentage: 0 };
    this.showCreateIncreaseModal = true;
  }

  closeCreateIncreaseModal(): void {
    this.showCreateIncreaseModal = false;
  }

  saveNewIncrease(): void {
    if (!this.newIncrease.effectiveDate || this.newIncrease.percentage <= 0) {
      Swal.fire('Datos inválidos', 'Debe seleccionar un mes (formato AAAA-MM) y un porcentaje mayor a 0.', 'warning');
      return;
    }
    this.monthlyIncreaseService.createSetting(this.newIncrease).subscribe({
      next: (created) => {
        Swal.fire('Creado', `Aumento del ${created.percentage}% para ${this.formatDateToMonthYear(created.effectiveDate)} creado.`, 'success');
        this.loadMonthlyIncreases();
        this.closeCreateIncreaseModal();
      },
      error: (err) => {
        console.error('Error al crear aumento', err);
        Swal.fire('Error', 'No se pudo crear el aumento. ¿Quizás ya existe para ese mes?', 'error');
      }
    });
  }

  openEditIncreaseModal(increase: MonthlyIncreaseSetting): void {
    this.editingIncrease = { ...increase }; // Copia
    this.originalIncreasePercentage = increase.percentage;
    this.showEditIncreaseModal = true;
  }

  closeEditIncreaseModal(): void {
    this.showEditIncreaseModal = false;
  }

  saveUpdatedIncrease(): void {
    if (this.editingIncrease.percentage <= 0) {
      Swal.fire('Datos inválidos', 'El porcentaje debe ser mayor a 0.', 'warning');
      return;
    }
    if (this.editingIncrease.percentage === this.originalIncreasePercentage) {
      this.closeEditIncreaseModal();
      return;
    }
    
    const dto: UpdateMonthlyIncreaseDto = { percentage: this.editingIncrease.percentage };
    this.monthlyIncreaseService.updateSetting(this.editingIncrease.id, dto).subscribe({
      next: () => {
        Swal.fire('Actualizado', 'Porcentaje de aumento actualizado.', 'success');
        this.loadMonthlyIncreases();
        this.closeEditIncreaseModal();
      },
      error: (err) => {
        console.error('Error al actualizar aumento', err);
        Swal.fire('Error', 'No se pudo actualizar el aumento.', 'error');
      }
    });
  }

  async deleteIncrease(increase: MonthlyIncreaseSetting): Promise<void> {
    const confirmed = await this.deleteConfirmation.confirm({
      message: `Esta acción eliminará el aumento del ${increase.percentage}% para ${this.formatDateToMonthYear(increase.effectiveDate)}.`
    });
    if (!confirmed) return;

    this.monthlyIncreaseService.deleteSetting(increase.id).subscribe({
      next: () => {
        Swal.fire('Eliminado', 'El aumento ha sido eliminado.', 'success');
        this.loadMonthlyIncreases();
      },
      error: () => {
        console.error('Error al eliminar aumento');
        Swal.fire('Error', 'No se pudo eliminar el aumento.', 'error');
      }
    });
  }

  // --- Offline Snapshot Methods ---
  async downloadSnapshot(): Promise<void> {
    try {
      await this.syncService.downloadSnapshot();
      Swal.fire('Éxito', 'El archivo de respaldo (snapshot) se descargó correctamente.', 'success');
    } catch (err) {
      console.error(err);
      Swal.fire('Error', 'No se pudo generar el archivo de respaldo.', 'error');
    }
  }

  async importSnapshot(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) return;
    
    const file = input.files[0];
    try {
      const success = await this.syncService.importSnapshot(file);
      if (success) {
        Swal.fire('Éxito', 'El archivo de respaldo fue importado y guardado localmente para uso sin conexión.', 'success');
      } else {
        Swal.fire('Error', 'El archivo es inválido o corrupto.', 'error');
      }
    } catch (err) {
      console.error(err);
      Swal.fire('Error', 'Hubo un problema al importar el archivo.', 'error');
    } finally {
      input.value = ''; // Reset input
    }
  }

  // Helper para mostrar "Noviembre 2025"
  formatDateToMonthYear(dateInput: Date | string): string {
      const date = new Date(dateInput);
      return date.toLocaleDateString('es-ES', { month: 'long', year: 'numeric', timeZone: 'UTC' }); 
  }

  // --- SMTP Config Methods ---
  loadConfigs() {
    this.communicationService.getAllSmtpConfigs().subscribe({
      next: (data) => this.smtpConfigs.set(data),
      error: (err) => console.error('Error al cargar configs SMTP', err)
    });
  }

  openModal(config?: SmtpConfig) {
    this.showSmtpPassword = false;
    if (config) {
      this.currentConfig.set({ ...config });
    } else {
      this.resetForm();
    }
    this.isModalOpen.set(true);
  }

  closeModal() {
    this.isModalOpen.set(false);
  }

  resetForm() {
    this.currentConfig.set({
      id: null,
      name: '',
      host: '',
      port: 587,
      email: '',
      password: '',
      useSsl: true,
      enableBcc: false,
      bccEmail: ''
    });
  }

  updateSmtpField(field: keyof SmtpConfig, value: string | number | boolean): void {
    this.currentConfig.update(config => ({
      ...config,
      [field]: value
    } as SmtpConfig));
  }

  toggleSmtpPassword(): void {
    this.showSmtpPassword = !this.showSmtpPassword;
  }

  saveConfig() {
    const config = {
      ...this.currentConfig(),
      name: this.currentConfig().name.trim(),
      host: this.currentConfig().host.trim(),
      email: this.currentConfig().email.trim(),
      bccEmail: this.currentConfig().bccEmail.trim()
    };

    if (!config.name || !config.host || !config.email || !config.password) {
      Swal.fire({
        icon: 'warning',
        title: 'Faltan datos',
        text: 'Completá el nombre, host, usuario y contraseña del servidor.'
      });
      return;
    }

    if (!this.isValidEmail(config.email) || (config.enableBcc && !this.isValidEmail(config.bccEmail))) {
      Swal.fire({
        icon: 'warning',
        title: 'Email inválido',
        text: 'Revisá la dirección de correo ingresada antes de continuar.'
      });
      return;
    }

    if (!Number.isInteger(config.port) || config.port < 1 || config.port > 65535) {
      Swal.fire({
        icon: 'warning',
        title: 'Puerto inválido',
        text: 'Ingresá un puerto entre 1 y 65535.'
      });
      return;
    }

    if (config.enableBcc && !config.bccEmail) {
      Swal.fire({
        icon: 'warning',
        title: 'Falta el correo de copia',
        text: 'Ingresá una dirección para activar la copia oculta.'
      });
      return;
    }

    this.currentConfig.set(config);
    
    if (config.id) {
      // Update vía Service
      this.communicationService.updateSmtpConfig(config).subscribe({
        next: () => {
          this.loadConfigs();
          this.closeModal();
          Swal.fire({
            toast: true,
            position: 'top-end',
            icon: 'success',
            title: 'Servidor actualizado',
            showConfirmButton: false,
            timer: 2600,
            timerProgressBar: true
          });
        },
        error: () => Swal.fire({
          icon: 'error',
          title: 'No se pudo actualizar',
          text: 'Revisá los datos e intentá nuevamente.'
        })
      });
    } else {
      // Create vía Service
      this.communicationService.createSmtpConfig(config).subscribe({
        next: () => {
          this.loadConfigs();
          this.closeModal();
          Swal.fire({
            toast: true,
            position: 'top-end',
            icon: 'success',
            title: 'Servidor creado',
            showConfirmButton: false,
            timer: 2600,
            timerProgressBar: true
          });
        },
        error: () => Swal.fire({
          icon: 'error',
          title: 'No se pudo crear',
          text: 'El servidor no fue guardado. Revisá los datos e intentá nuevamente.'
        })
      });
    }
  }

  private isValidEmail(email: string): boolean {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
  }

  async deleteConfig(id: number): Promise<void> {
    const confirmed = await this.deleteConfirmation.confirm({
      headerTitle: 'Confirmar Eliminación',
      message: 'Las comunicaciones futuras ya no podrán usar esta configuración.'
    });
    if (!confirmed) return;

    this.communicationService.deleteSmtpConfig(id).subscribe({
      next: () => {
        this.loadConfigs();
        Swal.fire({
          toast: true,
          position: 'top-end',
          icon: 'success',
          title: 'Servidor eliminado',
          showConfirmButton: false,
          timer: 2400,
          timerProgressBar: true
        });
      },
      error: () => Swal.fire({
        icon: 'error',
        title: 'No se pudo eliminar',
        text: 'Intentá nuevamente en unos instantes.'
      })
    });
  }

  loadWarehouses(): void {
    this.warehouseService.getWarehouses().subscribe({
      next: (data) => this.warehouses = data,
      error: (err) => console.error('Error cargando depósitos', err)
    });
  }

  // --- Warehouses methods ---
  
  openCreateWarehouseModal() {
    this.newWarehouse = { name: '', address: '' };
    this.showCreateWarehouseModal = true;
  }
  
  closeCreateWarehouseModal() { this.showCreateWarehouseModal = false; }
  
  saveNewWarehouse() {
    if(!this.newWarehouse.name) {
       Swal.fire('Error', 'El nombre es obligatorio', 'warning');
       return;
    }
    this.warehouseService.createWarehouse(this.newWarehouse).subscribe({
      next: () => {
         Swal.fire('Éxito', 'Depósito creado', 'success');
         this.loadWarehouses();
         this.closeCreateWarehouseModal();
      },
      error: (err) => Swal.fire('Error', 'No se pudo crear el depósito', 'error')
    });
  }

  openEditWarehouseModal(wh: Warehouse) {
      this.editingWarehouse = { ...wh };
      this.showEditWarehouseModal = true;
  }
  
  closeEditWarehouseModal() { this.showEditWarehouseModal = false; }
  
  saveUpdatedWarehouse() {
     if(!this.editingWarehouse.name) {
       Swal.fire('Error', 'El nombre es obligatorio', 'warning');
       return;
    }
    const dto: UpdateWarehouseDto = { 
        name: this.editingWarehouse.name, 
        address: this.editingWarehouse.address 
    };
    this.warehouseService.updateWarehouse(this.editingWarehouse.id, dto).subscribe({
      next: () => {
         Swal.fire('Éxito', 'Depósito actualizado', 'success');
         this.loadWarehouses();
         this.closeEditWarehouseModal();
      },
      error: (err) => Swal.fire('Error', 'No se pudo actualizar', 'error')
    });
  }

  async deleteWarehouse(wh: Warehouse): Promise<void> {
    const confirmed = await this.deleteConfirmation.confirm({
      message: 'Esta acción eliminará el depósito',
      highlightedText: wh.name,
      messageSuffix: '.'
    });
    if (!confirmed) return;

    this.warehouseService.deleteWarehouse(wh.id).subscribe({
      next: () => {
        Swal.fire('Eliminado', 'Depósito eliminado.', 'success');
        this.loadWarehouses();
      },
      error: (err) => {
        const msg = err.error?.message || 'Error al eliminar.';
        Swal.fire('Error', msg, 'error');
      }
    });
  }

  openCreateLockerTypeModal() {
    this.newLockerType = { name: '', m3: 0 };
    this.showCreateLockerTypeModal = true;
  }

  closeCreateLockerTypeModal() { this.showCreateLockerTypeModal = false; }

  saveNewLockerType() {
    if(!this.newLockerType.name) {
       Swal.fire('Error', 'El nombre es obligatorio', 'warning');
       return;
    }
    this.lockerTypeService.createLockerType(this.newLockerType).subscribe({
      next: () => {
         Swal.fire('Éxito', 'Tipo de baulera creado', 'success');
          this.loadLockerTypes();
          this.closeCreateLockerTypeModal();
      },
      error: (err) => Swal.fire('Error', 'No se pudo crear el tipo de baulera', 'error')
    });
  }

  openUpdateLockerTypeModal(lt: LockerType) {
    this.editingLockerType = { ...lt };
    this.showEditLockerTypeModal = true;
  }

  async deleteLockerType(lt: LockerType): Promise<void> {
    const confirmed = await this.deleteConfirmation.confirm({
      message: 'Esta acción eliminará el tipo de baulera',
      highlightedText: lt.name,
      messageSuffix: '.'
    });
    if (!confirmed) return;

    this.lockerTypeService.deleteLockerType(lt.id).subscribe({
      next: () => {
        Swal.fire('Eliminado', 'Tipo de baulera eliminado.', 'success');
        this.loadLockerTypes();
      },
      error: (err) => {
        const msg = err.error?.message || 'Error al eliminar.';
        Swal.fire('Error', msg, 'error');
      }
    });
  }

  closeEditLockerTypeModal() { this.showEditLockerTypeModal = false; }
  
  saveUpdatedLockerType() {
    if(!this.editingLockerType.name) {
       Swal.fire('Error', 'El nombre es obligatorio', 'warning');
       return;
    }
    const dto: CreateLockerTypeDto = {
      name: this.editingLockerType.name,
      m3: this.editingLockerType.m3
    };
    this.lockerTypeService.updateLockerType(this.editingLockerType.id, dto).subscribe({
      next: () => {
         Swal.fire('Éxito', 'Tipo de baulera actualizado', 'success');
         this.loadLockerTypes();
         this.closeEditLockerTypeModal();
      },
      error: (err) => Swal.fire('Error', 'No se pudo actualizar el tipo de baulera', 'error')
    });
  }

}
