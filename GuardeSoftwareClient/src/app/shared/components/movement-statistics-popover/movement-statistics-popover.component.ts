import { Component, Input, OnChanges, OnDestroy, ViewChild, ElementRef, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { A11yModule } from '@angular/cdk/a11y';
import { Overlay, OverlayModule, ConnectedPosition, CdkConnectedOverlay } from '@angular/cdk/overlay';
import { Subscription } from 'rxjs';
import { IconComponent } from '../icon/icon.component';
import { AccountMovementDTO, MovementStatisticsDTO } from '../../../core/dtos/accountMovement/account-movement.dto';
import { AccountMovementService } from '../../../core/services/accountMovement-service/account-movement.service';
import { DataRefreshService } from '../../../core/services/data-refresh-service/data-refresh.service';

@Component({
  selector: 'app-movement-statistics-popover', standalone: true,
  imports: [CommonModule, OverlayModule, A11yModule, IconComponent],
  templateUrl: './movement-statistics-popover.component.html',
  styleUrl: './movement-statistics-popover.component.css'
})
export class MovementStatisticsPopoverComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) clientId!: number;
  @Input({ required: true }) movement!: AccountMovementDTO;
  @ViewChild('trigger') trigger?: ElementRef<HTMLButtonElement>;
  @ViewChild(CdkConnectedOverlay) connectedOverlay?: CdkConnectedOverlay;
  open = false;
  loading = false;
  error = false;
  stats: MovementStatisticsDTO | null = null;
  readonly scrollStrategy;
  readonly positions: ConnectedPosition[] = [
    { originX: 'end', originY: 'bottom', overlayX: 'end', overlayY: 'top', offsetY: 8 },
    { originX: 'end', originY: 'top', overlayX: 'end', overlayY: 'bottom', offsetY: -8 }
  ];
  private request?: Subscription;
  private readonly refresh: Subscription;
  constructor(private service: AccountMovementService, overlay: Overlay,
    private cdr: ChangeDetectorRef, dataRefresh: DataRefreshService) {
    this.scrollStrategy = overlay.scrollStrategies.close();
    this.refresh = dataRefresh.watch(['clients', 'finances']).subscribe(() => {
      this.close(); this.cdr.markForCheck();
    });
  }
  ngOnChanges(): void { this.close(); }
  ngOnDestroy(): void { this.request?.unsubscribe(); this.refresh.unsubscribe(); }
  toggle(): void {
    if (this.open) { this.close(); return; }
    this.open = true;
    this.load();
  }
  load(): void {
    this.request?.unsubscribe(); this.stats = null; this.error = false; this.loading = true;
    this.request = this.service.getMovementStatistics(this.clientId, this.movement.id).subscribe({
      next: stats => { this.stats = stats; this.loading = false; this.updateCardPosition(); },
      error: () => { this.error = true; this.loading = false; this.updateCardPosition(); }
    });
  }
  private updateCardPosition(): void {
    // The loading surface is shorter: position again after rendering real values/errors.
    this.cdr.detectChanges();
    this.connectedOverlay?.overlayRef.updatePosition();
  }
  close(restoreFocus = false): void {
    this.request?.unsubscribe(); this.open = false; this.loading = false; this.stats = null; this.error = false;
    if (restoreFocus) this.trigger?.nativeElement.focus();
  }
  onKeydown(event: Event): void {
    if ((event as KeyboardEvent).key !== 'Escape') return;
    event.preventDefault(); event.stopImmediatePropagation(); this.close(true);
  }
}
