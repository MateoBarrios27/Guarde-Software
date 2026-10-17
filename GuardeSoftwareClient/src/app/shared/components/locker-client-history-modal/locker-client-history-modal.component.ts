import { CommonModule } from '@angular/common';
import { A11yModule } from '@angular/cdk/a11y';
import { Overlay } from '@angular/cdk/overlay';
import { Component, EventEmitter, Input, OnChanges, OnDestroy, OnInit, Output, SimpleChanges, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { Locker } from '../../../core/models/locker';
import { LockerClientHistory } from '../../../core/models/locker-client-history';
import { LockerService } from '../../../core/services/locker-service/locker.service';
import { DataRefreshService } from '../../../core/services/data-refresh-service/data-refresh.service';
import { IconComponent } from '../icon/icon.component';

@Component({
  selector: 'app-locker-client-history-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, A11yModule, IconComponent],
  templateUrl: './locker-client-history-modal.component.html',
  styleUrl: './locker-client-history-modal.component.css'
})
export class LockerClientHistoryModalComponent implements OnInit, OnChanges, OnDestroy {
  private readonly lockerService = inject(LockerService);
  private readonly dataRefresh = inject(DataRefreshService);
  private readonly scrollStrategy = inject(Overlay).scrollStrategies.block();
  private request?: Subscription;
  private refresh?: Subscription;

  @Input({ required: true }) locker!: Locker;
  @Input() warehouseName = '';
  @Input() lockerTypeName = '';
  @Output() close = new EventEmitter<void>();

  history: LockerClientHistory[] = [];
  loading = false;
  error = '';
  search = '';

  ngOnInit(): void {
    this.scrollStrategy.enable();
    this.refresh = this.dataRefresh.watch(['lockers', 'clients'], 'locker-client-history')
      .subscribe(() => this.loadHistory());
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['locker']) {
      this.search = '';
      this.loadHistory();
    }
  }

  ngOnDestroy(): void {
    this.request?.unsubscribe();
    this.refresh?.unsubscribe();
    this.scrollStrategy.disable();
  }

  loadHistory(): void {
    this.request?.unsubscribe();
    this.history = [];
    this.error = '';
    this.loading = true;
    this.request = this.lockerService.getClientHistory(this.locker.id).subscribe({
      next: history => {
        this.history = history;
        this.loading = false;
      },
      error: response => {
        this.error = response.status === 404
          ? 'Esta baulera ya no está disponible. Cerrá el historial y actualizá la lista.'
          : 'No se pudo cargar el historial. Revisá la conexión y volvé a intentarlo.';
        this.loading = false;
      }
    });
  }

  get clientCount(): number {
    return new Set(this.history.map(item => item.clientId)).size;
  }

  get activeCount(): number {
    return this.history.filter(item => item.endDate === null).length;
  }

  get filteredHistory(): LockerClientHistory[] {
    const term = this.search.trim().toLocaleLowerCase('es-AR');
    if (!term) return this.history;
    return this.history.filter(item => item.clientName.toLocaleLowerCase('es-AR').includes(term)
      || (item.paymentIdentifier !== null && String(item.paymentIdentifier).includes(term)));
  }

  trackHistory(_index: number, item: LockerClientHistory): number {
    return item.id;
  }

  requestClose(event?: Event): void {
    event?.preventDefault();
    event?.stopPropagation();
    this.request?.unsubscribe();
    this.close.emit();
  }
}
