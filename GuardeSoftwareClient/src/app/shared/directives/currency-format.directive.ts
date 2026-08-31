import { Directive, ElementRef, HostListener, forwardRef } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

@Directive({
  selector: '[appCurrencyFormat]',
  standalone: true,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => CurrencyFormatDirective),
      multi: true
    }
  ]
})
export class CurrencyFormatDirective implements ControlValueAccessor {
  private onChange: (val: number | null) => void = () => {};
  private onTouched: () => void = () => {};

  constructor(private el: ElementRef) {}

  writeValue(value: any): void {
    if (value !== undefined && value !== null && value !== '') {
      const numberValue = typeof value === 'number'
        ? value
        : this.parseAndFormat(String(value)).numberValue;
      this.el.nativeElement.value = numberValue !== null && !isNaN(numberValue)
        ? this.formatNumber(numberValue)
        : '';
    } else {
      this.el.nativeElement.value = '';
    }
  }

  registerOnChange(fn: any): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: any): void {
    this.onTouched = fn;
  }

  @HostListener('input', ['$event'])
  onInput(event: any): void {
    const input = event.target as HTMLInputElement;
    const originalValue = input.value;
    const originalLength = originalValue.length;
    let cursorPosition = input.selectionStart ?? originalLength;
    const parsed = this.parseAndFormat(originalValue);
    const formattedStr = parsed.formatted;

    this.el.nativeElement.value = formattedStr;

    const newLength = formattedStr.length;
    cursorPosition = cursorPosition + (newLength - originalLength);

    if (cursorPosition < 0) cursorPosition = 0;

    setTimeout(() => {
      input.setSelectionRange(cursorPosition, cursorPosition);
    }, 0);

    this.onChange(parsed.numberValue);
  }
  
  @HostListener('blur')
  onBlur() {
    this.onTouched();
    const val = this.el.nativeElement.value;

    const parsed = this.parseAndFormat(val);
    if (parsed.numberValue !== null && !isNaN(parsed.numberValue)) {
      this.onChange(parsed.numberValue);
      this.el.nativeElement.value = this.formatNumber(parsed.numberValue);
    } else {
      this.onChange(null);
      this.el.nativeElement.value = '';
    }
  }

  private parseAndFormat(rawValue: string): { formatted: string; numberValue: number | null } {
    const isNegative = rawValue.trim().startsWith('-');
    const value = rawValue.replace(/[^0-9.,]/g, '');
    const decimalSeparator = this.detectDecimalSeparator(value);
    const hasDecimalSeparator = decimalSeparator !== null;

    let integerPart = value;
    let fractionPart = '';

    if (decimalSeparator) {
      const decimalIndex = value.lastIndexOf(decimalSeparator);
      integerPart = value.substring(0, decimalIndex);
      fractionPart = value.substring(decimalIndex + 1).replace(/[.,]/g, '').substring(0, 2);
    }

    const integerDigits = integerPart.replace(/[.,]/g, '');
    const hasDigits = /\d/.test(integerDigits + fractionPart);
    const displayInteger = integerDigits || (fractionPart ? '0' : '');
    const groupedInteger = displayInteger.replace(/\B(?=(\d{3})+(?!\d))/g, '.');

    let formatted = groupedInteger;
    if (hasDecimalSeparator) {
      formatted += ',' + fractionPart;
    }

    if (isNegative) {
      formatted = formatted ? '-' + formatted : '-';
    }

    if (!hasDigits) {
      return { formatted, numberValue: null };
    }

    const numberValue = Number(`${integerDigits || '0'}${fractionPart ? `.${fractionPart}` : ''}`);
    return {
      formatted,
      numberValue: isNegative ? -numberValue : numberValue
    };
  }

  private detectDecimalSeparator(value: string): ',' | '.' | null {
    const commaIndex = value.lastIndexOf(',');
    const dotIndex = value.lastIndexOf('.');

    if (commaIndex === -1 && dotIndex === -1) return null;
    if (commaIndex !== -1 && dotIndex !== -1) {
      return commaIndex > dotIndex ? ',' : '.';
    }
    if (commaIndex !== -1) return ',';

    const dotParts = value.split('.');
    if (dotParts.length > 2) {
      const isThousandsGrouping = dotParts.slice(1).every(part => part.length === 3);
      return isThousandsGrouping ? null : '.';
    }

    const digitsAfter = value.length - dotIndex - 1;
    if (digitsAfter === 3) {
      const digitsBefore = value.substring(0, dotIndex).replace(/\D/g, '').length;
      return digitsBefore > 3 ? '.' : null;
    }

    // Más de tres dígitos después de un punto normalmente significa que el
    // usuario siguió escribiendo sobre el separador de miles ya formateado
    // (por ejemplo, 1.2345). Se conserva como agrupador para no convertir
    // accidentalmente 12.345 en 12,34 al continuar tipeando.
    return digitsAfter <= 2 ? '.' : null;
  }

  private formatNumber(value: number): string {
    return new Intl.NumberFormat('es-AR', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    }).format(value);
  }
}
