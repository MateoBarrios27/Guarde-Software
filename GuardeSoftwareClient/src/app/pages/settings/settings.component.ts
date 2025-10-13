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
    password: '',
    userTypeId: 0,
  }

  showCreateUserModal = false;
  showEditUserModal = false;

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
        console.log(data);
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
        console.log(data);
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

  closeCreateUserModal() { this.showCreateUserModal = false; }
  
  openUpdateUserModal(){ this.showCreateUserModal = true; }

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
      password: item.password
    }
     this.showEditUserModal = true; 
    }
}
