import { Component } from '@angular/core';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { delay, of } from 'rxjs';
import { MonthlyIncreaseService } from '../../../core/services/monthlyIncrease-service/monthly-increase.service';
import { PaymentIncreaseModalComponent } from './payment-increase-modal.component';
import { FinancesComponent } from '../../../pages/finances/finances.component';
import { DashboardComponent } from '../../../pages/dashboard/dashboard.component';
import { ClientDetailModalComponent } from '../client-detail-modal/client-detail-modal.component';

@Component({standalone: true, imports: [PaymentIncreaseModalComponent], template: `
  <app-payment-increase-modal [increaseYear]="2026" [increaseMonth]="11" [currentRent]="232000"
    [projectedRent]="rent" [percentage]="percentage"
    (configuredPercentageResolved)="applyDefault($event)" (percentageChange)="setPercentage($event)"
    (projectedRentChange)="setRent($event)" (percentageBlur)="finishPercentage()"
    (projectedRentBlur)="finishRent()"></app-payment-increase-modal>`})
class InteractionHost {
  state: any;
  planning = false;
  initialize(type: typeof FinancesComponent | typeof DashboardComponent | typeof ClientDetailModalComponent): void {
    this.planning = type === ClientDetailModalComponent;
    this.state = Object.create(type.prototype);
    Object.assign(this.state, { selectedClientRentAmount: 232000, selectedCurrentRent: 232000,
      selectedPreferredPaymentId: 1, getNamePaymentMethodById: () => 'Efectivo', updateProjectedDate: () => {},
      paymentPlanningContext: { baseRent: 232000 }, paymentPlanningIncreases: [],
      client: { preferredPaymentMethod: 'Efectivo' }, cdr: { markForCheck: () => {} } });
  }
  get percentage(): number { return this.planning ? this.state.planningIncreasePercentage : this.state.increasePercentage; }
  get rent(): number { return this.planning ? this.state.planningProjectedRent : this.state.projectedNewRent; }
  setPercentage(value: number): void { this.state[this.planning ? 'planningIncreasePercentage' : 'increasePercentage'] = value; }
  setRent(value: number): void { this.state[this.planning ? 'planningProjectedRent' : 'projectedNewRent'] = value; }
  applyDefault(value: number): void {
    if (this.planning) this.state.applyConfiguredPlanningIncrease(value);
    else this.state.applyConfiguredIncrease(value);
  }
  finishPercentage(): void {
    if (this.planning) this.state.calculatePlanningProjectedRent();
    else this.state.onIncreasePercentageBlur();
  }
  finishRent(): void {
    if (this.planning) this.state.onPlanningProjectedRentBlur();
    else this.state.onProjectedRentBlur();
  }
}

describe('Real increase modal focus/input/blur with actual caller calculations', () => {
  let fixture: ComponentFixture<InteractionHost>;
  beforeEach(async () => {
    await TestBed.configureTestingModule({imports: [InteractionHost], providers: [
      { provide: MonthlyIncreaseService, useValue: { getPercentageForMonth: () => of(6.7).pipe(delay(0)) } }
    ]}).compileComponents();
  });
  afterEach(() => fixture?.destroy());
  function open(type: typeof FinancesComponent | typeof DashboardComponent | typeof ClientDetailModalComponent) {
    fixture = TestBed.createComponent(InteractionHost);
    fixture.componentInstance.initialize(type);
    fixture.detectChanges(); tick(); fixture.detectChanges(); tick();
    return {
      host: fixture.componentInstance,
      percentage: fixture.nativeElement.querySelector('#increase-percentage') as HTMLInputElement,
      amount: fixture.nativeElement.querySelector('#increase-projected-rent') as HTMLInputElement
    };
  }
  function edit(input: HTMLInputElement, value: string): void {
    input.focus(); input.value = value; input.dispatchEvent(new Event('input'));
    fixture.detectChanges(); tick(); input.blur(); fixture.detectChanges(); tick(); fixture.detectChanges();
  }
  for (const [name, type] of [['Finanzas', FinancesComponent], ['Dashboard', DashboardComponent],
    ['Planificación', ClientDetailModalComponent]] as const) {
    it(`${name}: repeated real focus/blur without edits does not change either value`, fakeAsync(() => {
      const {host, percentage, amount} = open(type);
      expect(host.rent).toBe(248000);
      expect(host.percentage).toBe(6.8966);
      for (let click = 0; click < 8; click++) {
        percentage.focus(); expect(document.activeElement).toBe(percentage);
        amount.focus(); amount.blur(); fixture.detectChanges(); tick();
        expect(Number(amount.value)).toBe(248000);
        expect(Number(percentage.value)).toBe(6.8966);
      }
    }));
    it(`${name}: edited percentage and amount each round once and stay stable afterward`, fakeAsync(() => {
      const {host, percentage, amount} = open(type);
      edit(percentage, '8');
      expect(host.rent).toBe(251000);
      expect(host.percentage).toBe(8.1897);
      percentage.focus(); amount.focus(); amount.blur(); fixture.detectChanges(); tick();
      expect(host.rent).toBe(251000);
      edit(amount, '249100');
      expect(host.rent).toBe(250000);
      expect(host.percentage).toBe(7.7586);
      for (let click = 0; click < 5; click++) {
        percentage.focus(); amount.focus(); amount.blur(); fixture.detectChanges(); tick();
        expect(host.rent).toBe(250000);
        expect(host.percentage).toBe(7.7586);
      }
    }));
  }
  it('typing then restoring the original rate does not apply the rounded rate again', fakeAsync(() => {
    const {host, percentage} = open(FinancesComponent);
    percentage.focus();
    for (const value of ['9', '6.8966']) {
      percentage.value = value; percentage.dispatchEvent(new Event('input')); fixture.detectChanges(); tick();
    }
    percentage.blur(); fixture.detectChanges(); tick();
    expect(host.rent).toBe(248000);
    expect(host.percentage).toBe(6.8966);
  }));
});
