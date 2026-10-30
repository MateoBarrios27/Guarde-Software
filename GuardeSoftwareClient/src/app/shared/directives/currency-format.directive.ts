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

  // Último texto que la directiva dejó en el campo. Todo '.' que contiene es un
  // separador de miles escrito por ella, así que sólo un '.' nuevo respecto de este
  // texto puede ser un decimal tipeado. Sin esta referencia, un dígito más
  // (1.234.567 -> 1.234.5678) o un Backspace (1.234.567 -> 1.234.56) hacían que el
  // último punto pasara por decimal y el número se truncaba o cambiaba de valor.
  private lastText = '';

  constructor(private el: ElementRef) {}

  writeValue(value: any): void {
    if (value !== undefined && value !== null && value !== '') {
      const numberValue = typeof value === 'number'
        ? value
        : this.parseAndFormat(String(value)).numberValue;
      this.setText(numberValue !== null && !isNaN(numberValue)
        ? this.formatNumber(numberValue)
        : '');
    } else {
      this.setText('');
    }
  }

  registerOnChange(fn: any): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: any): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.el.nativeElement.disabled = isDisabled;
  }

  @HostListener('input', ['$event'])
  onInput(event: any): void {
    const input = event.target as HTMLInputElement;
    let originalValue = input.value;
    let originalCursorPosition = input.selectionStart ?? originalValue.length;

    // Si sólo se borró un separador de miles, el formato lo repondría y el cursor
    // quedaría atrapado: Backspace no avanzaría nunca. Se borra también el dígito
    // contiguo, que es lo que el usuario espera de la tecla.
    const edit = this.diffFromPrevious(originalValue, this.lastText);
    if (edit.start === edit.end && /^\.+$/.test(edit.removed)) {
      const digitIndex = event.inputType === 'deleteContentForward' ? edit.start : edit.start - 1;
      if (/\d/.test(originalValue[digitIndex] ?? '')) {
        originalValue = originalValue.substring(0, digitIndex) + originalValue.substring(digitIndex + 1);
        originalCursorPosition = digitIndex;
      }
    }

    const digitsToTheRight = (originalValue.substring(originalCursorPosition).match(/\d/g) ?? []).length;
    const parsed = this.parseAndFormat(originalValue, this.lastText);
    const formattedStr = parsed.formatted;

    this.setText(formattedStr);

    // Reubicar el cursor de forma sincrónica evita que queden callbacks viejos
    // capaces de moverlo mientras el usuario continúa escribiendo. Contar los
    // dígitos a la derecha también conserva la posición lógica cuando aparecen
    // o desaparecen separadores de miles.
    const cursorPosition = this.cursorPositionFromRight(formattedStr, digitsToTheRight);
    input.setSelectionRange(cursorPosition, cursorPosition);

    this.onChange(parsed.numberValue);
  }

  private cursorPositionFromRight(value: string, digitsToTheRight: number): number {
    if (digitsToTheRight === 0) return value.length;

    let remainingDigits = digitsToTheRight;
    for (let index = value.length - 1; index >= 0; index--) {
      if (/\d/.test(value[index])) {
        remainingDigits--;
        if (remainingDigits === 0) return index;
      }
    }

    return 0;
  }
  
  @HostListener('blur')
  onBlur() {
    this.onTouched();
    const val = this.el.nativeElement.value;

    // El texto del campo es el que escribió la directiva: sus puntos son de miles.
    const parsed = this.parseAndFormat(val, val);
    if (parsed.numberValue !== null && !isNaN(parsed.numberValue)) {
      this.onChange(parsed.numberValue);
      this.setText(this.formatNumber(parsed.numberValue));
    } else {
      this.onChange(null);
      this.setText('');
    }
  }

  private setText(text: string): void {
    this.el.nativeElement.value = text;
    this.lastText = text;
  }

  // Compara el texto actual con el anterior y devuelve el tramo del actual que
  // cambió ([start, end)) y el texto que había en ese lugar antes de la edición.
  private diffFromPrevious(value: string, previous: string): { start: number; end: number; removed: string } {
    const max = Math.min(value.length, previous.length);
    let start = 0;
    while (start < max && value[start] === previous[start]) start++;

    let tail = 0;
    while (tail < max - start && value[value.length - 1 - tail] === previous[previous.length - 1 - tail]) tail++;

    return { start, end: value.length - tail, removed: previous.substring(start, previous.length - tail) };
  }

  private parseAndFormat(rawValue: string, previousText = ''): { formatted: string; numberValue: number | null } {
    const isNegative = rawValue.trim().startsWith('-');
    const value = this.canonicalize(rawValue, previousText);
    const decimalIndex = value.lastIndexOf(',');
    const hasDecimalSeparator = decimalIndex !== -1;

    // Sin ceros a la izquierda: 0001 se muestra y se guarda como 1, no como 0.001.
    const integerDigits = (hasDecimalSeparator ? value.substring(0, decimalIndex) : value)
      .replace(/,/g, '')
      .replace(/^0+(?=\d)/, '');
    const fractionPart = hasDecimalSeparator
      ? value.substring(decimalIndex + 1).replace(/,/g, '').substring(0, 2)
      : '';
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

  // Deja sólo dígitos y, como mucho, la coma decimal. Lo que ya estaba en el campo
  // conserva su significado (el '.' es de miles, la ',' es decimal); sólo se
  // interpreta lo que el usuario acaba de insertar.
  private canonicalize(rawValue: string, previousText: string): string {
    const { start, end } = this.diffFromPrevious(rawValue, previousText);
    const unchanged = (text: string) => text.replace(/[^0-9,]/g, '');
    const before = unchanged(rawValue.substring(0, start));
    const after = unchanged(rawValue.substring(end));
    return before + this.canonicalizeInserted(rawValue.substring(start, end), before + after) + after;
  }

  private canonicalizeInserted(inserted: string, unchanged: string): string {
    const text = inserted.replace(/[^0-9.,]/g, '');

    // Pegado, autocompletado o carga del modelo: no hay referencia previa, así que
    // el formato se deduce del propio texto.
    if (text.length > 1) {
      const separator = this.detectDecimalSeparator(text);
      if (!separator) return text.replace(/[.,]/g, '');
      const decimalIndex = text.lastIndexOf(separator);
      return text.substring(0, decimalIndex).replace(/[.,]/g, '') + ',' + text.substring(decimalIndex + 1).replace(/[.,]/g, '');
    }

    // Un solo carácter tipeado: '.' y ',' valen como separador decimal, pero sólo
    // si el campo todavía no tiene uno.
    if (text === '.' || text === ',') return unchanged.includes(',') ? '' : ',';
    return text;
  }

  // Sólo se usa para texto pegado o cargado desde el modelo, donde no se sabe qué
  // puntos puso la directiva. Al tipear no interviene: ahí cada '.' ya presente
  // es un separador de miles.
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

    return digitsAfter <= 2 ? '.' : null;
  }

  private formatNumber(value: number): string {
    return new Intl.NumberFormat('es-AR', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    }).format(value);
  }
}
