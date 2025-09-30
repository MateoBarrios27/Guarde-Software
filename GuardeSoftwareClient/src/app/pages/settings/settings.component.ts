import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { User } from '../../core/models/user';
import { PaymentMethod } from '../../core/models/payment-method';
import { UserService } from '../../core/services/user-service/user.service';
import { PaymentMethodService } from '../../core/services/paymentMethod-service/payment-method.service';

@Component({
  selector: 'app-settings',
  imports: [CommonModule, FormsModule],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.css'
})
export class SettingsComponent implements OnInit {

  constructor(private userService: UserService, private paymentMethodService: PaymentMethodService) {}

  activeSection: string = 'usuarios';
  users : User[] = [];
  paymentMethods : PaymentMethod [] = [];

  ngOnInit(): void {
    this.loadUsers();
    this.loadPaymentMethods();
  }

  loadUsers(): void{
    this.userService.getUser().subscribe({
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
    this.paymentMethodService.getPaymentMethod().subscribe({
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
}
