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

  // Reproducen lo que hace el navegador al editar: cambian el texto y el cursor
  // y recién entonces disparan el evento input.
  const replace = (start: number, end: number, text: string, inputType: string) => {
    input.value = input.value.slice(0, start) + text + input.value.slice(end);
    input.setSelectionRange(start + text.length, start + text.length);
    directive.onInput({ target: input, inputType });
  };
  const type = (text: string) => {
    for (const char of text) {
      replace(input.selectionStart ?? input.value.length, input.selectionEnd ?? input.value.length, char, 'insertText');
    }
  };
  const backspace = () => {
    const start = input.selectionStart ?? input.value.length;
    const end = input.selectionEnd ?? start;
    if (start !== end) return replace(start, end, '', 'deleteContentBackward');
    if (start > 0) replace(start - 1, start, '', 'deleteContentBackward');
  };
  const del = () => {
    const start = input.selectionStart ?? input.value.length;
    const end = input.selectionEnd ?? start;
    if (start !== end) return replace(start, end, '', 'deleteContentForward');
    if (start < input.value.length) replace(start, start + 1, '', 'deleteContentForward');
  };
  const paste = (text: string) => {
    input.value = '';
    input.setSelectionRange(0, 0);
    replace(0, 0, text, 'insertFromPaste');
  };
  const moveCursorTo = (position: number) => input.setSelectionRange(position, position);

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

  describe('typing', () => {
    it('keeps every digit of amounts of ten million or more', () => {
      type('12345678');
      expect(input.value).toBe('12.345.678');
      expect(modelValue).toBe(12345678);

      type('9012');
      expect(input.value).toBe('123.456.789.012');
      expect(modelValue).toBe(123456789012);
    });

    it('does not read the last thousands dot as a decimal point', () => {
      type('1234567');
      type('8');

      expect(input.value).toBe('12.345.678');
      expect(input.value).not.toContain(',');
    });

    it('inserts a digit in the middle of a long amount', () => {
      type('1234567');
      moveCursorTo(4);

      type('9');

      expect(input.value).toBe('12.394.567');
      expect(modelValue).toBe(12394567);
      expect(input.selectionStart).toBe(5);
    });

    it('drops leading zeros', () => {
      type('0001');
      expect(input.value).toBe('1');
      expect(modelValue).toBe(1);
    });

    it('keeps a single zero', () => {
      type('00');
      expect(input.value).toBe('0');
      expect(modelValue).toBe(0);
    });

    it('formats negative amounts', () => {
      type('-');
      expect(input.value).toBe('-');
      expect(modelValue).toBeNull();

      type('1234');
      expect(input.value).toBe('-1.234');
      expect(modelValue).toBe(-1234);
    });
  });

  describe('decimals', () => {
    it('treats a typed dot as the decimal separator', () => {
      type('12.5');
      expect(input.value).toBe('12,5');
      expect(modelValue).toBe(12.5);
    });

    it('treats a typed dot after thousands separators as the decimal separator', () => {
      type('1234.5');
      expect(input.value).toBe('1.234,5');
      expect(modelValue).toBe(1234.5);
    });

    it('ignores a second decimal separator', () => {
      type('12,5,');
      expect(input.value).toBe('12,5');

      type('.');
      expect(input.value).toBe('12,5');
      expect(modelValue).toBe(12.5);
    });

    it('keeps at most two decimals', () => {
      type('1,234');
      expect(input.value).toBe('1,23');
      expect(modelValue).toBe(1.23);
    });

    it('shows a leading zero when only the decimals are typed', () => {
      type(',5');
      expect(input.value).toBe('0,5');
      expect(modelValue).toBe(0.5);
    });
  });

  describe('deleting', () => {
    it('removes one digit per Backspace without reinterpreting the dots', () => {
      type('1234567');

      const seen: string[] = [];
      const models: Array<number | null> = [];
      for (let i = 0; i < 7; i++) {
        backspace();
        seen.push(input.value);
        models.push(modelValue);
      }

      expect(seen).toEqual(['123.456', '12.345', '1.234', '123', '12', '1', '']);
      expect(models).toEqual([123456, 12345, 1234, 123, 12, 1, null]);
    });

    it('keeps the decimals when deleting them one by one', () => {
      type('1234,56');

      backspace();
      expect(input.value).toBe('1.234,5');
      backspace();
      expect(input.value).toBe('1.234,');
      backspace();
      expect(input.value).toBe('1.234');
      expect(modelValue).toBe(1234);
      backspace();
      expect(input.value).toBe('123');
      expect(modelValue).toBe(123);
    });

    it('does not get stuck when Backspace removes a thousands dot', () => {
      type('1234567');
      moveCursorTo(2); // 1.|234.567

      backspace();

      expect(input.value).toBe('234.567');
      expect(modelValue).toBe(234567);
      expect(input.selectionStart).toBe(0);
    });

    it('removes the digit before the second thousands dot', () => {
      type('1234567');
      moveCursorTo(6); // 1.234.|567

      backspace();

      expect(input.value).toBe('123.567');
      expect(modelValue).toBe(123567);
      expect(input.selectionStart).toBe(4);
    });

    it('removes the digit after a thousands dot with Delete', () => {
      type('1234');
      moveCursorTo(1); // 1|.234

      del();

      expect(input.value).toBe('134');
      expect(modelValue).toBe(134);
      expect(input.selectionStart).toBe(1);
    });

    it('deletes a selection that spans separators', () => {
      type('1234567');
      input.setSelectionRange(1, 6); // 1[.234.]567

      backspace();

      expect(input.value).toBe('1.567');
      expect(modelValue).toBe(1567);
    });

    it('keeps the sign while deleting', () => {
      type('-1234');
      backspace();

      expect(input.value).toBe('-123');
      expect(modelValue).toBe(-123);
    });
  });

  describe('pasting', () => {
    it('reads a plain number with a decimal dot', () => {
      paste('1234567.89');
      expect(input.value).toBe('1.234.567,89');
      expect(modelValue).toBe(1234567.89);
    });

    it('reads Argentine formatting', () => {
      paste('1.234.567,89');
      expect(input.value).toBe('1.234.567,89');
      expect(modelValue).toBe(1234567.89);
    });

    it('reads English formatting', () => {
      paste('1,234.56');
      expect(input.value).toBe('1.234,56');
      expect(modelValue).toBe(1234.56);
    });

    it('ignores currency symbols and spaces', () => {
      paste('$ 1.234,5');
      expect(input.value).toBe('1.234,5');
      expect(modelValue).toBe(1234.5);
    });

    it('reads a lone dot with three digits as thousands', () => {
      paste('12.345');
      expect(input.value).toBe('12.345');
      expect(modelValue).toBe(12345);
    });
  });

  describe('model and blur', () => {
    it('writes numbers, numeric strings and empty values', () => {
      directive.writeValue(1234.5);
      expect(input.value).toBe('1.234,50');

      directive.writeValue('1234.5');
      expect(input.value).toBe('1.234,50');

      directive.writeValue(null);
      expect(input.value).toBe('');
    });

    it('edits a value loaded from the model without reading dots as decimals', () => {
      directive.writeValue(1234567);
      expect(input.value).toBe('1.234.567,00');
      moveCursorTo(9); // 1.234.567|,00

      type('8');

      expect(input.value).toBe('12.345.678,00');
      expect(modelValue).toBe(12345678);
    });

    it('formats with two decimals on blur', () => {
      type('1234,5');

      directive.onBlur();

      expect(input.value).toBe('1.234,50');
      expect(modelValue).toBe(1234.5);
    });

    it('keeps the integer on blur after typing a long amount', () => {
      type('12345678');

      directive.onBlur();

      expect(input.value).toBe('12.345.678,00');
      expect(modelValue).toBe(12345678);
    });

    it('clears the model on blur when nothing numeric is left', () => {
      type('5');
      backspace();

      directive.onBlur();

      expect(input.value).toBe('');
      expect(modelValue).toBeNull();
    });
  });
});
