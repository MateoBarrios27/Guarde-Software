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

  // 1. FORMATEO EN TIEMPO REAL (Mientras el usuario escribe)
  // 1. FORMATEO EN TIEMPO REAL (Con retención de cursor)
  @HostListener('input', ['$event'])
  onInput(event: any) {
    const input = event.target;
    let val = input.value;

    // A. Guardamos la posición original del cursor y la longitud del texto
    let cursorPosition = input.selectionStart;
    const originalLength = val.length;

    // B. Limpiamos y formateamos (misma lógica de antes)
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

    // C. Mostramos el número enmascarado en el input
    this.el.nativeElement.value = formattedStr;

    // D. RESTAURACIÓN DEL CURSOR
    // Calculamos si la longitud cambió (ej: si se agregó o se quitó un punto de miles)
    const newLength = formattedStr.length;
    cursorPosition = cursorPosition + (newLength - originalLength);

    // Evitamos posiciones negativas por seguridad
    if (cursorPosition < 0) cursorPosition = 0;

    // Usamos setTimeout para que el navegador aplique la posición DESPUÉS de renderizar el nuevo texto
    setTimeout(() => {
      input.setSelectionRange(cursorPosition, cursorPosition);
    }, 0);

    // E. Actualizamos el modelo numérico por debajo
    const cleanValue = formattedStr.replace(/\./g, '').replace(',', '.');
    const numberValue = parseFloat(cleanValue);

    if (!isNaN(numberValue)) {
      this.onChange(numberValue);
    } else {
      this.onChange(null);
    }
  }
  
  // 2. AL SALIR DEL INPUT (Rellena con ceros si es necesario, ej: 1.500,5 -> 1.500,50)
  @HostListener('blur')
  onBlur() {
    this.onTouched();
    const val = this.el.nativeElement.value;
    
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

  // Lógica de formato oficial
  private formatNumber(value: number): string {
    return new Intl.NumberFormat('es-AR', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    }).format(value);
  }
}