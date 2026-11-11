import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { By } from '@angular/platform-browser';
import { MonthPickerComponent, PickedMonth } from './month-picker.component';

// Lógica del selector de meses (el mismo diseño que el de Finanzas; lo usan Caja, Estadísticas y los formularios). Se
// instancia la clase real y se fija la fecha de hoy: miércoles 4/11/2026.
function createPicker(inputs: Partial<Pick<MonthPickerComponent, 'month' | 'year' | 'minYear' | 'variant' | 'maxMonth'>> = {}): MonthPickerComponent {
  const picker = TestBed.runInInjectionContext(() => new MonthPickerComponent());
  picker.month = 11;
  picker.year = 2026;
  Object.assign(picker, inputs);
  return picker;
}

describe('Selector de meses', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({});
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date(2026, 10, 4, 10, 30));
  });

  afterEach(() => jasmine.clock().uninstall());

  it('marca el mes en curso y deja de marcarlo al mirar otro', () => {
    const picker = createPicker();
    expect(picker.isCurrent).toBeTrue();

    picker.month = 10;
    expect(picker.isCurrent).toBeFalse();

    picker.month = 11;
    picker.year = 2025;
    expect(picker.isCurrent).toBeFalse();
  });

  it('las flechas avisan con -1 o +1 y no cambian nada por su cuenta', () => {
    const picker = createPicker();
    const steps: number[] = [];
    picker.monthStep.subscribe(delta => steps.push(delta));

    picker.step(-1);
    picker.step(1);

    expect(steps).toEqual([-1, 1]);
    expect(picker.month).toBe(11);
    expect(picker.year).toBe(2026);
  });

  it('no se puede ir antes de enero del primer año permitido', () => {
    const picker = createPicker({ month: 1, year: 2026, minYear: 2026 });
    const steps: number[] = [];
    picker.monthStep.subscribe(delta => steps.push(delta));

    expect(picker.atMinimum).toBeTrue();
    picker.step(-1);
    expect(steps).toEqual([]);

    picker.step(1);
    expect(steps).toEqual([1]);

    picker.month = 2;
    expect(picker.atMinimum).toBeFalse();
  });

  it('sin año mínimo no hay tope hacia atrás', () => {
    const picker = createPicker({ month: 1, year: 2020, minYear: null });
    expect(picker.atMinimum).toBeFalse();
    expect(picker.canGoToPreviousRailYear).toBeTrue();
  });

  it('el panel arranca en el año que se está viendo y se abre y se cierra con el título', () => {
    const picker = createPicker({ month: 3, year: 2027 });
    picker.railYear = 1999;

    picker.toggle();
    expect(picker.open).toBeTrue();
    expect(picker.railYear).toBe(2027);

    picker.toggle();
    expect(picker.open).toBeFalse();
  });

  it('cambiar de año en el panel no cambia el mes mostrado ni avisa', () => {
    const picker = createPicker({ minYear: 2026 });
    const picked: PickedMonth[] = [];
    picker.monthSelect.subscribe(value => picked.push(value));

    picker.toggle();
    picker.shiftRailYear(1);
    picker.shiftRailYear(1);

    expect(picker.railYear).toBe(2028);
    expect(picker.year).toBe(2026);
    expect(picked).toEqual([]);
    // El mes elegido sólo se marca en su propio año.
    expect(picker.isActive(10)).toBeFalse();
    picker.shiftRailYear(-1);
    picker.shiftRailYear(-1);
    expect(picker.isActive(10)).toBeTrue();
  });

  it('el panel no retrocede de año por debajo del mínimo', () => {
    const picker = createPicker({ minYear: 2026 });
    picker.toggle();

    expect(picker.canGoToPreviousRailYear).toBeFalse();
    picker.shiftRailYear(-1);
    expect(picker.railYear).toBe(2026);
  });

  it('elegir un mes avisa con mes y año del panel y cierra el panel', () => {
    const picker = createPicker();
    const picked: PickedMonth[] = [];
    picker.monthSelect.subscribe(value => picked.push(value));

    picker.toggle();
    picker.shiftRailYear(1);
    picker.select(2); // Marzo de 2027

    expect(picked).toEqual([{ month: 3, year: 2027 }]);
    expect(picker.open).toBeFalse();
  });

  it('«Volver al mes actual» avisa con el mes de hoy', () => {
    const picker = createPicker({ month: 2, year: 2026 });
    const picked: PickedMonth[] = [];
    picker.monthSelect.subscribe(value => picked.push(value));

    picker.goToCurrent();

    expect(picked).toEqual([{ month: 11, year: 2026 }]);
  });

  it('en el panel, el mes en curso lleva su marca sólo en el año de hoy', () => {
    const picker = createPicker();
    picker.railYear = 2026;
    expect(picker.isCurrentMonth(10)).toBeTrue();
    expect(picker.isCurrentMonth(9)).toBeFalse();

    picker.railYear = 2027;
    expect(picker.isCurrentMonth(10)).toBeFalse();
  });

  it('un clic en el propio título no cuenta como clic afuera', () => {
    const picker = createPicker();
    const title = document.createElement('button');
    (picker as unknown as { title: { nativeElement: HTMLElement } }).title = { nativeElement: title };
    picker.toggle();

    picker.onOutsideClick({ target: title } as unknown as MouseEvent);
    expect(picker.open).toBeTrue();

    picker.onOutsideClick({ target: document.body } as unknown as MouseEvent);
    expect(picker.open).toBeFalse();
  });
});

// --- Como campo de formulario (formControl / ngModel): el valor es «YYYY-MM» ---
@Component({
  imports: [MonthPickerComponent, ReactiveFormsModule],
  template: `<app-month-picker variant="field" [formControl]="control"></app-month-picker>`,
})
class FieldHostComponent {
  control = new FormControl<string | null>(null);
}

describe('Selector de meses como campo de formulario', () => {
  let fixture: ComponentFixture<FieldHostComponent>;
  let host: FieldHostComponent;
  let picker: MonthPickerComponent;

  // El mes y el año son dos elementos pegados (se separan con un hueco de estilo): se unen con un espacio.
  const titleText = () => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.mp-name, .mp-year'))
    .map(element => element.textContent!.trim()).join(' ');

  beforeEach(() => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date(2026, 10, 4, 10, 30));
    TestBed.configureTestingModule({ imports: [FieldHostComponent] });
    fixture = TestBed.createComponent(FieldHostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
    picker = fixture.debugElement.query(By.directive(MonthPickerComponent)).componentInstance;
  });

  afterEach(() => jasmine.clock().uninstall());

  it('sin valor muestra el texto de ayuda y no marca ningún mes como elegido', () => {
    expect(picker.hasValue).toBeFalse();
    expect(titleText()).toBe('Elegir mes');
    picker.railYear = 2026;
    expect(picker.isActive(10)).toBeFalse();
  });

  it('muestra el valor del control, también si llega como fecha completa', () => {
    host.control.setValue('2027-03');
    fixture.detectChanges();
    expect(titleText()).toBe('Marzo 2027');

    host.control.setValue('2026-12-01T12:00:00Z');
    fixture.detectChanges();
    expect(titleText()).toBe('Diciembre 2026');

    host.control.setValue('');
    fixture.detectChanges();
    expect(titleText()).toBe('Elegir mes');
  });

  it('elegir un mes en el panel guarda «YYYY-MM» en el control', () => {
    picker.toggle();
    picker.shiftRailYear(1);
    picker.select(2); // Marzo de 2027

    expect(host.control.value).toBe('2027-03');
    expect(picker.hasValue).toBeTrue();
    expect(picker.open).toBeFalse();
  });

  it('las flechas parten del mes en curso si el campo está vacío y guardan el mes nuevo', () => {
    picker.step(1);
    expect(host.control.value).toBe('2026-12');
    picker.step(1);
    expect(host.control.value).toBe('2027-01');
    picker.step(-1);
    picker.step(-1);
    expect(host.control.value).toBe('2026-11');
  });

  it('un control desactivado no se abre ni cambia', () => {
    host.control.setValue('2026-05');
    host.control.disable();
    fixture.detectChanges();

    picker.toggle();
    picker.step(1);
    expect(picker.open).toBeFalse();
    expect(host.control.value).toBe('2026-05');
    const buttons = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button'));
    expect(buttons.length).toBe(1);
    expect(buttons.every(button => button.disabled)).toBeTrue();
  });

  it('en un formulario se ve como un campo más: sin flechas ni barra azul, con un calendario, y abre el mismo panel', () => {
    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('.mp-field')).not.toBeNull();
    expect(root.querySelector('.mp-bar')).toBeNull();
    expect(root.querySelectorAll('.mp-step').length).toBe(0);
    expect(root.querySelector('.mp-meta')).toBeNull();
    expect(root.querySelector('.mp-field-icon')).not.toBeNull();
    expect(root.querySelector('.mp-caret')).toBeNull();

    picker.toggle();
    fixture.detectChanges();
    expect(picker.open).toBeTrue();
  });

  it('Escape cierra sólo el panel: no sigue de largo hacia el modal que lo contiene', () => {
    picker.toggle();
    const escape = jasmine.createSpyObj<KeyboardEvent>('KeyboardEvent', ['stopPropagation'], { key: 'Escape' });

    picker.onPanelKeydown(escape);

    expect(escape.stopPropagation).toHaveBeenCalled();
    expect(picker.open).toBeFalse();

    const other = jasmine.createSpyObj<KeyboardEvent>('KeyboardEvent', ['stopPropagation'], { key: 'a' });
    picker.toggle();
    picker.onPanelKeydown(other);
    expect(other.stopPropagation).not.toHaveBeenCalled();
    expect(picker.open).toBeTrue();
  });
});

describe('Selector de meses con tope (no mira el futuro)', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({});
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date(2026, 10, 4, 10, 30));
  });

  afterEach(() => jasmine.clock().uninstall());

  const capped = () => createPicker({ month: 11, year: 2026, maxMonth: { month: 11, year: 2026 } });

  it('la flecha del mes siguiente se apaga en el último mes y no avisa', () => {
    const picker = capped();
    const steps: number[] = [];
    picker.monthStep.subscribe(delta => steps.push(delta));

    expect(picker.atMaximum).toBeTrue();
    picker.step(1);
    expect(steps).toEqual([]);

    picker.step(-1);
    expect(steps).toEqual([-1]);

    picker.month = 10;
    expect(picker.atMaximum).toBeFalse();
  });

  it('en el panel no se pueden elegir meses posteriores ni pasar a un año posterior', () => {
    const picker = capped();
    const picked: PickedMonth[] = [];
    picker.monthSelect.subscribe(value => picked.push(value));
    picker.toggle();

    expect(picker.canGoToNextRailYear).toBeFalse();
    picker.shiftRailYear(1);
    expect(picker.railYear).toBe(2026);

    expect(picker.isMonthDisabled(10)).toBeFalse(); // noviembre, el último permitido
    expect(picker.isMonthDisabled(11)).toBeTrue(); // diciembre
    picker.select(11);
    expect(picked).toEqual([]);

    picker.select(9);
    expect(picked).toEqual([{ month: 10, year: 2026 }]);
  });

  it('en años anteriores al tope todos los meses se pueden elegir', () => {
    const picker = capped();
    picker.railYear = 2025;
    expect(picker.isMonthDisabled(11)).toBeFalse();
    expect(picker.canGoToNextRailYear).toBeTrue();
  });
});
