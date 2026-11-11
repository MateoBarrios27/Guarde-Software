import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { calculateInterest, resolveInterestRule } from './interest-rule';
import { projectLatePaymentSurcharge } from './late-payment-surcharge';
import { MonthlyInterestService } from '../services/monthlyInterest-service/monthly-interest.service';
import { MonthlyInterestOverview } from '../models/monthly-interest';
import { FinancesComponent } from '../../pages/finances/finances.component';
import { environment } from '../../../environments/environments';

// Intereses por mora configurables (2026-11-10): espejo de InterestRule.cs en el frontend.
describe('Intereses por mora: regla del mes', () => {
  const settings = [
    { effectiveDate: '2026-12-01T00:00:00', percentage: 12.5, applyDay: 15 },
    { effectiveDate: '2027-01-01T00:00:00', percentage: 0, applyDay: 5 },
  ];

  it('un mes sin configurar usa 10 % el día 11 (vence el 10)', () => {
    expect(resolveInterestRule(settings, 2026, 11)).toEqual({ percentage: 10, applyDay: 11, cutoffDay: 10, isDefault: true });
    expect(resolveInterestRule(null, 2026, 11).cutoffDay).toBe(10);
  });

  it('un mes configurado usa su porcentaje y su día', () => {
    expect(resolveInterestRule(settings, 2026, 12)).toEqual({ percentage: 12.5, applyDay: 15, cutoffDay: 14, isDefault: false });
    expect(resolveInterestRule(settings, 2027, 1).percentage).toBe(0);
  });

  it('respeta los valores por defecto que envía el servidor', () => {
    expect(resolveInterestRule([], 2026, 11, { percentage: 8, applyDay: 12 }))
      .toEqual({ percentage: 8, applyDay: 12, cutoffDay: 11, isDefault: true });
  });

  it('trunca a la centena como el backend', () => {
    expect(calculateInterest(280700, 10)).toBe(28000);
    expect(calculateInterest(280700, 12.5)).toBe(35000);
    expect(calculateInterest(999, 10)).toBe(0);
    expect(calculateInterest(280700, 0)).toBe(0);
    // 3 × 0,1 tiene ruido binario: 3000 × 0,1 % no debe perder un escalón.
    expect(calculateInterest(100000, 0.3)).toBe(300);
  });

  it('la proyección del recargo usa el porcentaje del mes (10 % por defecto)', () => {
    expect(projectLatePaymentSurcharge(48700, 232000).surchargeAmount).toBe(28000);
    expect(projectLatePaymentSurcharge(48700, 232000, 12.5).surchargeAmount).toBe(35000);
  });
});

describe('Intereses por mora: servicio', () => {
  let service: MonthlyInterestService;
  let http: HttpTestingController;
  const overview: MonthlyInterestOverview = {
    defaultPercentage: 10,
    defaultApplyDay: 11,
    today: '2026-11-05',
    settings: [{ id: 1, effectiveDate: '2026-12-01T00:00:00', percentage: 12.5, applyDay: 15, cutoffDay: 14, isLocked: false }],
    runs: [],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    service = TestBed.inject(MonthlyInterestService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sin resumen rige el valor por defecto', () => {
    expect(service.ruleForMonth(2026, 12)).toEqual({ percentage: 10, applyDay: 11, cutoffDay: 10, isDefault: true });
  });

  it('con el resumen cargado resuelve el mes configurado', () => {
    service.refresh();
    http.expectOne(`${environment.apiUrl}/MonthlyInterest`).flush(overview);
    expect(service.ruleForDate(new Date(2026, 11, 20)).cutoffDay).toBe(14);
    expect(service.ruleForMonth(2026, 11).isDefault).toBeTrue();
  });

  it('un error al cargar conserva el valor por defecto', () => {
    service.refresh();
    http.expectOne(`${environment.apiUrl}/MonthlyInterest`).flush({}, { status: 500, statusText: 'Error' });
    expect(service.ruleForMonth(2026, 12).percentage).toBe(10);
  });
});

describe('Intereses por mora: Registrar Pago en Finanzas', () => {
  function createComponent(rule: { percentage: number; applyDay: number }): FinancesComponent {
    const router = { events: { subscribe: () => ({ unsubscribe: () => undefined }) } };
    const overlay = { scrollStrategies: { close: () => ({}), reposition: () => ({}) } };
    const interests = {
      ruleForMonth: () => ({ ...rule, cutoffDay: rule.applyDay - 1, isDefault: false }),
      ruleForDate: () => ({ ...rule, cutoffDay: rule.applyDay - 1, isDefault: false }),
    };
    const nothing = null as never;
    const component = new FinancesComponent(
      nothing, nothing, nothing, nothing, router as never, nothing, nothing, nothing,
      nothing, nothing, nothing, nothing, nothing, nothing, overlay as never, interests as never
    );
    component.selectedClientRentAmount = 232000;
    component.selectedInterestAmount = 48700;
    component.selectedPendingSurcharge = 0;
    component.isCurrentMonthPaidFlag = false;
    component.manualDateEnabled = true;
    return component;
  }

  it('con el día 15 configurado, pagar el 14 está dentro del plazo', () => {
    const component = createComponent({ percentage: 12.5, applyDay: 15 });
    component.dateString = '2026-12-14';
    expect(component.getSurchargeScenario()).toBe('D');
    component.selectedPendingSurcharge = 3000;
    expect(component.getSurchargeScenario()).toBe('A');
  });

  it('con el día 15 configurado, pagar el 15 proyecta el 12,5 %', () => {
    const component = createComponent({ percentage: 12.5, applyDay: 15 });
    component.dateString = '2026-12-15';
    expect(component.getSurchargeScenario()).toBe('C');
    expect(component.calculateInterestAmount()).toBe(35000);
  });

  it('sin configuración el corte sigue siendo el 10 y el 10 %', () => {
    const component = createComponent({ percentage: 10, applyDay: 11 });
    component.dateString = '2026-11-10';
    expect(component.getSurchargeScenario()).toBe('D');
    component.dateString = '2026-11-11';
    expect(component.getSurchargeScenario()).toBe('C');
    expect(component.calculateInterestAmount()).toBe(28000);
  });
});
