import { CurrencyFormatDirective } from './currency-format.directive';
import { ElementRef } from '@angular/core';

describe('CurrencyFormatDirective', () => {
  let input: HTMLInputElement;
  let directive: CurrencyFormatDirective;
  let modelValue: number | null;

  beforeEach(() => {
    input = document.createElement('input');
    directive = new CurrencyFormatDirective(new ElementRef(input));
    modelValue = null;
    directive.registerOnChange((value: number | null) => modelValue = value);
  });

  it('should create an instance', () => {
    expect(directive).toBeTruthy();
  });

  it('formats a large amount without moving the cursor away from the end', () => {
    for (const digit of '123456789') {
      const cursor = input.selectionStart ?? input.value.length;
      input.value = input.value.slice(0, cursor) + digit + input.value.slice(cursor);
      input.setSelectionRange(cursor + 1, cursor + 1);

      directive.onInput({ target: input });

      expect(input.selectionStart).toBe(input.value.length);
    }

    expect(input.value).toBe('123.456.789');
    expect(modelValue).toBe(123456789);
  });

  it('accepts an explicit comma decimal separator', () => {
    input.value = '1234567,89';
    input.setSelectionRange(input.value.length, input.value.length);

    directive.onInput({ target: input });

    expect(input.value).toBe('1.234.567,89');
    expect(modelValue).toBe(1234567.89);
  });

  it('keeps the logical cursor position when editing in the middle', () => {
    input.value = '12.934.567';
    input.setSelectionRange(4, 4);

    directive.onInput({ target: input });

    expect(input.value).toBe('12.934.567');
    expect(input.selectionStart).toBe(4);
    expect(modelValue).toBe(12934567);
  });
});
