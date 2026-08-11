import { Injectable } from '@angular/core';
import SweetAlert, {
  SweetAlertCustomClass,
  SweetAlertIcon,
  SweetAlertOptions,
  SweetAlertResult,
} from 'sweetalert2';

/**
 * Punto único de entrada para los avisos de la aplicación.
 *
 * Se mantiene una API compatible con SweetAlert2 para que los casos complejos
 * (formularios, contenido HTML y confirmaciones encadenadas) no pierdan
 * capacidades, mientras que la apariencia y los valores por defecto viven en
 * este único lugar.
 */
@Injectable({ providedIn: 'root' })
export class UiAlertService {
  private static readonly themedAlert = SweetAlert.mixin({
    buttonsStyling: false,
    background: '#ffffff',
    backdrop: 'rgba(15, 23, 42, 0.46)',
    focusConfirm: false,
    reverseButtons: true,
    confirmButtonText: 'Aceptar',
    cancelButtonText: 'Cancelar',
    showClass: {
      popup: 'guarde-alert-pop-in',
    },
    hideClass: {
      popup: 'guarde-alert-pop-out',
    },
  });

  /** Compatible con el uso existente: Swal.DismissReason.cancel. */
  static readonly DismissReason = SweetAlert.DismissReason;

  static fire(options: SweetAlertOptions): Promise<SweetAlertResult>;
  static fire(
    title?: string,
    html?: string,
    icon?: SweetAlertIcon,
  ): Promise<SweetAlertResult>;
  static fire(
    optionsOrTitle?: SweetAlertOptions | string,
    html?: string,
    icon?: SweetAlertIcon,
  ): Promise<SweetAlertResult> {
    const options: SweetAlertOptions =
      typeof optionsOrTitle === 'string' || optionsOrTitle === undefined
        ? { title: optionsOrTitle, html, icon }
        : optionsOrTitle;

    return UiAlertService.themedAlert.fire(UiAlertService.applyTheme(options));
  }

  static close(result?: Partial<SweetAlertResult>): void {
    SweetAlert.close(result);
  }

  static getInput(): HTMLInputElement | null {
    return SweetAlert.getInput();
  }

  static showValidationMessage(message: string): void {
    SweetAlert.showValidationMessage(message);
  }

  static isVisible(): boolean {
    return SweetAlert.isVisible();
  }

  static success(title: string, text?: string): Promise<SweetAlertResult> {
    return UiAlertService.fire({ title, text, icon: 'success' });
  }

  static error(title: string, text?: string): Promise<SweetAlertResult> {
    return UiAlertService.fire({ title, text, icon: 'error' });
  }

  static warning(title: string, text?: string): Promise<SweetAlertResult> {
    return UiAlertService.fire({ title, text, icon: 'warning' });
  }

  static info(title: string, text?: string): Promise<SweetAlertResult> {
    return UiAlertService.fire({ title, text, icon: 'info' });
  }

  private static applyTheme(options: SweetAlertOptions): SweetAlertOptions {
    const isToast = Boolean(options.toast);
    const hasCustomClass = Boolean(options.customClass);

    return {
      ...options,
      confirmButtonText: options.confirmButtonText ?? 'Aceptar',
      cancelButtonText: options.cancelButtonText ?? 'Cancelar',
      buttonsStyling: options.buttonsStyling ?? false,
      reverseButtons: options.reverseButtons ?? true,
      showClass: options.showClass ?? { popup: 'guarde-alert-pop-in' },
      hideClass: options.hideClass ?? { popup: 'guarde-alert-pop-out' },
      customClass: hasCustomClass
        ? options.customClass
        : UiAlertService.getCustomClass(isToast, options.confirmButtonColor),
    };
  }

  private static getCustomClass(
    isToast: boolean,
    confirmButtonColor?: string,
  ): SweetAlertCustomClass {
    const confirmButtonVariant = UiAlertService.getConfirmButtonVariant(confirmButtonColor);

    return {
      container: 'guarde-alert-container',
      popup: isToast ? 'guarde-alert-toast' : 'guarde-alert-popup',
      title: 'guarde-alert-title',
      htmlContainer: 'guarde-alert-html',
      icon: 'guarde-alert-icon',
      actions: 'guarde-alert-actions',
      confirmButton: `guarde-alert-confirm guarde-alert-confirm--${confirmButtonVariant}`,
      cancelButton: 'guarde-alert-cancel',
      denyButton: 'guarde-alert-deny',
      input: 'guarde-alert-input',
      validationMessage: 'guarde-alert-validation',
      timerProgressBar: 'guarde-alert-progress',
    };
  }

  private static getConfirmButtonVariant(color?: string): 'primary' | 'danger' | 'success' {
    const normalizedColor = color?.toLowerCase() ?? '';

    if (
      normalizedColor.includes('d33') ||
      normalizedColor.includes('red') ||
      normalizedColor.includes('dc2626') ||
      normalizedColor.includes('b91c1c')
    ) {
      return 'danger';
    }

    if (
      normalizedColor.includes('10b981') ||
      normalizedColor.includes('green') ||
      normalizedColor.includes('emerald')
    ) {
      return 'success';
    }

    return 'primary';
  }
}

export default UiAlertService;
