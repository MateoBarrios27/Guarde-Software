import { FinancesComponent } from './finances.component';
import { IconComponent } from '../../shared/components/icon/icon.component';

// Lógica del encabezado de período y de los métodos de pago de Finanzas. Se instancia la clase real
// con dependencias nulas (sólo el router y el overlay necesitan forma) y se fija la fecha de hoy.
function createComponent(): FinancesComponent {
  const router = { events: { subscribe: () => ({ unsubscribe: () => undefined }) } };
  const overlay = { scrollStrategies: { close: () => ({}), reposition: () => ({}) } };
  const nothing = null as never;
  const component = new FinancesComponent(
    nothing, nothing, nothing, nothing, router as never, nothing, nothing, nothing,
    nothing, nothing, nothing, nothing, nothing, nothing, overlay as never, nothing
  );
  spyOn(component, 'filterPayments');
  return component;
}

describe('Finanzas: encabezado de período', () => {
  let component: FinancesComponent;

  beforeEach(() => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date(2026, 10, 4, 10, 30)); // miércoles 4/11/2026
    component = createComponent();
  });

  afterEach(() => jasmine.clock().uninstall());

  it('muestra el mes en curso por defecto, con su marca', () => {
    expect(component.dateScope).toBe('month');
    expect(component.dateBanner).toEqual({
      eyebrow: 'Pagos de', title: 'Noviembre', subtitle: '2026', badge: 'Mes en curso', returnLabel: null
    });
  });

  it('ofrece volver al mes actual cuando se mira otro mes', () => {
    component.selectedMonth = '2026-10';
    expect(component.dateBanner.title).toBe('Octubre');
    expect(component.dateBanner.badge).toBeNull();
    expect(component.dateBanner.returnLabel).toBe('Volver al mes actual');
  });

  it('las flechas cruzan el año hacia atrás y hacia adelante', () => {
    component.selectedMonth = '2026-01';
    component.shiftDateScope(-1);
    expect(component.selectedMonth).toBe('2025-12');
    expect(component.railYear).toBe(2025);

    component.selectedMonth = '2026-12';
    component.shiftDateScope(1);
    expect(component.selectedMonth).toBe('2027-01');
    expect(component.railYear).toBe(2027);
  });

  it('al cambiar de mes vuelve a la primera página y vuelve a filtrar', () => {
    component.page = 4;
    component.shiftDateScope(-1);
    expect(component.page).toBe(1);
    expect(component.filterPayments).toHaveBeenCalledTimes(1);
  });

  it('en modo día muestra el día de la semana y avanza por días, también entre meses', () => {
    component.dateFilterType = 'day';
    component.selectedDay = '2026-11-03';
    expect(component.dateScope).toBe('day');
    expect(component.dateBanner).toEqual({
      eyebrow: 'Pagos del día', title: 'Martes', subtitle: '3 de noviembre de 2026', badge: null, returnLabel: 'Volver a hoy'
    });

    component.selectedDay = '2026-12-01';
    component.shiftDateScope(-1);
    expect(component.selectedDay).toBe('2026-11-30');
    component.shiftDateScope(1);
    component.shiftDateScope(1);
    expect(component.selectedDay).toBe('2026-12-02');
  });

  it('hoy lleva la marca «Hoy» y «volver» lleva al día y al mes de hoy', () => {
    component.dateFilterType = 'day';
    component.selectedDay = '2026-11-04';
    expect(component.dateBanner.badge).toBe('Hoy');
    expect(component.dateBanner.title).toBe('Miércoles');

    component.selectedDay = '2026-10-15';
    component.goToCurrentPeriod();
    expect(component.selectedDay).toBe('2026-11-04');

    component.dateFilterType = 'month';
    component.selectedMonth = '2025-02';
    component.goToCurrentPeriod();
    expect(component.selectedMonth).toBe('2026-11');
    expect(component.railYear).toBe(2026);
  });

  it('sin filtro de fecha, o con el mes vacío, es «Todos» y las flechas no hacen nada', () => {
    component.dateFilterType = 'none';
    expect(component.dateScope).toBe('all');
    expect(component.dateBanner.title).toBe('Todos');
    component.shiftDateScope(1);
    component.goToCurrentPeriod();
    expect(component.filterPayments).not.toHaveBeenCalled();

    component.dateFilterType = 'month';
    component.selectedMonth = '';
    expect(component.dateScope).toBe('all');
    expect(component.dateBanner.subtitle).toBe('Sin filtro de fecha');
  });

  it('el selector de meses elige el mes del año que se está recorriendo y pasa a «Por Mes»', () => {
    component.dateFilterType = 'day';
    component.selectedDay = '2026-11-04';
    component.shiftRailYear(-1);
    expect(component.railYear).toBe(2025);
    expect(component.dateScope).toBe('day'); // recorrer el año no filtra

    component.selectRailMonth(2);
    expect(component.dateFilterType).toBe('month');
    expect(component.selectedDay).toBe('');
    expect(component.selectedMonth).toBe('2025-03');
    expect(component.isRailMonthActive(2)).toBeTrue();
    expect(component.isRailMonthActive(10)).toBeFalse();
  });

  it('Mes / Día / Todo: Día toma hoy, Mes toma el mes del día que se veía y Todo quita la fecha', () => {
    component.setDateMode('day');
    expect(component.dateFilterType).toBe('day');
    expect(component.selectedDay).toBe('2026-11-04');
    expect(component.dateScope).toBe('day');

    component.selectedDay = '2026-10-15';
    component.setDateMode('month');
    expect(component.selectedMonth).toBe('2026-10');
    expect(component.selectedDay).toBe('');
    expect(component.railYear).toBe(2026);

    component.setDateMode('none');
    expect(component.dateScope).toBe('all');
    expect(component.selectedMonth).toBe('');

    component.setDateMode('month');
    expect(component.selectedMonth).toBe('2026-11'); // sin día previo: el mes en curso
    expect(component.filterPayments).toHaveBeenCalledTimes(4);
  });

  it('volver a tocar Mes conserva el mes elegido y Día conserva el día elegido', () => {
    component.selectedMonth = '2025-03';
    component.setDateMode('month');
    expect(component.selectedMonth).toBe('2025-03');

    component.setDateMode('day');
    component.selectedDay = '2026-01-20';
    component.setDateMode('day');
    expect(component.selectedDay).toBe('2026-01-20');
  });

  it('el panel de meses se abre y cierra desde el título, y elegir un mes lo cierra', () => {
    component.selectedMonth = '2025-06';
    component.railYear = 2030;
    component.toggleMonthPicker();
    expect(component.monthPickerOpen).toBeTrue();
    expect(component.railYear).toBe(2025); // abre en el año del mes elegido

    component.toggleMonthPicker();
    expect(component.monthPickerOpen).toBeFalse();

    component.toggleMonthPicker();
    component.selectRailMonth(0);
    expect(component.monthPickerOpen).toBeFalse();
    expect(component.selectedMonth).toBe('2025-01');
  });

  it('un clic afuera cierra el panel, pero no un clic sobre el propio título', () => {
    const title = document.createElement('button');
    const inner = document.createElement('span');
    title.appendChild(inner);
    component.toggleMonthPicker();

    component.onMonthPickerOutsideClick({ target: inner } as unknown as MouseEvent, title);
    expect(component.monthPickerOpen).toBeTrue();

    component.onMonthPickerOutsideClick({ target: document.body } as unknown as MouseEvent, title);
    expect(component.monthPickerOpen).toBeFalse();
  });

  it('cambiar de modo o limpiar los filtros cierra el panel', () => {
    component.toggleMonthPicker();
    component.setDateMode('none');
    expect(component.monthPickerOpen).toBeFalse();

    component.toggleMonthPicker();
    component.resetFilters();
    expect(component.monthPickerOpen).toBeFalse();
    expect(component.selectedMonth).toBe('2026-11');
  });

  it('el mes marcado como actual sólo aparece en el año en curso', () => {
    expect(component.isRailMonthCurrent(10)).toBeTrue();
    expect(component.isRailMonthCurrent(9)).toBeFalse();
    component.shiftRailYear(-1);
    expect(component.isRailMonthCurrent(10)).toBeFalse();
  });
});

describe('Finanzas: métodos de pago', () => {
  let component: FinancesComponent;

  beforeEach(() => { component = createComponent(); });

  it('asigna color e ícono propios a cada método, sin distinguir mayúsculas ni tildes', () => {
    expect(component.getPaymentMethodTone('Efectivo')).toEqual({ cls: 'finance-method-cash', icon: 'banknote' });
    expect(component.getPaymentMethodTone('TRANSFERENCIA')).toEqual({ cls: 'finance-method-transfer', icon: 'bank' });
    expect(component.getPaymentMethodTone('Transferencia Bancaria')).toEqual({ cls: 'finance-method-transfer', icon: 'bank' });
    // En producción la transferencia se llama «Banco»: mismo color e ícono que «Transferencia».
    const transfer = component.getPaymentMethodTone('Transferencia');
    expect(component.getPaymentMethodTone('Banco')).toEqual(transfer);
    expect(component.getPaymentMethodTone('BANCO')).toEqual(transfer);
    expect(component.getPaymentMethodTone(' banco ')).toEqual(transfer);
    expect(component.getPaymentMethodTone('Mercado Pago')).toEqual({ cls: 'finance-method-mp', icon: 'mercado-pago' });
    expect(component.getPaymentMethodTone('MERCADOPAGO')).toEqual({ cls: 'finance-method-mp', icon: 'mercado-pago' });
    expect(component.getPaymentMethodTone('MP')).toEqual({ cls: 'finance-method-mp', icon: 'mercado-pago' });
    expect(component.getPaymentMethodTone('Ajuste / Manual')).toEqual({ cls: 'finance-method-manual', icon: 'sliders' });
  });

  it('un método desconocido, o sin nombre, usa la billetera gris pizarra', () => {
    expect(component.getPaymentMethodTone('Cheque')).toEqual({ cls: 'finance-method-other', icon: 'wallet' });
    expect(component.getPaymentMethodTone('Empresa MP Logística')).toEqual({ cls: 'finance-method-other', icon: 'wallet' });
    expect(component.getPaymentMethodTone('Bancolombia')).toEqual({ cls: 'finance-method-other', icon: 'wallet' }); // «banco» sólo como palabra
    expect(component.getPaymentMethodTone(null)).toEqual({ cls: 'finance-method-other', icon: 'wallet' });
  });

  it('cada método conocido usa un ícono distinto', () => {
    const icons = ['Efectivo', 'Transferencia', 'Mercado Pago', 'Ajuste / Manual', 'Cheque']
      .map(name => component.getPaymentMethodTone(name).icon);
    expect(new Set(icons).size).toBe(icons.length);
  });

  it('elegir un método filtra y volver a elegirlo, o «Todos», quita el filtro', () => {
    component.page = 3;
    component.selectMethodFilter('Efectivo');
    expect(component.selectedMethodFilter).toBe('Efectivo');
    expect(component.page).toBe(1);
    component.selectMethodFilter('Efectivo');
    expect(component.selectedMethodFilter).toBe('');
    component.selectMethodFilter('Transferencia');
    component.selectMethodFilter('');
    expect(component.selectedMethodFilter).toBe('');
    expect(component.filterPayments).toHaveBeenCalledTimes(4);
  });
});

describe('Finanzas: íconos de los métodos', () => {
  const sanitizer = { bypassSecurityTrustHtml: (html: string) => html };
  const render = (name: string): string => {
    const icon = new IconComponent(sanitizer as never);
    icon.name = name;
    icon.ngOnChanges();
    return String(icon.svgContent);
  };

  ['banknote', 'bank', 'sliders', 'mercado-pago', 'wallet', 'chevron-down'].forEach(name => {
    it(`el ícono «${name}» existe en la biblioteca`, () => {
      expect(render(name)).toContain('<svg');
    });
  });

  it('el de Mercado Pago es el logo relleno de la marca y toma el color del texto', () => {
    const mp = render('mercado-pago');
    // Sin `fill="currentColor"` el logo quedaría negro sobre la pastilla y el chip elegido.
    expect(mp).toContain('fill="currentColor"');
    expect(mp).toContain('viewBox="0 0 24 24"');
    expect(mp).not.toContain('stroke=');
    expect(mp).not.toMatch(/#[0-9a-f]{3,8}\b/i);
  });
});
