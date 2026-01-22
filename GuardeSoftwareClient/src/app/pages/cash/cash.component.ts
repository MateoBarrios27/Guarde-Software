import { Component, OnInit } from '@angular/core';
import { CashService } from '../../core/services/cash-service/cash.service';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import Swal from 'sweetalert2';
import { IconComponent } from "../../shared/components/icon/icon.component";
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CashFlowItem, FinancialAccount, MonthlySummary } from '../../core/models/cash';

@Component({
  selector: 'app-cash',
  templateUrl: './cash.component.html',
  styleUrls: ['./cash.component.css'],
  imports: [IconComponent, CommonModule, FormsModule]
})
export class CashComponent implements OnInit {
  
  // Estado de Fechas
  currentDate = new Date();
  selectedMonth = this.currentDate.getMonth() + 1;
  selectedYear = this.currentDate.getFullYear();
  
  // Datos
  items: CashFlowItem[] = [];
  summary: MonthlySummary = {
    totalSystemIncome: 0, totalAdvancePayments: 0, totalManualExpenses: 0, netBalance: 0, pendingCollection: 0
  };
  accounts: FinancialAccount[] = [];

  // Totales Locales de la Tabla (Columnas)
  totals = { 
    depo: 0, 
    casa: 0, 
    pagado: 0, 
    retiros: 0, 
    extras: 0,
    // Nuevos calculados
    aPagar: 0,      // (Depo + Casa)
    faltaPagar: 0   // (A Pagar - Pagado)
  };

  // Control de Guardado Automático
  private saveSubject = new Subject<CashFlowItem>();
  isLoading = false;

  constructor(private cashService: CashService) {
    // Configurar debounce: Espera 1s después de que el usuario deja de escribir para guardar
    this.saveSubject.pipe(
      debounceTime(1000) 
    ).subscribe(item => this.saveItem(item));
  }

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.isLoading = true;
    
    // 1. Cargar Items Manuales
    this.cashService.getItems(this.selectedMonth, this.selectedYear).subscribe(data => {
      this.items = data;
      // Si no hay items, agregamos una fila vacía para empezar
      if (this.items.length === 0) this.addNewRow(); 
      this.calculateLocalTotals();
      this.isLoading = false;
    });

    // 2. Cargar Resumen Automático (Ingresos del Sistema)
    this.cashService.getMonthlySummary(this.selectedMonth, this.selectedYear).subscribe(sum => {
      this.summary = sum;
      this.calculateNetBalance(); // Recalcular con los gastos manuales cargados
    });

    // 3. Cargar Cuentas
    this.cashService.getAccounts().subscribe(acc => this.accounts = acc);
  }

  // --- Lógica de Tabla Editable ---

  addNewRow(): void {
    const newItem: CashFlowItem = {
      date: new Date().toISOString().split('T')[0], // Hoy
      description: '',
      depo: 0, casa: 0, pagado: 0, retiros: 0, extras: 0
    };
    this.items.push(newItem);
  }

  onItemChange(item: CashFlowItem): void {
    this.calculateLocalTotals(); // Actualizar totales visuales inmediatamente
    this.saveSubject.next(item); // Encolar guardado
  }

  saveItem(item: CashFlowItem): void {
    this.cashService.upsertItem(item).subscribe(id => {
      item.id = id; // Asignar ID si era nuevo
      console.log('Item guardado/actualizado');
    });
  }

  deleteItem(item: CashFlowItem, index: number): void {
    if (!item.id) {
      this.items.splice(index, 1); // Si no está en BD, solo borrar del array
      return;
    }

    Swal.fire({
      title: '¿Eliminar concepto?',
      text: "No podrás revertir esto.",
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#d33',
      confirmButtonText: 'Sí, eliminar'
    }).then((result) => {
      if (result.isConfirmed) {
        this.cashService.deleteItem(item.id!).subscribe(() => {
          this.items.splice(index, 1);
          this.calculateLocalTotals();
        });
      }
    });
  }

  // --- Cálculos ---

  calculateLocalTotals(): void {
    // 1. Sumar columnas individuales
    this.totals = this.items.reduce((acc, curr) => ({
      ...acc, // Mantenemos propiedades previas para sobreescribir
      depo: acc.depo + (Number(curr.depo) || 0),
      casa: acc.casa + (Number(curr.casa) || 0),
      pagado: acc.pagado + (Number(curr.pagado) || 0),
      retiros: acc.retiros + (Number(curr.retiros) || 0),
      extras: acc.extras + (Number(curr.extras) || 0),
    }), { 
      depo: 0, casa: 0, pagado: 0, retiros: 0, extras: 0, 
      aPagar: 0, faltaPagar: 0 // Reset de nuevos valores
    });

    // 2. Cálculos de Agrupación (Lógica de Negocio Solicitada)
    
    // "A PAGAR" = Gastos previstos (Depo + Casa)
    this.totals.aPagar = this.totals.depo + this.totals.casa;

    // "FALTA PAGAR" = Lo que debía pagar menos lo que realmente pagué
    this.totals.faltaPagar = this.totals.aPagar - this.totals.pagado;

    // 3. Actualizar resumen para el cálculo de Ganancia Neta
    // Aquí la lógica depende de qué considera el cliente "Ganancia Real".
    // Opción A (Caja Real): Ingresos - Lo que realmente salió (Pagado + Retiros + Extras)
    // Opción B (Devengado): Ingresos - Lo que debería salir (A Pagar + Retiros + Extras)
    
    // Usaremos Opción A (Caja Real) que suele ser lo que buscan en estas planillas:
    const totalSalidasReales = this.totals.pagado + this.totals.retiros + this.totals.extras;
    
    this.summary.totalManualExpenses = totalSalidasReales;
    this.calculateNetBalance();
  }

  calculateNetBalance(): void {
    // Ganancia = (Ingresos Sistema + Pagos Adelantados) - Gastos Manuales
    // Nota: Ajustar fórmula según si DEPO suma o no.
    const totalIncome = (this.summary.totalSystemIncome || 0) + (this.summary.totalAdvancePayments || 0);
    this.summary.netBalance = totalIncome - this.summary.totalManualExpenses;
  }

  // --- Cambio de Mes ---
  changeMonth(delta: number): void {
    let m = this.selectedMonth + delta;
    let y = this.selectedYear;
    
    if (m > 12) { m = 1; y++; }
    if (m < 1) { m = 12; y--; }

    // VALIDACIÓN: No ir al futuro
    const today = new Date();
    // Creamos fecha del primer dia del mes seleccionado
    const selectedDate = new Date(y, m - 1, 1);
    // Creamos fecha del primer dia del mes actual
    const currentDate = new Date(today.getFullYear(), today.getMonth(), 1);

    if (selectedDate > currentDate) {
        Swal.fire('Atención', 'No se puede gestionar la caja de meses futuros.', 'warning');
        return;
    }

    this.selectedMonth = m;
    this.selectedYear = y;
    this.loadData();
  }


  onAccountChange(account: FinancialAccount): void {
    // Persistencia inmediata al cambiar el valor
    this.cashService.updateAccountBalance(account.id, account.balance).subscribe({
        error: () => Swal.fire('Error', 'No se pudo actualizar el saldo', 'error')
    });
  }

  addAccount(): void {
    Swal.fire({
      title: 'Nueva Cuenta / Caja',
      html: `
        <input id="acc-name" class="swal2-input" placeholder="Nombre (ej: Banco Galicia)">
        <select id="acc-type" class="swal2-input">
          <option value="Banco">Banco</option>
          <option value="Caja Fuerte">Caja Fuerte</option>
          <option value="Billetera">Billetera</option>
          <option value="Otro">Otro</option>
        </select>
      `,
      showCancelButton: true,
      confirmButtonText: 'Crear',
      preConfirm: () => {
        const name = (document.getElementById('acc-name') as HTMLInputElement).value;
        const type = (document.getElementById('acc-type') as HTMLSelectElement).value;
        if (!name) Swal.showValidationMessage('El nombre es requerido');
        return { name, type, balance: 0, currency: 'ARS' } as FinancialAccount;
      }
    }).then((result) => {
      if (result.isConfirmed) {
        this.cashService.createAccount(result.value).subscribe(id => {
          const newAcc = { ...result.value, id };
          this.accounts.push(newAcc);
          Swal.fire('Creada', 'La cuenta ha sido agregada.', 'success');
        });
      }
    });
  }

  deleteAccount(account: FinancialAccount, index: number): void {
    Swal.fire({
      title: '¿Eliminar cuenta?',
      text: `Se borrará "${account.name}" y su saldo actual.`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#d33',
      confirmButtonText: 'Sí, eliminar'
    }).then((result) => {
      if (result.isConfirmed) {
        this.cashService.deleteAccount(account.id).subscribe(() => {
          this.accounts.splice(index, 1);
        });
      }
    });
  }
}