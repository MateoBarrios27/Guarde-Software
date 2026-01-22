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
  totals = { depo: 0, casa: 0, pagado: 0, retiros: 0, extras: 0 };

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
    // 1. Sumar columnas
    this.totals = this.items.reduce((acc, curr) => ({
      depo: acc.depo + (Number(curr.depo) || 0),
      casa: acc.casa + (Number(curr.casa) || 0),
      pagado: acc.pagado + (Number(curr.pagado) || 0),
      retiros: acc.retiros + (Number(curr.retiros) || 0),
      extras: acc.extras + (Number(curr.extras) || 0),
    }), { depo: 0, casa: 0, pagado: 0, retiros: 0, extras: 0 });

    // 2. Actualizar el total de gastos en el resumen
    // Asumimos que "Gastos" es la suma de todo lo que sale (CASA + PAGADO + RETIROS + EXTRAS)
    // OJO: DEPO suele ser ingreso manual o movimiento interno. Ajustar según lógica del cliente.
    // Según Excel: DEPO parece ser columna informativa o ingreso extra. 
    // Asumiremos GASTOS = CASA + PAGADO + RETIROS + EXTRAS
    this.summary.totalManualExpenses = this.totals.casa + this.totals.pagado + this.totals.retiros + this.totals.extras;
    
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

    this.selectedMonth = m;
    this.selectedYear = y;
    this.loadData();
  }
  
  onAccountChange(account: FinancialAccount): void {
      this.cashService.updateAccountBalance(account.id, account.balance).subscribe();
  }
}