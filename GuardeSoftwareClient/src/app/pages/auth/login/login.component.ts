import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth-service/auth.service';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  imports: [FormsModule, CommonModule],
})
export class LoginComponent {
  userName = '';
  password = '';
  loading = false;
  errorMessage = '';

  constructor(
    private authService: AuthService,
    private router: Router
  ) {}

  submit() {
    if (!this.userName || !this.password) {
      this.errorMessage = 'Debe ingresar usuario y contraseña';
      return;
    }

    // this.loading = true;
    this.errorMessage = '';

    this.authService.login(this.userName, this.password).subscribe({
      next: () => {
      //  this.loading = false;
       this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        // this.loading = false;
        if (err.status === 401) {
            this.errorMessage = 'Usuario o contraseña incorrectos';
        } else {
            this.errorMessage = 'Ocurrió un error al intentar iniciar sesión';
          }
      }
    });
  }
}
