import { FinancesComponent } from '../../../pages/finances/finances.component';
import { DashboardComponent } from '../../../pages/dashboard/dashboard.component';
import { ClientDetailModalComponent } from '../client-detail-modal/client-detail-modal.component';

describe('Configured increase in every modal caller', () => {
  const cases = [
    { method: 'Efectivo', rent: 251000, effectivePercentage: 6.8085 },
    { method: 'Transferencia', rent: 250800, effectivePercentage: 6.7234 }
  ];
  for (const [name, type] of [['Finanzas', FinancesComponent], ['Dashboard', DashboardComponent]] as const) {
    for (const scenario of cases) {
      it(`${name} initially displays the effective percentage after ${scenario.method} rounding`, () => {
        const component: any = Object.create(type.prototype);
        Object.assign(component, { selectedClientRentAmount: 235000, selectedCurrentRent: 235000,
          selectedPreferredPaymentId: 1, getNamePaymentMethodById: () => scenario.method, updateProjectedDate: () => {} });
        component.applyConfiguredIncrease(6.7);
        expect(component.projectedNewRent).toBe(scenario.rent);
        expect(component.increasePercentage).toBe(scenario.effectivePercentage);
        // Focusing/leaving the amount without editing must not change the initial percentage.
        component.calculatePercentageFromRent();
        expect(component.increasePercentage).toBe(scenario.effectivePercentage);
      });
    }
  }
  for (const scenario of cases) {
    it(`client planning initially displays the effective percentage after ${scenario.method} rounding`, () => {
      const component: any = Object.create(ClientDetailModalComponent.prototype);
      Object.assign(component, { paymentPlanningContext: { baseRent: 235000 }, paymentPlanningIncreases: [],
        client: { preferredPaymentMethod: scenario.method }, cdr: { markForCheck: jasmine.createSpy('markForCheck') } });
      component.applyConfiguredPlanningIncrease(6.7);
      expect(component.planningProjectedRent).toBe(scenario.rent);
      expect(component.planningIncreasePercentage).toBe(scenario.effectivePercentage);
      expect(component.cdr.markForCheck).toHaveBeenCalled();
      component.onPlanningProjectedRentBlur();
      expect(component.planningIncreasePercentage).toBe(scenario.effectivePercentage);
      expect(component.planningProjectedRent).toBe(scenario.rent);
    });
  }
  for (const [name, type] of [['Finanzas', FinancesComponent], ['Dashboard', DashboardComponent]] as const) {
    it(`${name} does not add another payment step after repeated percentage and amount blur`, () => {
      const component: any = Object.create(type.prototype);
      Object.assign(component, { selectedClientRentAmount: 232000, selectedCurrentRent: 232000,
        selectedPreferredPaymentId: 1, getNamePaymentMethodById: () => 'Efectivo', updateProjectedDate: () => {} });
      component.applyConfiguredIncrease(6.7);
      expect(component.projectedNewRent).toBe(248000);
      expect(component.increasePercentage).toBe(6.8966);
      // The amount is the source of truth; do not feed the displayed rounded rate back into it.
      for (let click = 0; click < 5; click++) {
        component.onProjectedRentBlur();
        expect(component.projectedNewRent).toBe(248000);
        expect(component.increasePercentage).toBe(6.8966);
      }
    });
  }
  it('client planning derives the percentage from the entered amount without rounding it twice', () => {
    const component: any = Object.create(ClientDetailModalComponent.prototype);
    Object.assign(component, { paymentPlanningContext: { baseRent: 232000 }, paymentPlanningIncreases: [],
      client: { preferredPaymentMethod: 'Efectivo' }, cdr: { markForCheck: () => {} } });
    component.applyConfiguredPlanningIncrease(6.7);
    for (let click = 0; click < 5; click++) {
      component.onPlanningProjectedRentBlur();
      expect(component.planningProjectedRent).toBe(248000);
      expect(component.planningIncreasePercentage).toBe(6.8966);
    }
  });
  it('going back to a planning stage restores the manual values without requesting defaults again', () => {
    const component: any = Object.create(ClientDetailModalComponent.prototype);
    Object.assign(component, { paymentPlanningStep: 'increase', currentPlanningIncreaseIndex: 1,
      paymentPlanningIncreases: [{ percentage: 12, newRentAmount: 263200 }], planningIncreaseRestored: false });
    component.backPaymentPlanningStep();
    expect(component.planningIncreaseRestored).toBeTrue();
    expect(component.planningIncreasePercentage).toBe(12);
    expect(component.planningProjectedRent).toBe(263200);
    component.startCurrentPlanningIncrease();
    expect(component.planningIncreaseRestored).toBeFalse();
  });
  for (const [name, type] of [['Finanzas', FinancesComponent], ['Dashboard', DashboardComponent]] as const) {
    it(`${name} closes the increase flow on Escape without registering or omitting the payment`, () => {
      const component: any = Object.create(type.prototype);
      const skip = jasmine.createSpy('skip');
      const submit = jasmine.createSpy('submit');
      const summary = jasmine.createSpy('summary');
      Object.assign(component, { showIncreaseOverlay: true, currentIncreaseStep: 1, isSubmittingPayment: false,
        showClientModal: true, showPaymentModal: true, currentIncreaseFlow: 'advance', increaseResolved: true,
        originalBaseRentCopy: 232000, selectedClientRentAmount: 260000, selectedCurrentRent: 260000,
        increaseQueue: [{}], confirmedIncreases: [{}], currentPendingIncrease: {},
        paymentDto: { amount: 232000, appliedIncreases: [{}], skipFutureProjection: false },
        skipIncrease: skip, executeBackendCall: submit, showSummarySwal: summary });
      const event = new KeyboardEvent('keydown', { key: 'Escape', cancelable: true });
      component.onEscapeKey(event);
      expect(component.showIncreaseOverlay).toBeFalse();
      expect(component.currentIncreaseStep).toBe(0);
      expect(component.increaseQueue).toEqual([]);
      expect(component.paymentDto.appliedIncreases).toEqual([]);
      expect(component.paymentDto.amount).toBe(232000);
      expect(component.paymentDto.skipFutureProjection).toBeFalse();
      expect(component.showClientModal).toBeTrue();
      expect(component.showPaymentModal).toBeTrue();
      expect(event.defaultPrevented).toBeTrue();
      expect(skip).not.toHaveBeenCalled(); expect(submit).not.toHaveBeenCalled(); expect(summary).not.toHaveBeenCalled();
    });
  }
  it('client planning Escape closes without treating an increase as omitted', () => {
    const component: any = Object.create(ClientDetailModalComponent.prototype);
    const skip = jasmine.createSpy('skip');
    Object.assign(component, { showPaymentPlanningModal: true, paymentPlanningStep: 'increase',
      isSavingPaymentPlan: false, skipPlanningIncrease: skip });
    component.onPaymentPlanningEscape(new KeyboardEvent('keydown', { key: 'Escape', cancelable: true }));
    expect(component.showPaymentPlanningModal).toBeFalse();
    expect(skip).not.toHaveBeenCalled();
  });

});
