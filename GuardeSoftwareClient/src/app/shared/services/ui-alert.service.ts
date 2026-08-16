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
    const defaultCustomClass = UiAlertService.getCustomClass(isToast, options.confirmButtonColor);

    const themedIconHtml = options.iconHtml ?? UiAlertService.getStatusIconHtml(options.icon);

    return {
      ...options,
      iconHtml: themedIconHtml,
      confirmButtonText: options.confirmButtonText ?? 'Aceptar',
      cancelButtonText: options.cancelButtonText ?? 'Cancelar',
      buttonsStyling: options.buttonsStyling ?? false,
      reverseButtons: options.reverseButtons ?? true,
      showClass: options.showClass ?? { popup: 'guarde-alert-pop-in' },
      hideClass: options.hideClass ?? { popup: 'guarde-alert-pop-out' },
      customClass: UiAlertService.mergeCustomClass(defaultCustomClass, options.customClass),
    };
  }

  private static mergeCustomClass(
    base: SweetAlertCustomClass,
    custom?: SweetAlertCustomClass,
  ): SweetAlertCustomClass {
    const keys: Array<keyof SweetAlertCustomClass> = [
      'container',
      'popup',
      'title',
      'closeButton',
      'icon',
      'image',
      'htmlContainer',
      'input',
      'inputLabel',
      'validationMessage',
      'actions',
      'confirmButton',
      'denyButton',
      'cancelButton',
      'loader',
      'footer',
      'timerProgressBar',
    ];

    const classList = (value?: string | readonly string[]): string[] => {
      if (!value) return [];
      return Array.isArray(value) ? [...value] : [value];
    };

    const merged: SweetAlertCustomClass = {};
    for (const key of keys) {
      const value = [...classList(base[key]), ...classList(custom?.[key])].join(' ');
      if (value) merged[key] = value;
    }

    return merged;
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

  private static getConfirmButtonVariant(color?: string): 'primary' | 'danger' {
    const normalizedColor = color?.toLowerCase() ?? '';

    if (
      normalizedColor.includes('d33') ||
      normalizedColor.includes('red') ||
      normalizedColor.includes('dc2626') ||
      normalizedColor.includes('b91c1c')
    ) {
      return 'danger';
    }

    // Los estados exitosos se expresan en el ícono. La acción de confirmar
    // mantiene el azul primario, salvo que sea una operación destructiva.
    return 'primary';
  }

  private static getStatusIconHtml(icon?: SweetAlertIcon): string | undefined {
    const svgOpen = (variant: string) =>
      `<svg class="guarde-alert-status-icon guarde-alert-status-icon--${variant}" viewBox="0 0 64 64" aria-hidden="true" focusable="false">`;
    const svgClose = '</svg>';

    switch (icon) {
      case 'success':
        return `${svgOpen('success')}
          <circle class="guarde-alert-status-ring" cx="32" cy="32" r="27"></circle>
          <polyline class="guarde-alert-status-mark" points="16,33 27.5,44 48,21"></polyline>
        ${svgClose}`;
      case 'error':
        return `${svgOpen('error')}
          <path class="guarde-alert-status-mark" d="M14 14 50 50M50 14 14 50"></path>
        ${svgClose}`;
      case 'warning':
        return `${svgOpen('warning')}
          <path class="guarde-alert-status-mark" d="M32 14.5V38"></path>
          <circle class="guarde-alert-status-dot" cx="32" cy="49" r="3.5"></circle>
        ${svgClose}`;
      case 'info':
        return `${svgOpen('info')}
          <circle class="guarde-alert-status-ring" cx="32" cy="32" r="27"></circle>
          <path class="guarde-alert-status-mark" d="M32 28V46"></path>
          <circle class="guarde-alert-status-dot" cx="32" cy="19" r="3"></circle>
        ${svgClose}`;
      case 'question':
        return `${svgOpen('question')}
          <circle class="guarde-alert-status-ring" cx="32" cy="32" r="27"></circle>
          <path class="guarde-alert-status-mark" d="M23 25.5a9 9 0 1 1 14.6 7c-3.8 2.8-5.6 4.7-5.6 8"></path>
          <circle class="guarde-alert-status-dot" cx="32" cy="48.5" r="3"></circle>
        ${svgClose}`;
      default:
        return undefined;
    }
  }
}

export default UiAlertService;
