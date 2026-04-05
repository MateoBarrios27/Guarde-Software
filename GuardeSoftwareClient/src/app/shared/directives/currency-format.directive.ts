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
      this.el.nativeElement.value = this.formatNumber(Number(value));
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
  onInput(event: any) {
    const input = event.target;
    let val = input.value;
    let cursorPosition = input.selectionStart;
    const originalLength = val.length;

    const isNegative = val.startsWith('-');

    val = val.replace(/[^0-9,]/g, '');
    let parts = val.split(',');
    if (parts.length > 2) {
      parts.pop(); 
      val = parts.join(',');
    }

    if (parts.length === 2 && parts[1].length > 2) {
      parts[1] = parts[1].substring(0, 2);
    }

    let integerPart = parts[0];
    if (integerPart) {
      integerPart = integerPart.replace(/\B(?=(\d{3})+(?!\d))/g, ".");
    }

    let formattedStr = integerPart;
    if (parts.length > 1) {
      formattedStr += ',' + parts[1];
    } else if (val.endsWith(',')) {
      formattedStr += ',';
    }

    if (isNegative) {
      if (formattedStr !== '') {
        formattedStr = '-' + formattedStr;
      } else {
        formattedStr = '-'; 
      }
    }

    this.el.nativeElement.value = formattedStr;

    const newLength = formattedStr.length;
    cursorPosition = cursorPosition + (newLength - originalLength);

    if (cursorPosition < 0) cursorPosition = 0;

    setTimeout(() => {
      input.setSelectionRange(cursorPosition, cursorPosition);
    }, 0);

    const cleanValue = formattedStr.replace(/\./g, '').replace(',', '.');
    const numberValue = parseFloat(cleanValue);

    if (!isNaN(numberValue)) {
      this.onChange(numberValue);
    } else {
      this.onChange(null);
    }
  }
  
  @HostListener('blur')
  onBlur() {
    this.onTouched();
    const val = this.el.nativeElement.value;
    
    if (val === '-') {
      this.onChange(null);
      this.el.nativeElement.value = '';
      return;
    }

    const cleanValue = val.replace(/\./g, '').replace(',', '.');
    const numberValue = parseFloat(cleanValue);

    if (!isNaN(numberValue)) {
      this.onChange(numberValue);
      this.el.nativeElement.value = this.formatNumber(numberValue);
    } else {
      this.onChange(null);
      this.el.nativeElement.value = '';
    }
  }

  private formatNumber(value: number): string {
    return new Intl.NumberFormat('es-AR', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    }).format(value);
  }
}