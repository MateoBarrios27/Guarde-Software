import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
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


@Component({
  selector: 'app-settings',
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.css'
})
export class SettingsComponent implements OnInit {

  constructor(
    private userService: UserService,
    private paymentMethodService: PaymentMethodService,
    private userTypeService: UserTypeService,
  ) {}

  activeSection: string = 'usuarios';
  users : User[] = [];
  userTypes: UserType[] = [];
  paymentMethods : PaymentMethod [] = [];

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

  //payment method update
  paymentMethodUpdate: UpdatePaymentMethodDTO = {
    commission: 0,
  }

  SelectedPaymentMethodId = 0;
  SelectedPaymentMethodName = '';
  showUpdatePaymentMethodModal = false;

  //create Payment method

  createPaymentMethodDto: CreatePaymentMethodDTO = {
    name: '',
    commission: 0,
  }

  showCreatePaymentMethod = false;

  ngOnInit(): void {
    this.loadUsers();
    this.loadPaymentMethods();
    this.loadUserTypes();
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

  configSections = [
    { id: 'usuarios', title: 'Usuarios', icon: '👤' },
    { id: 'medios-pago', title: 'Medios de Pago', icon: '💳' },
    { id: 'datos', title: 'Datos', icon: '🗄️' }
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

  closeCreateUserModal() { this.showCreateUserModal = false; }
  
  openCreateUserModal(){ this.showCreateUserModal = true; }

  saveCreateUser(dto: CreateUserDTO){

    dto.userName = dto.userName?.trim() || '';
    dto.firstName = dto.firstName?.trim() || '';
    dto.lastName = dto.lastName?.trim() || '';
    dto.password = dto.password?.trim() || '';

    if (!dto.userName) {
      alert('⚠️ Debes ingresar un nombre de usuario.');
      return;
    }

    if (!dto.firstName) {
      alert('⚠️ Debes ingresar el nombre del usuario.');
      return;
    }

    if (!dto.lastName) {
      alert('⚠️ Debes ingresar el apellido del usuario.');
      return;
    }

    if (!dto.password) {
      alert('⚠️ Debes ingresar una contraseña.');
      return;
    }

    if (!dto.userTypeId || dto.userTypeId <= 0) {
      alert('⚠️ Debes seleccionar un tipo de usuario.');
      return;
    }

    if (/\s/.test(dto.userName)) {
      alert('⚠️ El nombre de usuario no puede contener espacios.');
      return;
    }
    this.userService.createUser(dto).subscribe({
      next: () => {
         alert('✅ usuario creado correctamente.');
         setTimeout(() => this.loadUsers(), 100); 
         this.closeCreateUserModal();
      },
      error: (err) => console.error('Error al crear usuario:', err)
    });
  }

  deleteUser(id: number): void{

    if (!id || id <= 0) {
    alert('⚠️ ID de usuario inválido.');
    return;
    }

    const confirmDelete = confirm('⚠️ ¿Estás seguro de que deseas eliminar este usuario? Esta acción no se puede deshacer.');

    if (!confirmDelete) {
      return;
    }
    this.userService.deleteUser(id).subscribe({
      next: () => {
         alert('✅ usuario eliminado correctamente.');
         this.users = this.users.filter(u => u.id !== id);
      },
      error: (err) => console.log('error al eliminar usuario',err)
    });
  }

    closeEditUserModal() { this.showEditUserModal = false; }
  
    openEditModalModal(item: User){
      this.userEdit = {
        id: item.id,
        userTypeId: item.userTypeId,
        userName: item.userName,
        firstName: item.firstName,
        lastName: item.lastName,
      }
      this.showEditUserModal = true; 
    }

    SaveUpdateUserModal(item: User){
      if (!item) {
        alert('Error: no se recibió la información del usuario.');
        return;
      }

      if (!item.userName || item.userName.trim() === '') {
        alert('El nombre de usuario es obligatorio.');
        return;
      }

      if (!item.firstName || item.firstName.trim() === '') {
        alert('El nombre es obligatorio.');
        return;
      }

      if (!item.lastName || item.lastName.trim() === '') {
        alert('El apellido es obligatorio.');
        return;
      }

      if (!item.userTypeId || item.userTypeId <= 0) {
        alert('Debe seleccionar un tipo de usuario válido.');
        return;
      }

      this.userUpdated = {
        userTypeId: item.userTypeId,
        userName: item.userName,
        firstName: item.firstName,
        lastName: item.lastName,
      }
      this.SelectedUserId = item.id;
 
      this.userService.updateUser(this.SelectedUserId, this.userUpdated).subscribe({
        next: () => {
          alert('Usuario actualizado correctamente');
          setTimeout(() => this.loadUsers(), 100); 
          this.showEditUserModal = false; 
        },
        error: (err) => {console.log('error actualizando el usuario.', err)}
      });
    }

    closeUpdatePaymentMethodModal() { this.showUpdatePaymentMethodModal = false; }

    openUpdatePaymentModal(item: PaymentMethod){

      this.paymentMethodUpdate = {
        commission : item.commission,
      }
      this.SelectedPaymentMethodId = item.id;
      this.SelectedPaymentMethodName = item.name;
      this.showUpdatePaymentMethodModal = true;
    }
    
    updatePaymentMethod(id: number,dto: UpdatePaymentMethodDTO){

      if (!id) {
      alert('⚠️ Debes seleccionar un método de pago.');
      return;
      }
      if (dto.commission === undefined || dto.commission < 0 || dto.commission > 100) {
        alert('⚠️ La comisión debe ser un valor entre 0 y 100.');
        return;
      }

      this.paymentMethodService.UpdatePaymentMethod(id,dto).subscribe({
        next: () => {
          alert('metodo de pago actualizado!');
          setTimeout(() => this.loadPaymentMethods(), 100); 
        },
        error: (err) =>{console.log('error al actualizar el metodo de pago: ',err)} 
      });
      this.closeUpdatePaymentMethodModal();
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
        alert('⚠️ Debes ingresar un nombre de metodo de pago.');
        return;
      }

      if (dto.commission === undefined || dto.commission < 0 || dto.commission > 100) {
        alert('⚠️ La comisión debe ser un valor entre 0 y 100.');
        return;
      }

      this.paymentMethodService.createPaymentMethod(dto).subscribe({
        next: () => {
          alert('Metodo de pago creado con exito!');
          setTimeout(() => this.loadPaymentMethods(), 100); 
          this.closeCreatePaymentMethodModal();
        },
        error: (err) => {console.log('error al crear metodo de pago', err)}
      });
    }

  deletePaymentMethod(id: number){
      if (!id) {
      console.log('id de medio de pago no valido.');
      return;
      }

      if (confirm(`¿Seguro que quieres borrar el metodo de pago?`)){ 
      this.paymentMethodService.deletePaymentMethod(id).subscribe({
        next: () => {
          alert('Metodo de pago eliminao con exito.');
          setTimeout(() => this.loadPaymentMethods(), 100); 
        },
        error: (err) => {console.log('error al eliminar el metodo de pago', err)}
      });
      }
  }
  
}
