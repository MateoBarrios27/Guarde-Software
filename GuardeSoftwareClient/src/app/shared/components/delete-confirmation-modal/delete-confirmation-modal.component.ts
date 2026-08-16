import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { IconComponent } from '../icon/icon.component';
import { DeleteConfirmationService } from '../../services/delete-confirmation.service';

@Component({
  selector: 'app-delete-confirmation-modal',
  standalone: true,
  imports: [CommonModule, IconComponent],
  templateUrl: './delete-confirmation-modal.component.html',
  styleUrl: './delete-confirmation-modal.component.css',
})
export class DeleteConfirmationModalComponent {
  readonly deleteConfirmation = inject(DeleteConfirmationService);
  readonly confirmation = this.deleteConfirmation.confirmation;

  accept(): void {
    this.deleteConfirmation.accept();
  }

  cancel(): void {
    this.deleteConfirmation.cancel();
  }
}

