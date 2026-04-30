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
import Swal from 'sweetalert2';
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


@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, ɵɵDir], // DatePipe añadido
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
    private lockerTypeService: LockerTypeService
  ) {}

  activeSection: string = 'usuarios';
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
    { id: 'usuarios', title: 'Usuarios', icon: '👤' },
    { id: 'medios-pago', title: 'Medios de Pago', icon: '💳' },
    { id: 'facturacion', title: 'Facturación', icon: '📄' },
    { id: 'locker-types', title: 'Tipos de Bauleras', icon: '🗄️' },
    { id: 'depositos', title: 'Depósitos', icon: '🏢' },
    { id: 'aumentos', title: 'Aumentos Mensuales', icon: '📈' },
    { id: 'smtp', title: 'Configuración SMTP', icon: '✉️' },
    
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
    console.log(this.showCreateUserModal);
    
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

  deleteUser(id: number): void{
    if (!id || id <= 0) {
      Swal.fire('Error', 'ID de usuario inválido.', 'error');
      return;
    }

    Swal.fire({
      title: '¿Estás seguro?',
      text: '¿Deseas eliminar este usuario? Esta acción no se puede deshacer.',
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#d33',
      cancelButtonColor: '#6B7280',
      confirmButtonText: 'Sí, eliminar',
      cancelButtonText: 'Cancelar'
    }).then((result) => {
      if (result.isConfirmed) {
        this.userService.deleteUser(id).subscribe({
          next: () => {
            Swal.fire('Eliminado', 'El usuario ha sido eliminado.', 'success');
            this.users = this.users.filter(u => u.id !== id);
          },
          error: (err) => {
            console.log('error al eliminar usuario', err);
            Swal.fire('Error', 'No se pudo eliminar el usuario.', 'error');
          }
        });
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

  deletePaymentMethod(id: number){
    if (!id) {
      console.log('id de medio de pago no valido.');
      return;
    }

    Swal.fire({
      title: '¿Estás seguro?',
      text: '¿Deseas eliminar este método de pago?',
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#d33',
      cancelButtonColor: '#6B7280',
      confirmButtonText: 'Sí, eliminar',
      cancelButtonText: 'Cancelar'
    }).then((result) => {
      if (result.isConfirmed) {
        this.paymentMethodService.deletePaymentMethod(id).subscribe({
          next: () => {
            Swal.fire('Eliminado', 'El método de pago ha sido eliminado.', 'success');
            this.loadPaymentMethods();
          },
          error: (err) => {
            console.log('error al eliminar el metodo de pago', err);
            Swal.fire('Error', 'No se pudo eliminar el método de pago.', 'error');
          }
        });
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

  deleteBillingType(billingType: BillingType): void {
    Swal.fire({
      title: '¿Estás seguro?',
      text: `¿Deseas eliminar el tipo de factura "${billingType.name}"? Esta acción no se puede revertir.`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#d33',
      cancelButtonColor: '#6B7280',
      confirmButtonText: 'Sí, eliminar',
      cancelButtonText: 'Cancelar'
    }).then((result) => {
      if (result.isConfirmed) {
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

  deleteIncrease(increase: MonthlyIncreaseSetting): void {
    Swal.fire({
      title: '¿Estás seguro?',
      text: `¿Deseas eliminar el aumento del ${increase.percentage}% para ${this.formatDateToMonthYear(increase.effectiveDate)}?`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#d33',
      cancelButtonColor: '#6B7280',
      confirmButtonText: 'Sí, eliminar',
      cancelButtonText: 'Cancelar'
    }).then((result) => {
      if (result.isConfirmed) {
        this.monthlyIncreaseService.deleteSetting(increase.id).subscribe({
          next: () => {
            Swal.fire('Eliminado', 'El aumento ha sido eliminado.', 'success');
            this.loadMonthlyIncreases();
          },
          error: (err) => {
            console.error('Error al eliminar aumento', err);
            Swal.fire('Error', 'No se pudo eliminar el aumento.', 'error');
          }
        });
      }
    });
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

  saveConfig() {
    const config = this.currentConfig();
    
    if (config.id) {
      // Update vía Service
      this.communicationService.updateSmtpConfig(config).subscribe({
        next: () => {
          this.loadConfigs();
          this.closeModal();
        },
        error: (err) => alert('Error al actualizar configuración')
      });
    } else {
      // Create vía Service
      this.communicationService.createSmtpConfig(config).subscribe({
        next: () => {
          this.loadConfigs();
          this.closeModal();
        },
        error: (err) => alert('Error al crear configuración')
      });
    }
  }

  deleteConfig(id: number) {
    if(confirm('¿Borrar esta configuración?')) {
      // Delete vía Service
      this.communicationService.deleteSmtpConfig(id).subscribe({
        next: () => this.loadConfigs(),
        error: (err) => alert('Error al eliminar configuración')
      });
    }
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

  deleteWarehouse(wh: Warehouse) {
     Swal.fire({
      title: '¿Estás seguro?',
      text: `Eliminar depósito "${wh.name}".`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonText: 'Sí, eliminar',
      confirmButtonColor: '#d33'
    }).then((result) => {
      if (result.isConfirmed) {
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

  deleteLockerType(lt: LockerType) {
    Swal.fire({
      title: '¿Estás seguro?',
      text: `Eliminar tipo de baulera "${lt.name}".`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonText: 'Sí, eliminar',
      confirmButtonColor: '#d33'
    }).then((result) => {
      if (result.isConfirmed) {
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