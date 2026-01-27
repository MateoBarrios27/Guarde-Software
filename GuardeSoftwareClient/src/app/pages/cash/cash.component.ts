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
  
  currentDate = new Date();
  selectedMonth = this.currentDate.getMonth() + 1;
  selectedYear = this.currentDate.getFullYear();
  
  items: CashFlowItem[] = [];
  summary: MonthlySummary = {
    totalSystemIncome: 0, totalAdvancePayments: 0, totalManualExpenses: 0, netBalance: 0, pendingCollection: 0
  };
  accounts: FinancialAccount[] = [];

  totals = { 
    depo: 0, 
    casa: 0, 
    pagado: 0, 
    retiros: 0, 
    extras: 0,
    aPagar: 0, 
    faltaPagar: 0 
  };

  accountTotals = { total: 0, banks: 0, cash: 0 };

  // Control de Guardado Automático
  private saveSubject = new Subject<CashFlowItem>();
  isLoading = false;

  constructor(private cashService: CashService) {
    this.saveSubject.pipe(
      debounceTime(1000) 
    ).subscribe(item => this.saveItem(item));
  }

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
  this.isLoading = true;
  
  this.cashService.getItems(this.selectedMonth, this.selectedYear).subscribe(data => {
    this.items = data.map(item => {
      if (item.date && item.date.includes('T')) {
        item.date = item.date.split('T')[0];
      }
      return item;
    });

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
    this.cashService.getAccounts().subscribe(acc => {
        this.accounts = acc;
        this.calculateAccountTotals();
    });
  }


  addNewRow(): void {
  const newItem: CashFlowItem = {
    date: new Date().toISOString().split('T')[0], 
    description: '',
    depo: 0, casa: 0, pagado: 0, retiros: 0, extras: 0
  };
  this.items.push(newItem);
}

  onItemChange(item: CashFlowItem): void {
    this.calculateLocalTotals(); 
    this.saveSubject.next(item); 
  }

  saveItem(item: CashFlowItem): void {
    this.cashService.upsertItem(item).subscribe(id => {
      item.id = id;
    });
  }

  deleteItem(item: CashFlowItem, index: number): void {
    if (!item.id) {
      this.items.splice(index, 1); 
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
      ...acc,
      depo: acc.depo + (Number(curr.depo) || 0),
      casa: acc.casa + (Number(curr.casa) || 0),
      pagado: acc.pagado + (Number(curr.pagado) || 0),
      retiros: acc.retiros + (Number(curr.retiros) || 0),
      extras: acc.extras + (Number(curr.extras) || 0),
    }), { 
      depo: 0, casa: 0, pagado: 0, retiros: 0, extras: 0, 
      aPagar: 0, faltaPagar: 0 // Reset de nuevos valores
    });

    this.totals.aPagar = this.totals.depo + this.totals.casa;

    this.totals.faltaPagar = this.totals.aPagar - this.totals.pagado;

    const totalSalidasReales = this.totals.pagado + this.totals.retiros + this.totals.extras;
    
    this.summary.totalManualExpenses = totalSalidasReales;
    this.calculateNetBalance();
  }

  calculateNetBalance(): void {
    const totalIncome = (this.summary.totalSystemIncome || 0) + (this.summary.totalAdvancePayments || 0);
    this.summary.netBalance = totalIncome - this.summary.totalManualExpenses;
  }

  changeMonth(delta: number): void {
    let m = this.selectedMonth + delta;
    let y = this.selectedYear;
    
    if (m > 12) { m = 1; y++; }
    if (m < 1) { m = 12; y--; }

    const today = new Date();
    const selectedDate = new Date(y, m - 1, 1);
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
    this.calculateAccountTotals(); // <--- RECALCULAR AL EDITAR MONTO
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
          this.calculateAccountTotals(); // <--- RECALCULAR AL AGREGAR
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
          this.calculateAccountTotals();
        });
      }
    });
  }

  calculateAccountTotals(): void {
    this.accountTotals = this.accounts.reduce((acc, curr) => {
      const bal = Number(curr.balance) || 0;

      acc.total += bal;

      if (curr.type === 'Banco') {
        acc.banks += bal;
      } else if (['Caja Fuerte', 'Billetera', 'Caja'].includes(curr.type)) {
        acc.cash += bal;
      }

      return acc;
    }, { total: 0, banks: 0, cash: 0 });
  }
}