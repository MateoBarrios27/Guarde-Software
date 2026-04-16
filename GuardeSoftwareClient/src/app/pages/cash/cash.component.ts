import { Component, ElementRef, HostListener, OnInit, ViewChild } from '@angular/core';
import { CashService } from '../../core/services/cash-service/cash.service';
import { Subject } from 'rxjs';
import { debounceTime, groupBy, mergeMap } from 'rxjs/operators';
import Swal from 'sweetalert2';
import { IconComponent } from "../../shared/components/icon/icon.component";
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CashFlowItem, FinancialAccount, MonthlySummary } from '../../core/models/cash';
import { CurrencyFormatDirective } from '../../shared/directives/currency-format.directive';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';

@Component({ 
  selector: 'app-cash',
  templateUrl: './cash.component.html',
  styleUrls: ['./cash.component.css'],
  imports: [IconComponent, CommonModule, FormsModule, CurrencyFormatDirective, DragDropModule]
})
export class CashComponent implements OnInit {
  
  currentDate = new Date();
  selectedMonth = this.currentDate.getMonth() + 1;
  selectedYear = this.currentDate.getFullYear();
  activeCommentItem: CashFlowItem | null = null;
  
  items: CashFlowItem[] = [];
  summary: MonthlySummary = {
    totalSystemIncome: 0, totalAdvancePayments: 0, totalManualExpenses: 0, netBalance: 0, pendingCollection: 0, abono: 0
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

  accountTotals = { 
    ars: { total: 0, banks: 0, cash: 0, others: 0 },
    usd: { total: 0, banks: 0, cash: 0, others: 0 }
  };

  usdExchangeRate: number = 1;
  private saveSubject = new Subject<CashFlowItem>();
  isLoading = false;
  searchTerm: string = '';
  searchDate: string = '';
  filteredItems: any[] = [];

  constructor(private cashService: CashService) {
    this.saveSubject.pipe(
      groupBy(item => item), 
      mergeMap(group => group.pipe(debounceTime(400))) 
    ).subscribe(item => this.saveItem(item));
  }

  ngOnInit(): void {
    const savedMonth = localStorage.getItem('cash_selected_month');
    const savedYear = localStorage.getItem('cash_selected_year');

    if (savedMonth && savedYear) {
      this.selectedMonth = parseInt(savedMonth, 10);
      this.selectedYear = parseInt(savedYear, 10);
    } else {
      const today = new Date();
      this.selectedMonth = today.getMonth() + 1;
      this.selectedYear = today.getFullYear();
    }

    this.loadData();
  }

  loadData(): void {
    this.isLoading = true;
    
    this.cashService.getItems(this.selectedMonth, this.selectedYear).subscribe(data => {

      this.items = data.map(item => {
        if (item.date && item.date.includes('T')) {
          item.date = item.date.split('T')[0];
        }
        item.depo = item.depo === 0 ? null as any : item.depo;
        item.casa = item.casa === 0 ? null as any : item.casa;
        item.retiros = item.retiros === 0 ? null as any : item.retiros;
        item.extras = item.extras === 0 ? null as any : item.extras;
        return item;
      });
      
      this.sortItems();
      this.filterItems();
      if (this.items.length === 0) this.addNewRow(); 
      this.calculateLocalTotals();
      
      this.cashService.getUsdRate(this.selectedMonth, this.selectedYear).subscribe(rate => {
        this.usdExchangeRate = rate;

        this.cashService.getAccounts(this.selectedMonth, this.selectedYear).subscribe(acc => {
            this.accounts = acc.sort((a, b) => (a.displayOrder || 0) - (b.displayOrder || 0));
            this.calculateAccountTotals();
        });
      });

      this.cashService.getMonthlySummary(this.selectedMonth, this.selectedYear).subscribe(sum => {
        this.summary = sum;
        this.calculateNetBalance();
      });

      this.isLoading = false;
    });
  }

  sortItems(): void {
    this.items.sort((a, b) => {
      const orderA = a.displayOrder || 0;
      const orderB = b.displayOrder || 0;

      if (orderA !== orderB) {
        return orderA - orderB;
      }
      
      const dateA = (a.date && a.date !== '') ? new Date(a.date).getTime() : 0;
      const dateB = (b.date && b.date !== '') ? new Date(b.date).getTime() : 0;
      
      return dateA - dateB; 
    });
  }

  addNewRow(): void {
    const newItem: CashFlowItem = {
      date: null as any,
      description: '',
      comment: '',
      depo: null as any, 
      casa: null as any, 
      isPaid: false, 
      retiros: null as any, 
      extras: null as any, 
      replicationState: 0
    };
    
    this.items.push(newItem);

    this.searchTerm = ''; 
    this.searchDate = '';
    this.filterItems();
  }

  toggleReplication(item: CashFlowItem): void {
    item.replicationState = (item.replicationState + 1) % 3;
    this.onItemChange(item);
  }

  get filterMinDate(): string {
    const y = this.selectedYear;
    const m = this.selectedMonth.toString().padStart(2, '0');
    return `${y}-${m}-01`;
  }

  get filterMaxDate(): string {
    const y = this.selectedYear;
    const m = this.selectedMonth;
    const lastDay = new Date(y, m, 0).getDate(); 
    const mStr = m.toString().padStart(2, '0');
    return `${y}-${mStr}-${lastDay.toString().padStart(2, '0')}`;
  }

  onItemChange(item: CashFlowItem): void {
    if (item.date === '') {
      item.date = null as any;
    }
    this.calculateLocalTotals(); 
    this.saveSubject.next(item); 
  }

  saveItem(item: CashFlowItem): void {

    const payloadToSave: CashFlowItem = {
      ...item,
      depo: item.depo || 0,
      casa: item.casa || 0,
      retiros: item.retiros || 0,
      extras: item.extras || 0
    };

    this.cashService.upsertItem(payloadToSave, this.selectedMonth, this.selectedYear).subscribe(id => {
      item.id = id;
    });
  }

  deleteItem(item: any): void {
    Swal.fire({
      title: '¿Eliminar concepto?',
      text: "No podrás revertir esto.",
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#d33',
      confirmButtonText: 'Sí, eliminar'
    }).then((result) => {
      if (result.isConfirmed) {
        const realIndex = this.items.indexOf(item);
        
        this.cashService.deleteItem(item.id!).subscribe(() => {
          if (realIndex !== -1) {
            this.items.splice(realIndex, 1);
            this.filterItems(); 
            this.calculateLocalTotals(); 
          }
        });
      }
    });
  }

  calculateLocalTotals(): void {
    this.totals = { depo: 0, casa: 0, retiros: 0, extras: 0, pagado: 0, aPagar: 0, faltaPagar: 0 };

    this.filteredItems.forEach(item => {
      this.totals.depo += Number(item.depo) || 0;
      this.totals.casa += Number(item.casa) || 0;
      this.totals.retiros += Number(item.retiros) || 0;
      this.totals.extras += Number(item.extras) || 0;

      const costoFila = (Number(item.depo) || 0) + 
                        (Number(item.casa) || 0);

      if (item.isPaid) {
        this.totals.pagado += costoFila;
      }
    });

    this.totals.aPagar = this.totals.depo + this.totals.casa;
    this.totals.faltaPagar = this.totals.aPagar - this.totals.pagado; 
  }

  calculateNetBalance(): void {
    const totalRealIncome = (this.summary.totalSystemIncome || 0) + 
                            (this.summary.totalAdvancePayments || 0);
                            
    this.summary.netBalance = totalRealIncome - this.summary.totalManualExpenses;
  }

  changeMonth(delta: number): void {
    let m = this.selectedMonth + delta;
    let y = this.selectedYear;
    
    if (m > 12) { 
        m = 1; 
        y++; 
    }
    if (m < 1) { 
        m = 12; 
        y--; 
    }

    if (y < 2026) {
        Swal.fire('Atención', 'No se pueden consultar o planificar datos anteriores al 2026.', 'warning');
        return;
    }
    
    this.selectedMonth = m;
    this.selectedYear = y;

    localStorage.setItem('cash_selected_month', this.selectedMonth.toString());
    localStorage.setItem('cash_selected_year', this.selectedYear.toString());

    this.loadData();
  }

  onAccountChange(account: FinancialAccount): void {
    this.calculateAccountTotals();
    this.cashService.updateAccountBalance(account.id!, account.balance, this.selectedMonth, this.selectedYear).subscribe({
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
          <option value="Caja">Caja</option>
          <option value="Otro">Otro</option>
        </select>
        <select id="acc-currency" class="swal2-input">
          <option value="ARS">Pesos Argentinos (ARS)</option>
          <option value="USD">Dólares (USD)</option>
        </select>
      `,
      showCancelButton: true,
      confirmButtonText: 'Crear',
      preConfirm: () => {
        const name = (document.getElementById('acc-name') as HTMLInputElement).value;
        const type = (document.getElementById('acc-type') as HTMLSelectElement).value;
        const currency = (document.getElementById('acc-currency') as HTMLSelectElement).value;
        if (!name) Swal.showValidationMessage('El nombre es requerido');
        return { name, type, balance: 0, currency } as FinancialAccount;
      }
    }).then((result) => {
      if (result.isConfirmed) {
        this.cashService.createAccount(result.value, this.selectedMonth, this.selectedYear).subscribe(id => {
          const newAcc = { ...result.value, id };
          this.accounts.push(newAcc);
          this.calculateAccountTotals(); 
          Swal.fire('Creada', 'La cuenta ha sido agregada.', 'success');
          console.log('Cuenta creada:', newAcc);
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
    this.accountTotals = {
      ars: { total: 0, banks: 0, cash: 0, others: 0 },
      usd: { total: 0, banks: 0, cash: 0, others: 0 }
    };

    this.accounts.forEach(curr => {
      const bal = Number(curr.balance) || 0;
      
      const currency = curr.currency === 'USD' ? 'usd' : 'ars';

      this.accountTotals[currency].total += bal;

      if (curr.type === 'Banco') {
        this.accountTotals[currency].banks += bal;
      } else if (['Caja Fuerte', 'Billetera', 'Caja'].includes(curr.type)) {
        this.accountTotals[currency].cash += bal;
      } else if (curr.type === 'Otro') { 
        this.accountTotals[currency].others += bal;
      }
    });
  } 

  filterItems(): void {
    const term = this.searchTerm.toLowerCase().trim();
    const dateFilter = this.searchDate;
    
    this.filteredItems = this.items.filter(item => {
      const matchesText = !term || (item.description || '').toLowerCase().includes(term);
      const matchesDate = !dateFilter || item.date === dateFilter;

      return matchesText && matchesDate;
    });

    this.calculateLocalTotals();
  }

  clearDateFilter(): void {
    this.searchDate = '';
    this.filterItems();
  }

  toggleComment(item: CashFlowItem): void {
    if (this.activeCommentItem === item) {
      this.activeCommentItem = null;
    } else {
      this.activeCommentItem = item;
      setTimeout(() => {
        const activeTextarea = document.getElementById('excel-comment-' + item.id) as HTMLTextAreaElement;
        if (activeTextarea) {
          activeTextarea.style.height = 'auto';
          activeTextarea.style.height = activeTextarea.scrollHeight + 'px';
          activeTextarea.focus();
        }
      }, 0);
    }
  }

  closeComment(item: CashFlowItem): void {
    this.activeCommentItem = null;
    this.onItemChange(item); 
  }

  autoResizeTextarea(event: Event): void {
    const textarea = event.target as HTMLTextAreaElement;
    textarea.style.height = 'auto';
    textarea.style.height = textarea.scrollHeight + 'px';
  }

  blurInput(event: Event): void {
    (event.target as HTMLElement).blur();
  }

  onCommentEnter(event: Event, item: CashFlowItem): void {
    event.preventDefault();
    this.closeComment(item);
  }

  dropItem(event: CdkDragDrop<CashFlowItem[]>) {
    moveItemInArray(this.filteredItems, event.previousIndex, event.currentIndex);

    const reorderedItems = this.filteredItems.map((item, index) => ({
      id: item.id,
      displayOrder: index
    }));

    this.cashService.updateItemsOrder(reorderedItems).subscribe();
  }

  dropAccount(event: CdkDragDrop<FinancialAccount[]>) {
    moveItemInArray(this.accounts, event.previousIndex, event.currentIndex);

    const reorderedAccounts = this.accounts.map((acc, index) => ({
      id: acc.id!,
      displayOrder: index
    }));

    this.cashService.updateAccountsOrder(reorderedAccounts).subscribe();
  }

  onExchangeRateChange(): void {
    this.cashService.updateUsdRate(this.usdExchangeRate, this.selectedMonth, this.selectedYear).subscribe();
    this.calculateAccountTotals();
  }

  deleteComment(item: CashFlowItem): void {
    item.comment = '';
    this.closeComment(item); 
  }

  onAccountColorChange(account: FinancialAccount): void {
    if (!account.color) account.color = '#1f2937';

    this.cashService.updateAccountColor(account.id, account.color).subscribe({
        error: () => Swal.fire('Error', 'No se pudo guardar el color de la cuenta', 'error')
    });
  }

  // --- NUEVAS VARIABLES PARA SELECCIÓN DE SALDOS ---
  @ViewChild('saldosContainer') saldosContainer!: ElementRef;
  selectedAccountIds: number[] = [];

  // --- LÓGICA DE CLIC AFUERA ---
  @HostListener('document:click', ['$event'])
  onClickOutside(event: Event) {
    // Si hay cuentas seleccionadas, verificamos si el clic fue AFUERA del contenedor de saldos
    if (this.selectedAccountIds.length > 0 && this.saldosContainer) {
      const clickedInside = this.saldosContainer.nativeElement.contains(event.target);
      if (!clickedInside) {
        this.selectedAccountIds = []; // Limpiamos la selección
      }
    }
  }

  // --- LÓGICA DE SELECCIÓN Y CÁLCULO ---
  toggleAccountSelection(account: FinancialAccount): void {
    if (!account.id) return;
    
    const index = this.selectedAccountIds.indexOf(account.id);
    if (index > -1) {
      this.selectedAccountIds.splice(index, 1); // Deseleccionar
    } else {
      this.selectedAccountIds.push(account.id); // Seleccionar
    }
  }

  get selectedAccountsSumARS(): number {
    let sum = 0;
    this.accounts.forEach(acc => {
      if (acc.id && this.selectedAccountIds.includes(acc.id)) {
        const balance = Number(acc.balance) || 0;
        // Si la cuenta está en dólares, la pasamos a pesos para la suma total
        sum += acc.currency === 'USD' ? balance * (this.usdExchangeRate || 1) : balance;
      }
    });
    return sum;
  }

  get monthName(): string {
    const date = new Date(this.selectedYear, this.selectedMonth - 1, 1);
    return date.toLocaleString('es-ES', { month: 'long', year: 'numeric' });
  }
}