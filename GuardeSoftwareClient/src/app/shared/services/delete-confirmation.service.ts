import { Injectable, signal } from '@angular/core';

export interface DeleteConfirmationOptions {
  headerTitle?: string;
  title?: string;
  message: string;
  highlightedText?: string;
  messageSuffix?: string;
  confirmText?: string;
}

export interface ActiveDeleteConfirmation extends Required<Pick<DeleteConfirmationOptions, 'headerTitle' | 'title' | 'message' | 'confirmText'>> {
  highlightedText?: string;
  messageSuffix?: string;
}

@Injectable({ providedIn: 'root' })
export class DeleteConfirmationService {
  private readonly activeConfirmation = signal<ActiveDeleteConfirmation | null>(null);
  private pendingResolver: ((confirmed: boolean) => void) | null = null;

  readonly confirmation = this.activeConfirmation.asReadonly();

  confirm(options: DeleteConfirmationOptions): Promise<boolean> {
    this.resolve(false);

    this.activeConfirmation.set({
      headerTitle: options.headerTitle ?? 'Confirmar Eliminación',
      title: options.title ?? '¿Estás seguro?',
      message: options.message,
      highlightedText: options.highlightedText,
      messageSuffix: options.messageSuffix,
      confirmText: options.confirmText ?? 'Confirmar Eliminación',
    });

    return new Promise<boolean>((resolve) => {
      this.pendingResolver = resolve;
    });
  }

  accept(): void {
    this.resolve(true);
  }

  cancel(): void {
    this.resolve(false);
  }

  private resolve(confirmed: boolean): void {
    const resolver = this.pendingResolver;
    this.pendingResolver = null;
    this.activeConfirmation.set(null);
    resolver?.(confirmed);
  }
}

