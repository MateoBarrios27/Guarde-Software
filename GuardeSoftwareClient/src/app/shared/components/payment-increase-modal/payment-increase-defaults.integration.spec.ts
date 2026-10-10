import { FinancesComponent } from '../../../pages/finances/finances.component';
import { DashboardComponent } from '../../../pages/dashboard/dashboard.component';
import { ClientDetailModalComponent } from '../client-detail-modal/client-detail-modal.component';

describe('Configured increase in every modal caller', () => {
  for (const [name, type] of [['Finanzas', FinancesComponent], ['Dashboard', DashboardComponent]] as const) {
    it(`${name} keeps the configured percentage while calculating the rounded new rent`, () => {
      const component: any = Object.create(type.prototype);
      Object.assign(component, { selectedClientRentAmount: 235000, selectedCurrentRent: 235000,
        selectedPreferredPaymentId: 1, getNamePaymentMethodById: () => 'Efectivo', updateProjectedDate: () => {} });
      component.applyConfiguredIncrease(6.7);
      expect(component.projectedNewRent).toBe(251000);
      expect(component.increasePercentage).toBe(6.7);
    });
  }
  it('client planning uses the same configured percentage and existing cash rounding', () => {
    const component: any = Object.create(ClientDetailModalComponent.prototype);
    Object.assign(component, { paymentPlanningContext: { baseRent: 235000 }, paymentPlanningIncreases: [],
      client: { preferredPaymentMethod: 'Efectivo' }, cdr: { markForCheck: jasmine.createSpy('markForCheck') } });
    component.applyConfiguredPlanningIncrease(6.7);
    expect(component.planningProjectedRent).toBe(251000);
    expect(component.planningIncreasePercentage).toBe(6.7);
    expect(component.cdr.markForCheck).toHaveBeenCalled();
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
});
