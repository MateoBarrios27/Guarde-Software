import { AfterViewInit, Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { CashService } from '../../core/services/cash-service/cash.service';
import { CashSignalrService } from '../../core/services/cash-signalr/cash-signalr.service';
import { Subject, Subscription } from 'rxjs';
import { debounceTime, groupBy, mergeMap } from 'rxjs/operators';
import Swal from '../../shared/services/ui-alert.service';
import { IconComponent } from "../../shared/components/icon/icon.component";
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CashFlowItem, FinancialAccount, MonthlySummary } from '../../core/models/cash';
import { CurrencyFormatDirective } from '../../shared/directives/currency-format.directive';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { ScrollingModule } from '@angular/cdk/scrolling';
import { DeleteConfirmationService } from '../../shared/services/delete-confirmation.service';

// --- Structure Historial (CTRL+Z) ---
export type ActionType =
  | 'ACCOUNT_EDIT'
  | 'ACCOUNT_CREATE'
  | 'ACCOUNT_DELETE'
  | 'ITEM_EDIT'
  | 'ITEM_CREATE'
  | 'ITEM_DELETE'
  | 'IVA_CREATE'
  | 'IVA_DELETE'
  | 'ADVANCE_CREATE'
  | 'ADVANCE_DELETE';

export interface UndoAction {
  type: ActionType;
  targetId?: number; 
  oldState?: any;
  newState?: any;
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
  isCommentPinned: boolean = false;
  private commentHoverTimer: any = null;
  private commentLeaveTimer: any = null;
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
  
  // Array de columnas visibles guardado en localstorage para recordar la selección
  visibleColumns: string[] = ['depo', 'casa', 'iaia', 'retiros'];

  // Para evitar que el scroll salte al recargar los datos
  trackById(index: number, item: any): number | string {
    return item.id || index;
  }
  
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

  // --- VARIABLES SISTEMA CTRL+Z / CTRL+Y ---
  public undoStack: UndoAction[] = [];
  public redoStack: UndoAction[] = [];
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
  newAdvance = { date: '', amount: null as any, comment: '' };
  advancesTotalAmount: number = 0;

  private signalRSubscription?: Subscription;

  constructor(
    private cashService: CashService,
    private cashSignalrService: CashSignalrService,
    private deleteConfirmation: DeleteConfirmationService,
    private router: Router
  ) {
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
  private saveHistory(): void {
    sessionStorage.setItem('cash_undo_stack', JSON.stringify(this.undoStack));
    sessionStorage.setItem('cash_redo_stack', JSON.stringify(this.redoStack));
  }

  private loadHistory(): void {
    const savedUndo = sessionStorage.getItem('cash_undo_stack');
    const savedRedo = sessionStorage.getItem('cash_redo_stack');

    if (savedUndo) {
      this.undoStack = JSON.parse(savedUndo);
    }

    if (savedRedo) {
      this.redoStack = JSON.parse(savedRedo);
    }
  }

  private recordHistoryAction(action: UndoAction): void {
    this.undoStack.push(action);
    this.redoStack = [];
    this.saveHistory();
  }

  @HostListener('window:keydown', ['$event'])
  handleKeyDown(event: KeyboardEvent): void {
    // Las pantallas principales se conservan en caché al navegar. El listener
    // de Caja sigue existiendo en ese estado, pero no debe actuar fuera de
    // /cash.
    if (!this.isCashRouteActive()) return;

    if (event.key === 'Escape') {
      if (this.showAdvancesModal) {
        this.closeAdvancesModal();
      } else if (this.showIvaComprasModal) {
        this.closeIvaComprasModal();
      } else if (this.activeCommentItem) {
        this.activeCommentItem = null;
      }
      return;
    }

    if (this.isHistoricalView) return; // Desactivar Ctrl+Z si está en modo reporte

    if (event.altKey || this.isEditableTarget(event)) return;

    const hasModifier = event.ctrlKey || event.metaKey;
    if (!hasModifier) return;

    const key = event.key.toLowerCase();
    const isUndoShortcut = key === 'z' && !event.shiftKey;
    const isRedoShortcut = (key === 'y' && !event.shiftKey) || (key === 'z' && event.shiftKey);

    if (isUndoShortcut && this.undoStack.length > 0) {
      event.preventDefault();
      this.undoLastAction();
      return;
    }

    if (isRedoShortcut && this.redoStack.length > 0) {
      event.preventDefault();
      this.redoLastAction();
    }
  }

  private isCashRouteActive(): boolean {
    const currentUrl = this.router.url.split(/[?#]/, 1)[0];
    return currentUrl === '/cash' || currentUrl.startsWith('/cash/');
  }

  private isEditableTarget(event: KeyboardEvent): boolean {
    return this.isEditableElement(event.target) || this.isEditableElement(document.activeElement);
  }

  private isEditableElement(element: EventTarget | null): boolean {
    const htmlElement = element as HTMLElement | null;
    if (!htmlElement?.tagName) return false;

    return ['INPUT', 'TEXTAREA', 'SELECT'].includes(htmlElement.tagName)
      || htmlElement.isContentEditable;
  }

  // --- CAPTURADORES DE ESTADOS ---
  captureAccount(acc: FinancialAccount) {
    this.capturedAccountState = JSON.stringify(acc);
  }

  checkAccountChange(acc: FinancialAccount) {
    if (!this.capturedAccountState) return;
    const oldState = JSON.parse(this.capturedAccountState);
    if (oldState.name !== acc.name || oldState.color !== acc.color || oldState.balance !== acc.balance) {
      this.recordHistoryAction({
        type: 'ACCOUNT_EDIT', 
        targetId: acc.id,
        oldState,
        newState: JSON.parse(JSON.stringify(acc)),
        anchorMonth: this.selectedMonth,
        anchorYear: this.selectedYear
      });
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
      this.recordHistoryAction({
        type: 'ITEM_EDIT', 
        targetId: item.id, 
        oldState,
        newState: JSON.parse(JSON.stringify(item)),
        anchorMonth: this.selectedMonth,
        anchorYear: this.selectedYear
      });
    }
    this.capturedItemState = '';
  }

  // --- EJECUCIÓN DEL DESHACER / REHACER ---
  undoLastAction(): void {
    if (this.undoStack.length === 0) return;

    const action = this.undoStack[this.undoStack.length - 1];
    this.captureCurrentStateForRedo(action);
    this.undoStack.pop();
    this.redoStack.push(action);
    this.saveHistory();
    this.applyHistoryAction(action, 'undo');
  }

  redoLastAction(): void {
    if (this.redoStack.length === 0) return;

    const action = this.redoStack.pop()!;
    this.undoStack.push(action);
    this.saveHistory();
    this.applyHistoryAction(action, 'redo');
  }

  private captureCurrentStateForRedo(action: UndoAction): void {
    if (action.newState) return;
    if (action.anchorMonth !== this.selectedMonth || action.anchorYear !== this.selectedYear) return;

    if (action.type === 'ACCOUNT_EDIT' || action.type === 'ACCOUNT_CREATE') {
      const account = this.accounts.find(item => item.id === action.targetId);
      if (account) action.newState = JSON.parse(JSON.stringify(account));
      return;
    }

    if (action.type === 'ITEM_EDIT') {
      const item = this.items.find(currentItem => currentItem.id === action.targetId);
      if (item) action.newState = JSON.parse(JSON.stringify(item));
      return;
    }

    if (action.type === 'IVA_CREATE') {
      const ivaCompra = this.ivaCompras.find(currentIva => currentIva.id === action.targetId);
      if (ivaCompra) action.newState = JSON.parse(JSON.stringify(ivaCompra));
      return;
    }

    if (action.type === 'ADVANCE_CREATE') {
      const advance = this.advances.find(currentAdvance => currentAdvance.id === action.targetId);
      if (advance) action.newState = JSON.parse(JSON.stringify(advance));
    }
  }

  private applyHistoryAction(action: UndoAction, direction: 'undo' | 'redo'): void {
    const isUndo = direction === 'undo';
    const m = action.anchorMonth;
    const y = action.anchorYear;
    const isCurrentMonth = (m === this.selectedMonth && y === this.selectedYear);

    switch (action.type) {
      case 'ACCOUNT_EDIT': {
        const state = isUndo ? action.oldState : action.newState;
        if (!state) return;

        this.cashService.updateAccountName(action.targetId!, state.name).subscribe();
        this.cashService.updateAccountColor(action.targetId!, state.color).subscribe();
        this.cashService.updateAccountBalance(action.targetId!, state.balance, m, y).subscribe();

        if (isCurrentMonth) {
          const acc = this.accounts.find(a => a.id === action.targetId);
          if (acc) Object.assign(acc, state);
          this.calculateAccountTotals();
        }
        this.showHistoryToast(direction, `Cuenta ${isUndo ? 'revertida' : 'restaurada'} al estado de ${this.getMonthNameByNum(m)}`);
        break;
      }

      case 'ACCOUNT_CREATE': {
        if (isUndo) {
          this.cashService.deleteAccount(action.targetId!).subscribe({
            next: () => {
              if (isCurrentMonth) {
                this.accounts = this.accounts.filter(a => a.id !== action.targetId);
                this.calculateAccountTotals();
              }
              this.showHistoryToast(direction, 'Creación de cuenta deshecha');
            }
          });
          break;
        }

        if (!action.newState) return;
        const accountToRestore = { ...action.newState };
        delete accountToRestore.id;

        this.cashService.createAccount(accountToRestore, m, y).subscribe({
          next: (newId) => {
            action.targetId = newId;
            action.newState = { ...accountToRestore, id: newId };
            this.saveHistory();

            if (isCurrentMonth) {
              this.accounts.push(action.newState);
              this.accounts.sort((a, b) => (a.displayOrder || 0) - (b.displayOrder || 0));
              this.calculateAccountTotals();
            }
            this.showHistoryToast(direction, 'Creación de cuenta rehecha');
          }
        });
        break;
      }

      case 'ACCOUNT_DELETE': {
        if (isUndo) {
          const accToRestore = { ...action.oldState };
          delete accToRestore.id;

          this.cashService.createAccount(accToRestore, m, y).subscribe({
            next: (newId) => {
              action.targetId = newId;
              action.oldState = { ...accToRestore, id: newId };
              this.saveHistory();

              if (isCurrentMonth) {
                this.accounts.push(action.oldState);
                this.accounts.sort((a, b) => (a.displayOrder || 0) - (b.displayOrder || 0));
                this.calculateAccountTotals();
              }
              this.showHistoryToast(direction, `Cuenta restaurada en ${this.getMonthNameByNum(m)}`);
            }
          });
          break;
        }

        this.cashService.deleteAccount(action.targetId!).subscribe({
          next: () => {
            if (isCurrentMonth) {
              this.accounts = this.accounts.filter(a => a.id !== action.targetId);
              this.calculateAccountTotals();
            }
            this.showHistoryToast(direction, 'Eliminación de cuenta rehecha');
          }
        });
        break;
      }

      case 'ITEM_EDIT': {
        const state = isUndo ? action.oldState : action.newState;
        if (!state) return;

        const restoredItem = { ...state, id: action.targetId };
        this.saveItemGlobal(restoredItem, m, y);

        if (isCurrentMonth) {
          const itemMem = this.items.find(i => i.id === action.targetId);
          if (itemMem) {
            Object.assign(itemMem, state);
            this.items = [...this.items];
            this.filterItems();
            this.calculateMonthlyTotals();
          }
        }
        this.showHistoryToast(direction, `Gasto ${isUndo ? 'revertido' : 'restaurado'} en ${this.getMonthNameByNum(m)}`);
        break;
      }

      case 'IVA_CREATE': {
        if (isUndo) {
          this.cashService.deleteIvaCompra(action.targetId!).subscribe({
            next: () => {
              if (isCurrentMonth) this.removeIvaCompraLocally(action.targetId!);
              this.showHistoryToast(direction, 'Alta de compra de IVA deshecha');
            }
          });
          break;
        }

        if (!action.newState) return;
        const ivaToCreate = { ...action.newState };
        delete ivaToCreate.id;

        this.cashService.addIvaCompra(ivaToCreate).subscribe({
          next: (newId) => {
            action.targetId = newId;
            action.newState = { ...ivaToCreate, id: newId };
            if (isCurrentMonth) this.addIvaCompraLocally(action.newState);
            this.saveHistory();
            this.showHistoryToast(direction, 'Alta de compra de IVA rehecha');
          }
        });
        break;
      }

      case 'IVA_DELETE': {
        if (isUndo) {
          if (!action.oldState) return;
          const ivaToRestore = { ...action.oldState };
          delete ivaToRestore.id;

          this.cashService.addIvaCompra(ivaToRestore).subscribe({
            next: (newId) => {
              action.targetId = newId;
              action.oldState = { ...ivaToRestore, id: newId };
              if (isCurrentMonth) this.addIvaCompraLocally(action.oldState);
              this.saveHistory();
              this.showHistoryToast(direction, 'Compra de IVA restaurada');
            }
          });
          break;
        }

        this.cashService.deleteIvaCompra(action.targetId!).subscribe({
          next: () => {
            if (isCurrentMonth) this.removeIvaCompraLocally(action.targetId!);
            this.showHistoryToast(direction, 'Eliminación de compra de IVA rehecha');
          }
        });
        break;
      }

      case 'ADVANCE_CREATE': {
        if (isUndo) {
          this.cashService.deleteAdvance(action.targetId!).subscribe({
            next: () => {
              this.syncAdvanceLocally(action.newState, false);
              this.showHistoryToast(direction, 'Alta de adelanto deshecha');
            }
          });
          break;
        }

        if (!action.newState) return;
        const advanceToCreate = { ...action.newState };
        delete advanceToCreate.id;

        this.cashService.addAdvance(advanceToCreate.itemId, advanceToCreate).subscribe({
          next: (newId) => {
            action.targetId = newId;
            action.newState = { ...advanceToCreate, id: newId };
            this.syncAdvanceLocally(action.newState, true);
            this.saveHistory();
            this.showHistoryToast(direction, 'Alta de adelanto rehecha');
          }
        });
        break;
      }

      case 'ADVANCE_DELETE': {
        if (isUndo) {
          if (!action.oldState) return;
          const advanceToRestore = { ...action.oldState };
          delete advanceToRestore.id;

          this.cashService.addAdvance(advanceToRestore.itemId, advanceToRestore).subscribe({
            next: (newId) => {
              action.targetId = newId;
              action.oldState = { ...advanceToRestore, id: newId };
              this.syncAdvanceLocally(action.oldState, true);
              this.saveHistory();
              this.showHistoryToast(direction, 'Adelanto restaurado');
            }
          });
          break;
        }

        this.cashService.deleteAdvance(action.targetId!).subscribe({
          next: () => {
            this.syncAdvanceLocally(action.oldState, false);
            this.showHistoryToast(direction, 'Eliminación de adelanto rehecha');
          }
        });
        break;
      }

      case 'ITEM_DELETE': {
        if (isUndo) {
          const itemToRestore = { ...action.oldState, id: 0 };
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
              action.targetId = newId;
              action.oldState = { ...action.oldState, id: newId };
              this.saveHistory();

              if (isCurrentMonth) {
                this.items.push(action.oldState);
                this.items = [...this.items];
                this.sortItems();
                this.filterItems();
                this.calculateMonthlyTotals();
              }
              this.showHistoryToast(direction, `Gasto restaurado en ${this.getMonthNameByNum(m)}`);
            }
          });
          break;
        }

        this.cashService.deleteItem(action.targetId!).subscribe({
          next: () => {
            if (isCurrentMonth) {
              const itemIndex = this.items.findIndex(item => item.id === action.targetId);
              if (itemIndex !== -1) this.items.splice(itemIndex, 1);
              this.filterItems();
              this.calculateMonthlyTotals();
              this.calculateTableTotals();
            }
            this.showHistoryToast(direction, 'Eliminación de gasto rehecha');
          }
        });
        break;
      }
    }
  }

  private addIvaCompraLocally(ivaCompra: any): void {
    const id = ivaCompra?.id ?? ivaCompra?.Id;
    const existingIndex = this.ivaCompras.findIndex(currentIva =>
      Number(currentIva.id ?? currentIva.Id) === Number(id)
    );

    if (existingIndex === -1) {
      this.ivaCompras.unshift({ ...ivaCompra, id });
    } else {
      this.ivaCompras[existingIndex] = { ...ivaCompra, id };
    }

    this.calculateTotalIvaCompras();
  }

  private removeIvaCompraLocally(id: number): void {
    const existingIndex = this.ivaCompras.findIndex(currentIva =>
      Number(currentIva.id ?? currentIva.Id) === Number(id)
    );

    if (existingIndex !== -1) {
      this.ivaCompras.splice(existingIndex, 1);
      this.calculateTotalIvaCompras();
    }
  }

  private syncAdvanceLocally(advance: any, exists: boolean): void {
    if (!advance) return;

    const advanceId = Number(advance.id ?? advance.Id);
    const itemId = Number(advance.itemId ?? advance.ItemId);
    const amount = Number(advance.amount ?? advance.Amount) || 0;
    const isSelectedItem = Number(this.selectedItemForAdvances?.id) === itemId;

    if (isSelectedItem) {
      const existingIndex = this.advances.findIndex(currentAdvance =>
        Number(currentAdvance.id ?? currentAdvance.Id) === advanceId
      );

      if (exists) {
        const normalizedAdvance = { ...advance, id: advanceId, itemId };
        if (existingIndex === -1) {
          this.advances.unshift(normalizedAdvance);
        } else {
          this.advances[existingIndex] = normalizedAdvance;
        }
      } else if (existingIndex !== -1) {
        this.advances.splice(existingIndex, 1);
      }

      this.calculateAdvancesTotal();
    }

    const item = this.items.find(currentItem => Number(currentItem.id) === itemId);
    const total = isSelectedItem
      ? this.advancesTotalAmount
      : Math.max(0, (Number(item?.totalAdvances) || 0) + (exists ? amount : -amount));

    if (item) {
      item.totalAdvances = total;
      item.hasAdvances = total > 0;
      item.isPaid = this.isAdvancesComplete(item);
    }

    if (isSelectedItem && this.selectedItemForAdvances && this.selectedItemForAdvances !== item) {
      this.selectedItemForAdvances.totalAdvances = total;
      this.selectedItemForAdvances.hasAdvances = total > 0;
      this.selectedItemForAdvances.isPaid = this.isAdvancesComplete(this.selectedItemForAdvances);
    }

    this.calculateMonthlyTotals();
    this.calculateTableTotals();
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

  private showHistoryToast(direction: 'undo' | 'redo', msg: string): void {
    const title = direction === 'undo' ? 'Deshecho (Ctrl+Z)' : 'Rehecho (Ctrl+Y)';
    Swal.fire({ toast: true, position: 'bottom-end', icon: 'success', title, text: msg, showConfirmButton: false, timer: 3500 });
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
    if (this.signalRSubscription) {
      this.signalRSubscription.unsubscribe();
    }
    this.cashSignalrService.stopConnection();
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

    this.loadHistory();
    this.loadData();

    // Iniciar SignalR y escuchar eventos
    this.cashSignalrService.startConnection();
    this.signalRSubscription = this.cashSignalrService.onCashUpdated$.subscribe(() => {
      // Evitar interrumpir al usuario si está editando activamente
      const activeEl = document.activeElement as HTMLElement;
      if (activeEl && (activeEl.tagName === 'INPUT' || activeEl.tagName === 'TEXTAREA' || activeEl.isContentEditable)) {
        console.log('[Cash] Actualización recibida pero ignorada porque el usuario está editando.');
        return;
      }
      console.log('[Cash] Actualización recibida, recargando datos...');
      this.loadData();
    });
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

  async clearItemField(item: CashFlowItem, field: keyof CashFlowItem): Promise<void> {
    const fieldLabels: Partial<Record<keyof CashFlowItem, string>> = {
      depo: 'Depósito',
      casa: 'Casa',
      retiros: 'Retiros',
      iaia: 'Ingresos adicionales'
    };
    const confirmed = await this.deleteConfirmation.confirm({
      title: '¿Vaciar este monto?',
      message: 'Se eliminará el monto cargado en',
      highlightedText: fieldLabels[field] || 'esta columna',
      messageSuffix: '.'
    });
    if (!confirmed) return;

    this.captureItem(item);
    (item as any)[field] = null;
    this.checkItemChange(item);
    this.onItemChange(item);
  }

  async clearAccountBalance(account: FinancialAccount): Promise<void> {
    const confirmed = await this.deleteConfirmation.confirm({
      title: '¿Vaciar saldo?',
      message: 'Se eliminará el saldo actual de la cuenta',
      highlightedText: account.name,
      messageSuffix: '.'
    });
    if (!confirmed) return;

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
    
    // Si el usuario pone el color blanco, lo interpretamos como "sin color"
    if (item.color && item.color.toLowerCase() === '#ffffff') {
      item.color = null as any;
    }
    
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

  async deleteItem(item: any): Promise<void> {
    const confirmed = await this.deleteConfirmation.confirm({
      message: 'Esta acción eliminará el concepto contable.'
    });
    if (!confirmed) return;

    const oldState = JSON.parse(JSON.stringify(item));
    const realIndex = this.items.indexOf(item);

    if (!item.id) {
      if (realIndex !== -1) {
        this.recordHistoryAction({ type: 'ITEM_DELETE', oldState, anchorMonth: this.selectedMonth, anchorYear: this.selectedYear });
        this.items.splice(realIndex, 1);
        this.filterItems();
        this.calculateMonthlyTotals();
      }
      return;
    }

    this.cashService.deleteItem(item.id).subscribe(() => {
      if (realIndex !== -1) {
        this.recordHistoryAction({ type: 'ITEM_DELETE', targetId: item.id, oldState, anchorMonth: this.selectedMonth, anchorYear: this.selectedYear });
        this.items.splice(realIndex, 1);
        this.filterItems();
        this.calculateMonthlyTotals();
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
          // Buscador global (Busca por monto, nro de fila, concepto, etc. EXCLUYE NOTAS)
          const searchStr = `${item.rowNum} ${item.description || ''} ${item.depo || ''} ${item.casa || ''} ${item.retiros || ''} ${item.iaia || ''}`.toLowerCase();
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
      // Excluimos item.comment de la búsqueda
      const searchStr = `${item.rowNum} ${item.description || ''} ${item.depo || ''} ${item.casa || ''} ${item.retiros || ''} ${item.iaia || ''}`.toLowerCase();
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
          this.recordHistoryAction({
            type: 'ACCOUNT_CREATE',
            targetId: newAcc.id,
            newState: JSON.parse(JSON.stringify(newAcc)),
            anchorMonth: this.selectedMonth,
            anchorYear: this.selectedYear
          });
          this.accounts.push(newAcc);
          this.calculateAccountTotals(); 
          Swal.fire('Creada', 'La cuenta ha sido agregada.', 'success');
        });
      }
    });
  }

  async deleteAccount(account: FinancialAccount, index: number): Promise<void> {
    const confirmed = await this.deleteConfirmation.confirm({
      message: 'Se borrará la cuenta',
      highlightedText: account.name,
      messageSuffix: 'y su saldo actual.'
    });
    if (!confirmed) return;

    const oldState = JSON.parse(JSON.stringify(account));
    this.cashService.deleteAccount(account.id!).subscribe(() => {
      this.recordHistoryAction({ type: 'ACCOUNT_DELETE', targetId: account.id, oldState, anchorMonth: this.selectedMonth, anchorYear: this.selectedYear });
      this.accounts.splice(index, 1);
      this.calculateAccountTotals();
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

  onCommentMouseEnter(item: CashFlowItem): void {
    if (this.commentLeaveTimer) {
      clearTimeout(this.commentLeaveTimer);
      this.commentLeaveTimer = null;
    }

    if (this.activeCommentItem === item) return;

    if (this.commentHoverTimer) {
      clearTimeout(this.commentHoverTimer);
    }

    this.commentHoverTimer = setTimeout(() => {
      if (this.activeCommentItem && !this.isCommentPinned && this.activeCommentItem !== item) {
        this.closeComment(this.activeCommentItem);
      }
      this.activeCommentItem = item;
      this.isCommentPinned = false;
      setTimeout(() => {
        const activeTextarea = document.getElementById('excel-comment-' + item.id) as HTMLTextAreaElement;
        if (activeTextarea) {
          activeTextarea.style.height = 'auto';
          activeTextarea.style.height = activeTextarea.scrollHeight + 'px';
        }
      }, 0);
    }, 400);
  }

  onCommentMouseLeave(item: CashFlowItem): void {
    if (this.commentHoverTimer) {
      clearTimeout(this.commentHoverTimer);
      this.commentHoverTimer = null;
    }

    if (this.activeCommentItem === item && !this.isCommentPinned) {
      if (this.commentLeaveTimer) {
        clearTimeout(this.commentLeaveTimer);
      }
      this.commentLeaveTimer = setTimeout(() => {
        if (this.activeCommentItem === item && !this.isCommentPinned) {
          this.closeComment(item);
        }
      }, 100);
    }
  }

  pinComment(item: CashFlowItem): void {
    if (this.commentLeaveTimer) {
      clearTimeout(this.commentLeaveTimer);
      this.commentLeaveTimer = null;
    }
    if (this.activeCommentItem === item) {
      this.isCommentPinned = true;
      setTimeout(() => {
        const activeTextarea = document.getElementById('excel-comment-' + item.id) as HTMLTextAreaElement;
        if (activeTextarea && document.activeElement !== activeTextarea) {
          activeTextarea.focus();
        }
      }, 0);
    }
  }

  toggleComment(item: CashFlowItem): void {
    if (this.commentHoverTimer) {
      clearTimeout(this.commentHoverTimer);
      this.commentHoverTimer = null;
    }
    if (this.commentLeaveTimer) {
      clearTimeout(this.commentLeaveTimer);
      this.commentLeaveTimer = null;
    }

    if (this.activeCommentItem === item) {
      if (this.isCommentPinned) {
        this.closeComment(item);
      } else {
        this.isCommentPinned = true;
        setTimeout(() => {
          const activeTextarea = document.getElementById('excel-comment-' + item.id) as HTMLTextAreaElement;
          if (activeTextarea) {
            activeTextarea.focus();
          }
        }, 0);
      }
    } else {
      if (this.activeCommentItem) {
        this.closeComment(this.activeCommentItem);
      }
      this.activeCommentItem = item;
      this.isCommentPinned = true;
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
    if (this.commentHoverTimer) {
      clearTimeout(this.commentHoverTimer);
      this.commentHoverTimer = null;
    }
    if (this.commentLeaveTimer) {
      clearTimeout(this.commentLeaveTimer);
      this.commentLeaveTimer = null;
    }
    this.checkItemChange(item);
    this.activeCommentItem = null;
    this.isCommentPinned = false;
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

  async deleteComment(item: CashFlowItem): Promise<void> {
    const confirmed = await this.deleteConfirmation.confirm({
      title: '¿Borrar nota?',
      message: 'Se eliminará la nota del concepto',
      highlightedText: item.description || 'este concepto',
      messageSuffix: '.'
    });
    if (!confirmed) return;

    this.captureItem(item);
    item.comment = '';
    item.commentUpdatedAt = new Date();
    this.checkItemChange(item);
    this.closeComment(item); 
  }

  onAccountColorChange(account: FinancialAccount): void {
    if (!account.color || account.color.toLowerCase() === '#ffffff') {
      account.color = null as any; // Volver a la normalidad
    }

    this.cashService.updateAccountColor(account.id!, account.color).subscribe({
        error: () => Swal.fire('Error', 'No se pudo guardar el color de la cuenta', 'error')
    });
  }

  onItemCommentInput(item: CashFlowItem): void {
    item.commentUpdatedAt = new Date();
  }

  getFormattedUpdatedDate(date?: Date | string | null, fallbackItem?: any): string {
    const rawDate = date || (fallbackItem?.CommentUpdatedAt) || (fallbackItem?.comment_updated_at);
    if (!rawDate) return '';
    const d = new Date(rawDate);
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
        const createdIvaCompra = { ...payload, id: newId };
        this.ivaCompras.unshift(createdIvaCompra);
        this.recordHistoryAction({
          type: 'IVA_CREATE',
          targetId: newId,
          newState: JSON.parse(JSON.stringify(createdIvaCompra)),
          anchorMonth: this.selectedMonth,
          anchorYear: this.selectedYear
        });
        this.calculateTotalIvaCompras();
        this.newIvaCompra.amount = null as any;
        this.newIvaCompra.comment = '';
        Swal.fire({
          toast: true,
          position: 'bottom-end',
          icon: 'success',
          title: 'Factura agregada',
          text: 'La compra de IVA se registró correctamente.',
          showConfirmButton: false,
          timer: 2600,
          timerProgressBar: true
        });
      },
      error: (err) => {
        console.error('Error al agregar compra de IVA:', err);
        Swal.fire({
          title: 'No se pudo agregar la factura',
          text: err.error?.message || 'Revisá los datos e intentá nuevamente.',
          icon: 'error'
        });
      }
    });
  }

  async deleteIvaCompra(id: number, index: number): Promise<void> {
    const ivaCompra = this.ivaCompras[index];
    const amount = Number(ivaCompra?.amount) || 0;
    const amountLabel = amount.toLocaleString('es-AR', {
      style: 'currency',
      currency: 'ARS',
      maximumFractionDigits: 2
    });
    const detail = ivaCompra?.comment?.trim() || amountLabel;
    const oldState = ivaCompra ? JSON.parse(JSON.stringify(ivaCompra)) : null;
    const confirmed = await this.deleteConfirmation.confirm({
      title: '¿Eliminar factura de IVA?',
      message: 'Esta acción eliminará la compra de IVA',
      highlightedText: detail,
      messageSuffix: 'de forma permanente.'
    });

    if (!confirmed) return;

    this.cashService.deleteIvaCompra(id).subscribe({
      next: () => {
        this.ivaCompras.splice(index, 1);
        if (oldState) {
          this.recordHistoryAction({
            type: 'IVA_DELETE',
            targetId: id,
            oldState,
            anchorMonth: this.selectedMonth,
            anchorYear: this.selectedYear
          });
        }
        this.calculateTotalIvaCompras();
        Swal.fire({
          toast: true,
          position: 'bottom-end',
          icon: 'success',
          title: 'Factura eliminada',
          text: 'La compra de IVA se eliminó correctamente.',
          showConfirmButton: false,
          timer: 2600,
          timerProgressBar: true
        });
      },
      error: (err) => {
        console.error('Error al eliminar compra de IVA:', err);
        Swal.fire({
          title: 'No se pudo eliminar la factura',
          text: err.error?.message || 'Intentá nuevamente en unos instantes.',
          icon: 'error'
        });
      }
    });
  }

// --- GRAB MULTIPLE ITEMS
  getSelectedItemsList(): CashFlowItem[] {
    return this.filteredItems.filter(item => this.selectedItemIds.includes(item.id!));
  }

  isAllItemsSelected(): boolean {
    if (!this.filteredItems || this.filteredItems.length === 0) return false;
    const validItems = this.filteredItems.filter(item => item.id);
    if (validItems.length === 0) return false;
    return validItems.every(item => this.selectedItemIds.includes(item.id!));
  }

  toggleSelectAllItems(): void {
    if (this.selectedItemIds.length > 0) {
      this.selectedItemIds = [];
    } else {
      this.selectedItemIds = this.filteredItems
        .filter(item => item.id)
        .map(item => item.id!);
    }
  }

  isAllAccountsSelected(): boolean {
    if (!this.accounts || this.accounts.length === 0) return false;
    const validAccounts = this.accounts.filter(acc => acc.id);
    if (validAccounts.length === 0) return false;
    return validAccounts.every(acc => this.selectedAccountIds.includes(acc.id!));
  }

  toggleSelectAllAccounts(): void {
    if (this.selectedAccountIds.length > 0) {
      this.selectedAccountIds = [];
    } else {
      this.selectedAccountIds = this.accounts
        .filter(acc => acc.id)
        .map(acc => acc.id!);
    }
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
      amount: adv.amount || adv.Amount,
      comment: adv.comment || adv.Comment || ''
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
    this.newAdvance = { date: `${yyyy}-${mm}-${dd}`, amount: null as any, comment: '' };
  });
}

closeAdvancesModal(): void {
  this.showAdvancesModal = false;
  this.selectedItemForAdvances = null;
  this.advances = [];
}

  isAdvancesComplete(item: CashFlowItem): boolean {
    if (!item || !item.hasAdvances) return false;
    const itemTotal = (Number(item.depo) || 0) + (Number(item.casa) || 0) + (Number(item.retiros) || 0) + (Number(item.iaia) || 0);
    const totalAdvances = Number(item.totalAdvances) || 0;
    if (itemTotal <= 0 && totalAdvances > 0) return true;
    return totalAdvances >= itemTotal - 0.01 && itemTotal > 0;
  }

  calculateAdvancesTotal(): void {
    this.advancesTotalAmount = this.advances.reduce((sum, adv) => {
      return sum + (Number(adv.amount) || 0);
    }, 0);
  }

  getAdvancesItemTotal(): number {
    if (!this.selectedItemForAdvances) return 0;
    return (Number(this.selectedItemForAdvances.depo) || 0) + 
           (Number(this.selectedItemForAdvances.casa) || 0) + 
           (Number(this.selectedItemForAdvances.retiros) || 0) + 
           (Number(this.selectedItemForAdvances.iaia) || 0);
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
      amount: this.newAdvance.amount,
      comment: this.newAdvance.comment
    };

    this.cashService.addAdvance(this.selectedItemForAdvances!.id!, payload).subscribe({
      next: (newId) => {
        const advanceComment = this.newAdvance.comment?.trim();
        const createdAdvance = { ...payload, id: newId };
        this.advances.unshift(createdAdvance);
        this.recordHistoryAction({
          type: 'ADVANCE_CREATE',
          targetId: newId,
          newState: JSON.parse(JSON.stringify(createdAdvance)),
          anchorMonth: this.selectedMonth,
          anchorYear: this.selectedYear
        });
        this.calculateAdvancesTotal();

        // Actualizar el item en la tabla principal
        this.selectedItemForAdvances!.hasAdvances = true;
        this.selectedItemForAdvances!.totalAdvances = this.advancesTotalAmount;
        this.selectedItemForAdvances!.isPaid = this.isAdvancesComplete(this.selectedItemForAdvances!);
        this.calculateMonthlyTotals();
        this.calculateTableTotals();

        this.newAdvance.amount = null as any;
        this.newAdvance.comment = '';
        Swal.fire({
          toast: true,
          position: 'bottom-end',
          icon: 'success',
          title: 'Adelanto registrado',
          text: advanceComment ? `Concepto: ${advanceComment}` : 'Se guardó correctamente.',
          showConfirmButton: false,
          timer: 2600,
          timerProgressBar: true
        });
      },
      error: (err) => {
        console.error('Error al registrar adelanto:', err);
        Swal.fire({
          title: 'No se pudo registrar el adelanto',
          text: err.error?.message || 'Revisá los datos e intentá nuevamente.',
          icon: 'error'
        });
      }
    });
  }

  async deleteAdvance(id: number, index: number): Promise<void> {
    const advance = this.advances[index];
    const amount = Number(advance?.amount) || 0;
    const amountLabel = amount.toLocaleString('es-AR', {
      style: 'currency',
      currency: 'ARS',
      maximumFractionDigits: 2
    });
    const detail = advance?.comment?.trim() || amountLabel;
    const oldState = advance ? JSON.parse(JSON.stringify(advance)) : null;
    const confirmed = await this.deleteConfirmation.confirm({
      title: '¿Eliminar pago parcial?',
      message: 'Esta acción eliminará el adelanto',
      highlightedText: detail,
      messageSuffix: 'de forma permanente.'
    });

    if (!confirmed) return;

    this.cashService.deleteAdvance(id).subscribe({
      next: () => {
        this.advances.splice(index, 1);
        if (oldState) {
          this.recordHistoryAction({
            type: 'ADVANCE_DELETE',
            targetId: id,
            oldState,
            anchorMonth: this.selectedMonth,
            anchorYear: this.selectedYear
          });
        }
        this.calculateAdvancesTotal();

        // Actualizar el item en la tabla principal
        this.selectedItemForAdvances!.totalAdvances = this.advancesTotalAmount;
        this.selectedItemForAdvances!.hasAdvances = this.advances.length > 0;
        this.selectedItemForAdvances!.isPaid = this.isAdvancesComplete(this.selectedItemForAdvances!);
        this.calculateMonthlyTotals();
        this.calculateTableTotals();
        Swal.fire({
          toast: true,
          position: 'bottom-end',
          icon: 'success',
          title: 'Adelanto eliminado',
          text: 'El pago parcial se eliminó correctamente.',
          showConfirmButton: false,
          timer: 2600,
          timerProgressBar: true
        });
      },
      error: (err) => {
        console.error('Error al eliminar adelanto:', err);
        Swal.fire({
          title: 'No se pudo eliminar el adelanto',
          text: err.error?.message || 'Intentá nuevamente en unos instantes.',
          icon: 'error'
        });
      }
    });
  }
}
