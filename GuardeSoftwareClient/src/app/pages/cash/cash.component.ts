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
import { ScrollingModule } from '@angular/cdk/scrolling';

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
  imports: [IconComponent, CommonModule, FormsModule, CurrencyFormatDirective, DragDropModule, ScrollingModule]
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
  
  // Totales estáticos para el Panel Izquierdo (Siempre es el mes seleccionado)
  totals = { depo: 0, casa: 0, pagado: 0, retiros: 0, extras: 0, iaia: 0, aPagar: 0, faltaPagar: 0 };
  
  // Totales dinámicos para el Footer de la Tabla (Cambian con la búsqueda)
  tableTotals = { depo: 0, casa: 0, retiros: 0, extras: 0, iaia: 0, pagado: 0 };

  accountTotals = { 
    ars: { total: 0, banks: 0, cash: 0, others: 0 },
    usd: { total: 0, banks: 0, cash: 0, others: 0 }
  };

  usdExchangeRate: number = 1;
  private saveSubject = new Subject<CashFlowItem>();
  isLoading = false;
  
  // --- VARIABLES DE BÚSQUEDA Y RANGO ---
  searchTerm: string = '';
  searchDateFrom: string = '';
  searchDateTo: string = '';
  isHistoricalView: boolean = false; 
  filteredItems: any[] = [];

  @ViewChild('topAnchor') topAnchor!: ElementRef;
  @ViewChild('bottomAnchor') bottomAnchor!: ElementRef;
  
  isScrolledDown: boolean = false;
  private scrollObserver!: IntersectionObserver;

  // --- VARIABLES SISTEMA CTRL+Z ---
  public undoStack: UndoAction[] = [];
  private capturedAccountState: string = '';
  private capturedItemState: string = '';

  // --- VARIABLES IVA COMPRAS ---
  showIvaComprasModal: boolean = false;
  ivaCompras: any[] = [];
  totalIvaCompras: number = 0;
  newIvaCompra = { date: '', amount: null as any, comment: '' };

  // --- VARIABLES ADELANTOS ---
  showAdvancesModal: boolean = false;
  selectedItemForAdvances: CashFlowItem | null = null;
  advances: any[] = [];
  newAdvance = { date: '', amount: null as any };
  advancesTotalAmount: number = 0;

  constructor(private cashService: CashService) {
    this.saveSubject.pipe(
      groupBy(item => item), 
      mergeMap(group => group.pipe(debounceTime(400))) 
    ).subscribe(item => this.saveItem(item));
  }

  // --- VARIABLES PARA SELECCIÓN MÚLTIPLE DE FILAS (GASTOS) ---
  selectedItemIds: number[] = [];

  toggleItemSelection(item: CashFlowItem): void {
    if (!item.id || item.id === 0) return; 
    
    const index = this.selectedItemIds.indexOf(item.id);
    if (index > -1) {
      this.selectedItemIds.splice(index, 1);
    } else {
      this.selectedItemIds.push(item.id);
    }
  }

  clearItemSelection(): void {
    this.selectedItemIds = [];
  }

  get selectedItemsSumARS(): number {
    let sum = 0;
    this.filteredItems.forEach(item => {
      if (item.id && this.selectedItemIds.includes(item.id)) {
        const depo = Number(item.depo) || 0;
        const casa = Number(item.casa) || 0;
        const iaia = Number(item.iaia) || 0;
        const retiros = Number(item.retiros) || 0;
        sum += (depo + casa + iaia + retiros);
      }
    });
    return sum;
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
    if (this.isHistoricalView) return; // Desactivar Ctrl+Z si está en modo reporte

    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'z') {
      const activeElement = document.activeElement as HTMLElement;
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

  // --- EJECUCIÓN DEL DESHACER ---
  undoLastAction() {
    if (this.undoStack.length === 0) return;
    const action = this.undoStack.pop()!;
    this.saveUndoStack();

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
        this.showUndoToast(`Cuenta revertida al estado de ${this.getMonthNameByNum(m)}`);
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
            this.showUndoToast(`Cuenta restaurada en ${this.getMonthNameByNum(m)}`);
          }
        });
        break;

      case 'ITEM_EDIT':
        const restoredItem = { ...action.oldState, id: action.targetId };
        this.saveItemGlobal(restoredItem, m, y); 
        
        if (isCurrentMonth) {
          const itemMem = this.items.find(i => i.id === action.targetId);
          if (itemMem) {
             Object.assign(itemMem, action.oldState);
             this.items = [...this.items]; 
             this.filterItems();
             this.calculateMonthlyTotals();
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
          extras: itemToRestore.extras || 0,
          iaia: itemToRestore.iaia || 0
        };

        this.cashService.upsertItem(payload, m, y).subscribe({
          next: (newId) => {
            action.oldState.id = newId; 
            if (isCurrentMonth) {
              this.items.push(action.oldState);
              this.items = [...this.items];
              this.sortItems();
              this.filterItems();
              this.calculateMonthlyTotals();
            }
            this.showUndoToast(`Gasto restaurado en ${this.getMonthNameByNum(m)}`);
          }
        });
        break;
    }
  }

  private saveItemGlobal(item: CashFlowItem, month: number, year: number): void {
    const payloadToSave: CashFlowItem = {
      ...item,
      depo: item.depo || 0,
      casa: item.casa || 0,
      retiros: item.retiros || 0,
      extras: item.extras || 0,
      iaia : item.iaia || 0
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
        item.iaia = item.iaia === 0 ? null as any : item.iaia;
        return item;
      });

      this.cashService.getIvaCompras(this.selectedMonth, this.selectedYear).subscribe(ivaData => {
        this.ivaCompras = ivaData.map(iva => ({
          id: iva.id || iva.Id,
          date: iva.date || iva.Date,
          amount: iva.amount || iva.Amount,
          comment: iva.comment || iva.Comment
        }));
        this.calculateTotalIvaCompras();
      });

      this.sortItems();
      this.filterItems(); 
      if (this.items.length === 0) this.addNewRow(); 
      
      // Calculamos los totales fijos de la izquierda
      this.calculateMonthlyTotals();
      
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

      if (orderA !== orderB) return orderA - orderB;
      
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
    iaia: null as any, 
    replicationState: 0,
    color: null as any
  };
  
  this.items.push(newItem);
  this.searchTerm = ''; 
  this.searchDateFrom = '';
  this.searchDateTo = '';
  this.filterItems();
}

  insertRowBelow(afterItem: CashFlowItem): void {
    const newItem: CashFlowItem = {
      date: null as any,
      description: '',
      comment: '',
      depo: null as any,
      casa: null as any,
      isPaid: false,
      retiros: null as any,
      extras: null as any,
      iaia: null as any,
      replicationState: 0,
      color: null as any
    };

    // Buscar en el array principal usando referencia directa o por id
    let indexInItems = this.items.indexOf(afterItem);
    if (indexInItems === -1 && afterItem.id) {
      indexInItems = this.items.findIndex(i => i.id === afterItem.id);
    }

    if (indexInItems !== -1) {
      this.items.splice(indexInItems + 1, 0, newItem);
    } else {
      this.items.push(newItem);
    }

    // Reasignar displayOrder y rowNum secuencialmente
    // IMPORTANTE: no llamar sortItems() acá porque reordenaría el array
    // y deshace el splice. El array ya está en el orden visual correcto.
    this.items.forEach((item, idx) => {
      item.displayOrder = idx;
      item.rowNum = idx + 1;
    });

    this.searchTerm = '';
    this.searchDateFrom = '';
    this.searchDateTo = '';
    this.filterItems();

    // Persistir el nuevo orden (solo items ya guardados)
    const reorderedItems = this.items
      .filter(item => item.id && item.id > 0)
      .map(item => ({ id: item.id!, displayOrder: item.displayOrder || 0 }));

    if (reorderedItems.length > 0) {
      this.cashService.updateItemsOrder(reorderedItems).subscribe();
    }
  }

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
    if (item.date === '') item.date = null as any;
    
    // Al modificar, actualizamos ambos paneles
    this.calculateMonthlyTotals(); 
    this.calculateTableTotals();
    
    this.saveSubject.next(item); 
  }

  saveItem(item: CashFlowItem): void {
    const payloadToSave: CashFlowItem = {
      ...item,
      depo: item.depo || 0,
      casa: item.casa || 0,
      retiros: item.retiros || 0,
      extras: item.extras || 0,
      iaia : item.iaia || 0
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
        const oldState = JSON.parse(JSON.stringify(item)); 
        const realIndex = this.items.indexOf(item);
        
        if (!item.id) {
            if (realIndex !== -1) {
              this.undoStack.push({ type: 'ITEM_DELETE', oldState, anchorMonth: this.selectedMonth, anchorYear: this.selectedYear }); 
              this.items.splice(realIndex, 1);
              this.filterItems(); 
              this.calculateMonthlyTotals(); 
            }
            return;
        }

        this.cashService.deleteItem(item.id).subscribe(() => {
          if (realIndex !== -1) {
            this.undoStack.push({ type: 'ITEM_DELETE', targetId: item.id, oldState, anchorMonth: this.selectedMonth, anchorYear: this.selectedYear }); 
            this.saveUndoStack(); 
            this.items.splice(realIndex, 1);
            this.filterItems(); 
            this.calculateMonthlyTotals(); 
          }
        });
      }
    });
  }

  // 1. Agregá este método para forzar la fecha del calendario al hacer clic
setDefaultDate(item: any): void {
  if (!item.date) {
    const mm = String(this.selectedMonth).padStart(2, '0');
    item.date = `${this.selectedYear}-${mm}-01`;
    // Disparamos el guardado automático
    if (this.checkItemChange) this.checkItemChange(item);
    if (this.onItemChange) this.onItemChange(item);
  }
}

// 2. Reemplazá tu método filterItems() por esta versión "omnipotente"
filterItems(): void {
  const term = this.searchTerm.toLowerCase().trim();
  
  if (this.searchDateFrom && this.searchDateTo) {
    if (!this.isHistoricalView) this.clearItemSelection();
    this.isHistoricalView = true;
    this.isLoading = true;
    
    this.cashService.getHistoricalReport(this.searchDateFrom, this.searchDateTo).subscribe({
      next: (data) => {
        data.forEach((item, index) => {
          item.id = -(index + 1);
          item.rowNum = index + 1; 
        });
        
        this.filteredItems = data.filter(item => {
          if (!term) return true;
          // Buscador global (Busca por monto, nro de fila, concepto, nota, etc)
          const searchStr = `${item.rowNum} ${item.description || ''} ${item.comment || ''} ${item.depo || ''} ${item.casa || ''} ${item.retiros || ''} ${item.iaia || ''}`.toLowerCase();
          return searchStr.includes(term);
        });
        
        this.calculateTableTotals();
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        Swal.fire('Error', 'No se pudo generar el reporte histórico', 'error');
      }
    });
  } 
  else {
    if (this.isHistoricalView) this.clearItemSelection();
    this.isHistoricalView = false;

    this.items.forEach((item, index) => {
      item.rowNum = index + 1;
    });

    this.filteredItems = this.items.filter(item => {
      const searchStr = `${item.rowNum} ${item.description || ''} ${item.comment || ''} ${item.depo || ''} ${item.casa || ''} ${item.retiros || ''} ${item.iaia || ''}`.toLowerCase();
      const matchesText = !term || searchStr.includes(term);
      const matchesDate = !this.searchDateFrom || item.date === this.searchDateFrom;
      return matchesText && matchesDate;
    });
    this.calculateTableTotals();
  }
}

  clearDateFilter(): void {
    this.searchDateFrom = '';
    this.searchDateTo = '';
    this.searchTerm = '';
    this.filterItems();
  }

  // --- MATEMÁTICAS SEPARADAS ---
  
  // 1. Calcula siempre sobre 'this.items' (Panel Izquierdo - Datos Reales del Mes)
  calculateMonthlyTotals(): void {
    this.totals = { depo: 0, casa: 0, retiros: 0, extras: 0, iaia: 0, pagado: 0, aPagar: 0, faltaPagar: 0 };

    this.items.forEach(item => {
      this.totals.depo += Number(item.depo) || 0;
      this.totals.casa += Number(item.casa) || 0;
      this.totals.retiros += Number(item.retiros) || 0;
      this.totals.extras += Number(item.extras) || 0;
      this.totals.iaia += Number(item.iaia) || 0;

      const costoFila = (Number(item.depo) || 0) + (Number(item.casa) || 0);
      if (item.hasAdvances) {
        this.totals.pagado += Number(item.totalAdvances) || 0;
      } else if (item.isPaid) {
        this.totals.pagado += costoFila;
      }
    });

    this.totals.aPagar = this.totals.depo + this.totals.casa;
    this.totals.faltaPagar = this.totals.aPagar - this.totals.pagado; 
  }

  // 2. Calcula siempre sobre 'this.filteredItems' (Pie de la Tabla - Cambia con búsquedas)
  calculateTableTotals(): void {
    this.tableTotals = { depo: 0, casa: 0, retiros: 0, extras: 0, iaia: 0, pagado: 0 };

    this.filteredItems.forEach(item => {
      this.tableTotals.depo += Number(item.depo) || 0;
      this.tableTotals.casa += Number(item.casa) || 0;
      this.tableTotals.retiros += Number(item.retiros) || 0;
      this.tableTotals.extras += Number(item.extras) || 0;
      this.tableTotals.iaia += Number(item.iaia) || 0;

      const costoFila = (Number(item.depo) || 0) + (Number(item.casa) || 0);
      if (item.hasAdvances) {
        this.tableTotals.pagado += Number(item.totalAdvances) || 0;
      } else if (item.isPaid) {
        this.tableTotals.pagado += costoFila;
      }
    });
  }

  calculateNetBalance(): void {
    const totalRealIncome = (this.summary.totalSystemIncome || 0) + 
                            (this.summary.totalAdvancePayments || 0);
                            
    this.summary.netBalance = totalRealIncome - this.summary.totalManualExpenses;
  }

  changeMonth(delta: number): void {
    this.searchDateFrom = '';
    this.searchDateTo = '';
    this.searchTerm = '';
    this.isHistoricalView = false;
    this.clearItemSelection();
    
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
    
    const targetDate = new Date(y, m - 1, 1);
    const targetMonthName = targetDate.toLocaleString('es-ES', { month: 'long', year: 'numeric' });

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
    if (this.isHistoricalView) return; 

    const draggedItem = this.filteredItems[event.previousIndex];
    const isMultiDrag = this.selectedItemIds.length > 1 && this.selectedItemIds.includes(draggedItem.id!);

    if (isMultiDrag) {
      const itemsToMove = this.filteredItems.filter(item => this.selectedItemIds.includes(item.id!));
      const remainingItems = this.filteredItems.filter(item => !this.selectedItemIds.includes(item.id!));
      
      // Simulamos el movimiento del ítem que el usuario agarró
      const simulated = [...this.filteredItems];
      moveItemInArray(simulated, event.previousIndex, event.currentIndex);
      
      // Contamos cuántos ítems "NO seleccionados" quedaron por encima de la posición de destino
      let unselectedBeforeTarget = 0;
      for (let i = 0; i < event.currentIndex; i++) {
          if (!this.selectedItemIds.includes(simulated[i].id!)) {
              unselectedBeforeTarget++;
          }
      }

      // Insertamos el bloque completo justo después de esos elementos
      remainingItems.splice(unselectedBeforeTarget, 0, ...itemsToMove);
      this.filteredItems = remainingItems;
    } else {
      moveItemInArray(this.filteredItems, event.previousIndex, event.currentIndex);
    }

    // Reasignamos el orden y estampamos el nuevo N° de fila para que se actualice visualmente
    const reorderedItems = this.filteredItems.map((item, index) => {
      item.displayOrder = index;
      (item as any).rowNum = index + 1; 

      const originalItem = this.items.find(i => i.id === item.id);
      if (originalItem) {
          originalItem.displayOrder = index;
          (originalItem as any).rowNum = index + 1; 
      }

      return { id: item.id!, displayOrder: index };
    });
    
    this.sortItems(); 
    this.cashService.updateItemsOrder(reorderedItems).subscribe();
  }

  getSelectedAccountsList(): FinancialAccount[] {
  return this.accounts.filter(acc => this.selectedAccountIds.includes(acc.id!));
}

// 2. Método dropAccount con soporte de multi-drag y cálculo milimétrico de inserción
dropAccount(event: CdkDragDrop<FinancialAccount[]>) {
  const draggedAccount = this.accounts[event.previousIndex];
  const isMultiDrag = this.selectedAccountIds.length > 1 && this.selectedAccountIds.includes(draggedAccount.id!);

  if (isMultiDrag) {
    const accountsToMove = this.accounts.filter(acc => this.selectedAccountIds.includes(acc.id!));
    const remainingAccounts = this.accounts.filter(acc => !this.selectedAccountIds.includes(acc.id!));
    
    // Simulamos el movimiento del ítem que el usuario agarró
    const simulated = [...this.accounts];
    moveItemInArray(simulated, event.previousIndex, event.currentIndex);
    
    // Contamos cuántas cuentas "NO seleccionadas" quedaron por encima de la posición de destino
    let unselectedBeforeTarget = 0;
    for (let i = 0; i < event.currentIndex; i++) {
        if (!this.selectedAccountIds.includes(simulated[i].id!)) {
            unselectedBeforeTarget++;
        }
    }

    // Insertamos el bloque completo justo después de esos elementos
    remainingAccounts.splice(unselectedBeforeTarget, 0, ...accountsToMove);
    this.accounts = remainingAccounts;
  } else {
    moveItemInArray(this.accounts, event.previousIndex, event.currentIndex);
  }

  // Reasignamos el orden de display
  const reorderedAccounts = this.accounts.map((acc, index) => {
    acc.displayOrder = index;
    return { id: acc.id!, displayOrder: index };
  });
  
  this.cashService.updateAccountsOrder(reorderedAccounts).subscribe();
}

  onExchangeRateChange(): void {
    this.cashService.updateUsdRate(this.usdExchangeRate, this.selectedMonth, this.selectedYear).subscribe();
    this.calculateAccountTotals();
  }

  deleteComment(item: CashFlowItem): void {
    this.captureItem(item);
    item.comment = '';
    item.commentUpdatedAt = new Date();
    this.checkItemChange(item);
    this.closeComment(item); 
  }

  onAccountColorChange(account: FinancialAccount): void {
    if (!account.color) account.color = '#1f2937';

    this.cashService.updateAccountColor(account.id!, account.color).subscribe({
        error: () => Swal.fire('Error', 'No se pudo guardar el color de la cuenta', 'error')
    });
  }

  resetAccountColor(account: FinancialAccount): void {
    account.color = '#1f2937';
    this.onAccountColorChange(account);
  }

  resetItemColor(item: CashFlowItem): void {
    item.color = null as any;
    this.checkItemChange(item);
    this.onItemChange(item);
  }

  getFormattedUpdatedDate(date?: Date | string | null): string {
    if (!date) return '';
    const d = new Date(date);
    if (isNaN(d.getTime())) return '';
    const day = String(d.getDate()).padStart(2, '0');
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const year = d.getFullYear();
    const hours = String(d.getHours()).padStart(2, '0');
    const minutes = String(d.getMinutes()).padStart(2, '0');
    return `Modif: ${day}/${month}/${year} ${hours}:${minutes}`;
  }

  @ViewChild('saldosContainer') saldosContainer!: ElementRef;
  @ViewChild('planillaContainer') planillaContainer!: ElementRef; 
  selectedAccountIds: number[] = [];

  @HostListener('document:click', ['$event'])
  onClickOutside(event: Event) {
    if (this.activeCommentItem) {
      const target = event.target as HTMLElement;
      if (!target.closest('.note-popup-container') && !target.closest('.note-toggle-btn')) {
        this.closeComment(this.activeCommentItem);
      }
    }

    if (this.selectedAccountIds.length > 0 && this.saldosContainer) {
      const clickedInside = this.saldosContainer.nativeElement.contains(event.target);
      if (!clickedInside) this.selectedAccountIds = [];
    }

    if (this.selectedItemIds.length > 0 && this.planillaContainer) {
      const clickedInside = this.planillaContainer.nativeElement.contains(event.target);
      if (!clickedInside) this.selectedItemIds = [];
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

  calculateTotalIvaCompras() {
    this.totalIvaCompras = this.ivaCompras.reduce((sum, item) => {
      const val = item.amount ?? item.Amount ?? 0;
      return sum + (Number(val) || 0);
    }, 0);
  }

  openIvaComprasModal() {
    this.showIvaComprasModal = true;
    const today = new Date();
    let initialDate: Date;

    if (this.selectedYear === today.getFullYear() && this.selectedMonth === (today.getMonth() + 1)) {
      initialDate = today;
    } else {
      initialDate = new Date(this.selectedYear, this.selectedMonth - 1, 1);
    }

    const yyyy = initialDate.getFullYear();
    const mm = String(initialDate.getMonth() + 1).padStart(2, '0');
    const dd = String(initialDate.getDate()).padStart(2, '0');

    this.newIvaCompra = { date: `${yyyy}-${mm}-${dd}`, amount: null as any, comment: '' };
  }

  closeIvaComprasModal() {
    this.showIvaComprasModal = false;
  }

  saveIvaCompra() {
    if (!this.newIvaCompra.amount || this.newIvaCompra.amount <= 0) {
      Swal.fire('Atención', 'Ingresa un monto válido.', 'warning');
      return;
    }
    if (!this.newIvaCompra.date) {
      Swal.fire('Atención', 'La fecha es obligatoria.', 'warning');
      return;
    }

    const payload = {
      month: this.selectedMonth,
      year: this.selectedYear,
      date: this.newIvaCompra.date,
      amount: this.newIvaCompra.amount,
      comment: this.newIvaCompra.comment
    };

    this.cashService.addIvaCompra(payload).subscribe({
      next: (newId) => {
        this.ivaCompras.unshift({ ...payload, id: newId });
        this.calculateTotalIvaCompras();
        this.newIvaCompra.amount = null as any;
        this.newIvaCompra.comment = '';
        Swal.fire({ toast: true, position: 'bottom-end', icon: 'success', title: 'Agregado', showConfirmButton: false, timer: 2000 });
      }
    });
  }

  deleteIvaCompra(id: number, index: number) {
    this.cashService.deleteIvaCompra(id).subscribe(() => {
      this.ivaCompras.splice(index, 1);
      this.calculateTotalIvaCompras();
    });
  }

// --- GRAB MULTIPLE ITEMS
  getSelectedItemsList(): CashFlowItem[] {
    return this.filteredItems.filter(item => this.selectedItemIds.includes(item.id!));
  }

  // Evalúa si todos los elementos actualmente listados con monto en esa columna ya están seleccionados
isColumnAllSelected(field: string): boolean {
  const itemsWithValues = this.filteredItems.filter(
    item => item.id && item[field] !== null && item[field] !== undefined && item[field] !== 0 && item[field] !== ''
  );
  if (itemsWithValues.length === 0) return false;
  return itemsWithValues.every(item => this.selectedItemIds.includes(item.id));
}

// Selecciona o deselecciona en bloque solo los elementos visibles que tengan valor en la columna
toggleSelectAllColumn(field: string): void {
  const itemsWithValues = this.filteredItems.filter(
    item => item.id && item[field] !== null && item[field] !== undefined && item[field] !== 0 && item[field] !== ''
  );
  
  const allSelected = itemsWithValues.every(item => this.selectedItemIds.includes(item.id));

  if (allSelected) {
    // Si ya estaban todos marcados, removemos sus IDs del listado de selección global
    const idsToRemove = itemsWithValues.map(item => item.id);
    this.selectedItemIds = this.selectedItemIds.filter(id => !idsToRemove.includes(id));
  } else {
    // Si faltaba alguno, los agregamos asegurando no duplicar IDs existentes
    itemsWithValues.forEach(item => {
      if (!this.selectedItemIds.includes(item.id)) {
        this.selectedItemIds.push(item.id);
      }
    });
  }
}

// --- ADELANTOS (Pagos Parciales) ---

openAdvancesModal(item: CashFlowItem): void {
  if (!item.id || item.id === 0) {
    // Guardar primero el item si no tiene ID
    const payloadToSave: CashFlowItem = {
      ...item,
      depo: item.depo || 0,
      casa: item.casa || 0,
      retiros: item.retiros || 0,
      extras: item.extras || 0,
      iaia: item.iaia || 0
    };
    this.cashService.upsertItem(payloadToSave, this.selectedMonth, this.selectedYear).subscribe(id => {
      item.id = id;
      this.loadAdvancesAndOpenModal(item);
    });
  } else {
    this.loadAdvancesAndOpenModal(item);
  }
}

private loadAdvancesAndOpenModal(item: CashFlowItem): void {
  this.selectedItemForAdvances = item;
  this.cashService.getAdvances(item.id!).subscribe(data => {
    this.advances = data.map(adv => ({
      id: adv.id || adv.Id,
      itemId: adv.itemId || adv.ItemId,
      date: adv.date || adv.Date,
      amount: adv.amount || adv.Amount
    }));
    this.calculateAdvancesTotal();
    this.showAdvancesModal = true;

    const today = new Date();
    let initialDate: Date;
    if (this.selectedYear === today.getFullYear() && this.selectedMonth === (today.getMonth() + 1)) {
      initialDate = today;
    } else {
      initialDate = new Date(this.selectedYear, this.selectedMonth - 1, 1);
    }
    const yyyy = initialDate.getFullYear();
    const mm = String(initialDate.getMonth() + 1).padStart(2, '0');
    const dd = String(initialDate.getDate()).padStart(2, '0');
    this.newAdvance = { date: `${yyyy}-${mm}-${dd}`, amount: null as any };
  });
}

closeAdvancesModal(): void {
  this.showAdvancesModal = false;
  this.selectedItemForAdvances = null;
  this.advances = [];
}

calculateAdvancesTotal(): void {
  this.advancesTotalAmount = this.advances.reduce((sum, adv) => {
    return sum + (Number(adv.amount) || 0);
  }, 0);
}

getAdvancesItemTotal(): number {
  if (!this.selectedItemForAdvances) return 0;
  return (Number(this.selectedItemForAdvances.depo) || 0) + (Number(this.selectedItemForAdvances.casa) || 0);
}

getAdvancesRemaining(): number {
  return this.getAdvancesItemTotal() - this.advancesTotalAmount;
}

getAdvancesProgress(): number {
  const total = this.getAdvancesItemTotal();
  if (total <= 0) return 0;
  return Math.min((this.advancesTotalAmount / total) * 100, 100);
}

saveAdvance(): void {
  if (!this.newAdvance.amount || this.newAdvance.amount <= 0) {
    Swal.fire('Atención', 'Ingresa un monto válido.', 'warning');
    return;
  }
  if (!this.newAdvance.date) {
    Swal.fire('Atención', 'La fecha es obligatoria.', 'warning');
    return;
  }

  const payload = {
    itemId: this.selectedItemForAdvances!.id,
    date: this.newAdvance.date,
    amount: this.newAdvance.amount
  };

  this.cashService.addAdvance(this.selectedItemForAdvances!.id!, payload).subscribe({
    next: (newId) => {
      this.advances.unshift({ ...payload, id: newId });
      this.calculateAdvancesTotal();

      // Actualizar el item en la tabla principal
      this.selectedItemForAdvances!.hasAdvances = true;
      this.selectedItemForAdvances!.totalAdvances = this.advancesTotalAmount;
      this.calculateMonthlyTotals();
      this.calculateTableTotals();

      this.newAdvance.amount = null as any;
      Swal.fire({ toast: true, position: 'bottom-end', icon: 'success', title: 'Adelanto registrado', showConfirmButton: false, timer: 2000 });
    }
  });
}

deleteAdvance(id: number, index: number): void {
  this.cashService.deleteAdvance(id).subscribe(() => {
    this.advances.splice(index, 1);
    this.calculateAdvancesTotal();

    // Actualizar el item en la tabla principal
    this.selectedItemForAdvances!.totalAdvances = this.advancesTotalAmount;
    this.selectedItemForAdvances!.hasAdvances = this.advances.length > 0;
    this.calculateMonthlyTotals();
    this.calculateTableTotals();
  });
}

}