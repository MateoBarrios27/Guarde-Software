import { ChangeDetectorRef, Component, DestroyRef, EventEmitter, HostListener, inject, Input, Output, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { A11yModule } from '@angular/cdk/a11y';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';
import { ClientDetailDTO } from '../../../core/dtos/client/ClientDetailDTO';
import { ClientReactivationContext, ClientService, ReactivationBalanceDecision } from '../../../core/services/client-service/client.service';
import { IconComponent } from '../icon/icon.component';

export interface ClientReactivationPrepared {
  clientDetail: ClientDetailDTO;
  decision: ReactivationBalanceDecision;
}

@Component({
  selector: 'app-client-reactivation-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, A11yModule, IconComponent],
  templateUrl: './client-reactivation-modal.component.html',
  styleUrl: './client-reactivation-modal.component.css'
})
export class ClientReactivationModalComponent {
  private readonly clients = inject(ClientService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly cdr = inject(ChangeDetectorRef);
  @Input({ required: true }) clientId!: number;
  @Input({ required: true }) clientName!: string;
  @Output() closed = new EventEmitter<void>();
  @Output() prepared = new EventEmitter<ClientReactivationPrepared>();
  step: 'start' | 'balance' = 'start';
  action: 'keep' | 'zero' = 'keep';
  loading = false;
  error = '';
  context: ClientReactivationContext | null = null;
  private clientDetail: ClientDetailDTO | null = null;

  @ViewChild('balanceInput') set balanceInput(input: ElementRef<HTMLInputElement> | undefined) {
    input?.nativeElement.focus();
  }

  get balanceAmount(): number { return Math.abs(this.context?.balance ?? 0); }

  @HostListener('document:keydown.escape', ['$event'])
  onEscape(event: KeyboardEvent): void {
    event.stopPropagation();
    this.closed.emit();
  }

  start(): void {
    if (this.loading) return;
    this.error = '';
    this.loading = true;
    forkJoin({
      clientDetail: this.clients.getClientDetailById(this.clientId),
      context: this.clients.getReactivationContext(this.clientId)
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: ({ clientDetail, context }) => {
        this.clientDetail = clientDetail;
        this.context = context;
        this.loading = false;
        if (context.balance === 0) this.continue();
        else this.step = 'balance';
        this.cdr.markForCheck();
      },
      error: err => {
        this.loading = false;
        this.error = err.error?.message || 'No se pudo consultar el saldo. Intentá nuevamente.';
        this.cdr.markForCheck();
      }
    });
  }

  continue(): void {
    if (!this.context || !this.clientDetail || this.loading) return;
    this.prepared.emit({ clientDetail: this.clientDetail, decision: { ...this.context, action: this.action } });
  }
}
