import { AfterViewInit, Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild } from '@angular/core';
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

// --- Structure Historial (CTRL+Z) ---
export type ActionType = 'ACCOUNT_EDIT' | 'ACCOUNT_CREATE' | 'ACCOUNT_DELETE' | 'ITEM_EDIT' | 'ITEM_CREATE' | 'ITEM_DELETE';

export interface UndoAction {
  type: ActionType;
  targetId?: number; 
  oldState?: any;
  anchorMonth: number;
  anchorYear: number;
}

@Component({ 
  selector: 'app-cash',
  templateUrl: './cash.component.html',
  styleUrls: ['./cash.component.css'],
  imports: [IconComponent, CommonModule, FormsModule, CurrencyFormatDirective, DragDropModule]
})
export class CashComponent implements OnInit, AfterViewInit, OnDestroy {
  
  currentDate = new Date();
  selectedMonth = this.currentDate.getMonth() + 1;
  selectedYear = this.currentDate.getFullYear();
  activeCommentItem: CashFlowItem | null = null;
  
  items: CashFlowItem[] = [];

  summary: MonthlySummary = {
    totalSystemIncome: 0,
     totalAdvancePayments: 0,
      totalManualExpenses: 0,
      netBalance: 0,
      pendingCollection: 0,
      abono: 0,
      ivaFacturaA: 0,
      ivaFacturaB: 0
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

  @ViewChild('topAnchor') topAnchor!: ElementRef;
  @ViewChild('bottomAnchor') bottomAnchor!: ElementRef;
  
  isScrolledDown: boolean = false;
  private scrollObserver!: IntersectionObserver;

  // --- VARIABLES SISTEMA CTRL+Z ---
  public undoStack: UndoAction[] = [];
  private capturedAccountState: string = '';
  private capturedItemState: string = '';

  constructor(private cashService: CashService) {
    this.saveSubject.pipe(
      groupBy(item => item), 
      mergeMap(group => group.pipe(debounceTime(400))) 
    ).subscribe(item => this.saveItem(item));
  }

  private saveUndoStack(): void {
    sessionStorage.setItem('cash_undo_stack', JSON.stringify(this.undoStack));
  }

  private loadUndoStack(): void {
    const saved = sessionStorage.getItem('cash_undo_stack');
    if (saved) {
      this.undoStack = JSON.parse(saved);
    }
  }

  @HostListener('window:keydown', ['$event'])
  handleKeyDown(event: KeyboardEvent) {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'z') {
      const activeElement = document.activeElement as HTMLElement;
      // Si está escribiendo dentro de un input, dejamos que el navegador haga su "deshacer" nativo de texto
      if (activeElement && (activeElement.tagName === 'INPUT' || activeElement.tagName === 'TEXTAREA')) {
        return;
      }
      event.preventDefault();
      this.undoLastAction();
    }
  }

  // --- CAPTURADORES DE ESTADOS ---
  captureAccount(acc: FinancialAccount) {
    this.capturedAccountState = JSON.stringify(acc);
  }

  checkAccountChange(acc: FinancialAccount) {
    if (!this.capturedAccountState) return;
    const oldState = JSON.parse(this.capturedAccountState);
    if (oldState.name !== acc.name || oldState.color !== acc.color || oldState.balance !== acc.balance) {
      this.undoStack.push({ 
        type: 'ACCOUNT_EDIT', 
        targetId: acc.id,
        oldState,
        anchorMonth: this.selectedMonth,
        anchorYear: this.selectedYear
      });
      this.saveUndoStack();
    }
    this.capturedAccountState = '';
  }

  captureItem(item: CashFlowItem) {
    this.capturedItemState = JSON.stringify(item);
  }

  checkItemChange(item: CashFlowItem) {
    if (!this.capturedItemState) return;
    const oldState = JSON.parse(this.capturedItemState);
    if (JSON.stringify(oldState) !== JSON.stringify(item)) {
      this.undoStack.push({ 
        type: 'ITEM_EDIT', 
        targetId: item.id, 
        oldState,
        anchorMonth: this.selectedMonth,
        anchorYear: this.selectedYear
      });
      this.saveUndoStack(); 
    }
    this.capturedItemState = '';
  }

  // --- EJECUCIÓN DEL DESHACER (CON STORAGE Y ANCLAS) ---
  undoLastAction() {
    if (this.undoStack.length === 0) return;
    const action = this.undoStack.pop()!;
    this.saveUndoStack(); // Actualizamos el storage al sacar un elemento

    const m = action.anchorMonth;
    const y = action.anchorYear;
    const isCurrentMonth = (m === this.selectedMonth && y === this.selectedYear);

    switch (action.type) {
      case 'ACCOUNT_EDIT':
        this.cashService.updateAccountName(action.targetId!, action.oldState.name).subscribe();
        this.cashService.updateAccountColor(action.targetId!, action.oldState.color).subscribe();
        this.cashService.updateAccountBalance(action.targetId!, action.oldState.balance, m, y).subscribe();
        
        if (isCurrentMonth) {
          const acc = this.accounts.find(a => a.id === action.targetId);
          if (acc) Object.assign(acc, action.oldState);
          this.calculateAccountTotals();
        }
        this.showUndoToast(`Cuenta revertida al estado de ${this.getMonthNameByNum(m)}, `);
        break;

      case 'ACCOUNT_CREATE':
        this.cashService.deleteAccount(action.targetId!).subscribe();
        if (isCurrentMonth) {
          this.accounts = this.accounts.filter(a => a.id !== action.targetId);
          this.calculateAccountTotals();
        }
        this.showUndoToast('Creación de cuenta desecha');
        break;

      case 'ACCOUNT_DELETE':
        const accToRestore = { ...action.oldState };
        delete accToRestore.id;
        this.cashService.createAccount(accToRestore, m, y).subscribe({
          next: (newId) => {
            action.oldState.id = newId;
            if (isCurrentMonth) {
              this.accounts.push(action.oldState);
              this.accounts.sort((a, b) => (a.displayOrder || 0) - (b.displayOrder || 0));
              this.calculateAccountTotals();
            }
            this.showUndoToast(`Cuenta restaurada en ${this.getMonthNameByNum(m)}, refrescar si no aparece`);
          }
        });
        break;

      case 'ITEM_EDIT':
        // Reconstruimos el item para enviarlo a la BD
        const restoredItem = { ...action.oldState, id: action.targetId };
        this.saveItemGlobal(restoredItem, m, y); 
        
        if (isCurrentMonth) {
          const itemMem = this.items.find(i => i.id === action.targetId);
          if (itemMem) {
             Object.assign(itemMem, action.oldState);
             this.items = [...this.items]; // Forzamos el repintado
             this.filterItems();
             this.calculateLocalTotals();
          }
        }
        this.showUndoToast(`Gasto revertido en ${this.getMonthNameByNum(m)}`);
        break;

      case 'ITEM_DELETE':
        const itemToRestore = { ...action.oldState };
        itemToRestore.id = 0; 
        const payload: CashFlowItem = {
          ...itemToRestore,
          depo: itemToRestore.depo || 0,
          casa: itemToRestore.casa || 0,
          retiros: itemToRestore.retiros || 0,
          extras: itemToRestore.extras || 0
        };

        this.cashService.upsertItem(payload, m, y).subscribe({
          next: (newId) => {
            action.oldState.id = newId; 
            if (isCurrentMonth) {
              this.items.push(action.oldState);
              this.items = [...this.items]; // Forzamos el repintado
              this.sortItems();
              this.filterItems();
              this.calculateLocalTotals();
            }
            this.showUndoToast(`Gasto restaurado en ${this.getMonthNameByNum(m)}, refrescar si no aparece`);
          }
        });
        break;
    }
  }

  // --- HELPER PARA GUARDAR EN UN MES ESPECÍFICO ---
  private saveItemGlobal(item: CashFlowItem, month: number, year: number): void {
    const payloadToSave: CashFlowItem = {
      ...item,
      depo: item.depo || 0,
      casa: item.casa || 0,
      retiros: item.retiros || 0,
      extras: item.extras || 0
    };
    this.cashService.upsertItem(payloadToSave, month, year).subscribe();
  }

  private getMonthNameByNum(m: number): string {
    const date = new Date(2000, m - 1, 1);
    return date.toLocaleString('es-ES', { month: 'long' });
  }

  private showUndoToast(msg: string) {
    Swal.fire({ toast: true, position: 'bottom-end', icon: 'success', title: 'Deshecho (Ctrl+Z)', text: msg, showConfirmButton: false, timer: 3500 });
  }

  ngAfterViewInit() {
    this.scrollObserver = new IntersectionObserver(([entry]) => {
      this.isScrolledDown = !entry.isIntersecting;
    }, { threshold: 0 });

    if (this.topAnchor) {
      this.scrollObserver.observe(this.topAnchor.nativeElement);
    }
  }

  ngOnDestroy() {
    if (this.scrollObserver) {
      this.scrollObserver.disconnect();
    }
  }

  toggleScroll() {
    if (this.isScrolledDown) {
      this.topAnchor.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
    } else {
      this.bottomAnchor.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'end' });
    }
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

    this.loadUndoStack();
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

  // --- MÉTODOS DE TOGGLE Y LIMPIEZA CON CAPTURA (CTRL+Z) ---
  toggleReplication(item: CashFlowItem): void {
    this.captureItem(item);
    item.replicationState = (item.replicationState + 1) % 3;
    this.checkItemChange(item);
    this.onItemChange(item);
  }

  togglePaid(item: CashFlowItem): void {
    this.captureItem(item);
    item.isPaid = !item.isPaid;
    this.checkItemChange(item);
    this.onItemChange(item);
  }

  clearItemField(item: CashFlowItem, field: keyof CashFlowItem): void {
    this.captureItem(item);
    (item as any)[field] = null;
    this.checkItemChange(item);
    this.onItemChange(item);
  }

  clearAccountBalance(account: FinancialAccount): void {
    this.captureAccount(account);
    account.balance = 0;
    this.checkAccountChange(account);
    this.onAccountChange(account);
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
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#d33',
      confirmButtonText: 'Sí, eliminar'
    }).then((result) => {
      if (result.isConfirmed) {
        const oldState = JSON.parse(JSON.stringify(item)); // Capturamos clon exacto
        const realIndex = this.items.indexOf(item);
        
        if (!item.id) {
            if (realIndex !== -1) {
              this.undoStack.push({ type: 'ITEM_DELETE', oldState, anchorMonth: this.selectedMonth, anchorYear: this.selectedYear }); 
              this.items.splice(realIndex, 1);
              this.filterItems(); 
              this.calculateLocalTotals(); 
            }
            return;
        }

        this.cashService.deleteItem(item.id).subscribe(() => {
          if (realIndex !== -1) {
            this.undoStack.push({ type: 'ITEM_DELETE', targetId: item.id, oldState, anchorMonth: this.selectedMonth, anchorYear: this.selectedYear }); 
            this.saveUndoStack(); 
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
    
    // 1. Calculamos a qué mes/año quiere ir
    if (m > 12) { 
        m = 1; 
        y++; 
    }
    if (m < 1) { 
        m = 12; 
        y--; 
    }

    // 2. Validación de seguridad de año mínimo
    if (y < 2026) {
        Swal.fire('Atención', 'No se pueden consultar o planificar datos anteriores al 2026.', 'warning');
        return;
    }
    
    // 3. Obtenemos el nombre del mes al que va a ir (para mejor UX en la alerta)
    const targetDate = new Date(y, m - 1, 1);
    const targetMonthName = targetDate.toLocaleString('es-ES', { month: 'long', year: 'numeric' });

    // 4. Lanzamos la pregunta
    Swal.fire({
      title: `¿Ir a ${targetMonthName}?`,
      text: 'Se cargarán todos los movimientos y estadísticas de ese mes.',
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Sí, cambiar',
      cancelButtonText: 'Cancelar',
      confirmButtonColor: '#2563eb',
      cancelButtonColor: '#9ca3af'
    }).then((result) => {
      // 5. Si confirma, aplicamos los cambios y cargamos la data
      if (result.isConfirmed) {
        this.selectedMonth = m;
        this.selectedYear = y;

        localStorage.setItem('cash_selected_month', this.selectedMonth.toString());
        localStorage.setItem('cash_selected_year', this.selectedYear.toString());

        this.loadData();
      }
    });
  }

  onAccountChange(account: FinancialAccount): void {
    this.calculateAccountTotals();
    this.cashService.updateAccountBalance(account.id!, account.balance, this.selectedMonth, this.selectedYear).subscribe({
        error: () => Swal.fire('Error', 'No se pudo actualizar el saldo', 'error')
    });
  }

  onAccountNameChange(account: FinancialAccount): void {
    if (!account.name || account.name.trim() === '') {
      Swal.fire('Atención', 'El nombre de la cuenta no puede quedar vacío.', 'warning');
      return;
    }

    this.cashService.updateAccountName(account.id!, account.name).subscribe({
        error: () => Swal.fire('Error', 'No se pudo guardar el nuevo nombre de la cuenta', 'error')
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
          this.undoStack.push({ type: 'ACCOUNT_CREATE', targetId: newAcc.id, anchorMonth: this.selectedMonth, anchorYear: this.selectedYear });
          this.saveUndoStack();
          this.accounts.push(newAcc);
          this.calculateAccountTotals(); 
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
        const oldState = JSON.parse(JSON.stringify(account));
        this.cashService.deleteAccount(account.id!).subscribe(() => {
          this.undoStack.push({ type: 'ACCOUNT_DELETE', targetId: account.id, oldState, anchorMonth: this.selectedMonth, anchorYear: this.selectedYear });
          this.saveUndoStack();
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
    this.checkItemChange(item);
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
    const reorderedItems = this.filteredItems.map((item, index) => ({ id: item.id, displayOrder: index }));
    this.cashService.updateItemsOrder(reorderedItems).subscribe();
  }

  dropAccount(event: CdkDragDrop<FinancialAccount[]>) {
    moveItemInArray(this.accounts, event.previousIndex, event.currentIndex);
    const reorderedAccounts = this.accounts.map((acc, index) => ({ id: acc.id!, displayOrder: index }));
    this.cashService.updateAccountsOrder(reorderedAccounts).subscribe();
  }

  onExchangeRateChange(): void {
    this.cashService.updateUsdRate(this.usdExchangeRate, this.selectedMonth, this.selectedYear).subscribe();
    this.calculateAccountTotals();
  }

  deleteComment(item: CashFlowItem): void {
    this.captureItem(item);
    item.comment = '';
    this.checkItemChange(item);
    this.closeComment(item); 
  }

  onAccountColorChange(account: FinancialAccount): void {
    if (!account.color) account.color = '#1f2937';

    this.cashService.updateAccountColor(account.id!, account.color).subscribe({
        error: () => Swal.fire('Error', 'No se pudo guardar el color de la cuenta', 'error')
    });
  }

  @ViewChild('saldosContainer') saldosContainer!: ElementRef;
  selectedAccountIds: number[] = [];

  @HostListener('document:click', ['$event'])
  onClickOutside(event: Event) {
    if (this.selectedAccountIds.length > 0 && this.saldosContainer) {
      const clickedInside = this.saldosContainer.nativeElement.contains(event.target);
      if (!clickedInside) {
        this.selectedAccountIds = [];
      }
    }
  }

  toggleAccountSelection(account: FinancialAccount): void {
    if (!account.id) return;
    const index = this.selectedAccountIds.indexOf(account.id);
    if (index > -1) {
      this.selectedAccountIds.splice(index, 1);
    } else {
      this.selectedAccountIds.push(account.id);
    }
  }

  get selectedAccountsSumARS(): number {
    let sum = 0;
    this.accounts.forEach(acc => {
      if (acc.id && this.selectedAccountIds.includes(acc.id)) {
        const balance = Number(acc.balance) || 0;
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