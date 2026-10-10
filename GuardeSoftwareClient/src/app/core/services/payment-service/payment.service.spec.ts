import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { PaymentService } from './payment.service';
import { DataRefreshService } from '../data-refresh-service/data-refresh.service';
import { UiAlertService } from '../../../shared/services/ui-alert.service';
import { CreatePaymentDTO } from '../../dtos/payment/CreatePaymentDTO';

const decision = {
  code: 'PAYMENT_DECISION_REQUIRED', scenario: 'surplus', message: 'Excedente', surplus: 10,
  remainingDebt: 0, decisionToken: 'decision-token', expectedPaymentStateToken: 'state-token',
  options: [{ action: 'credit_next_month', title: 'Un débito', description: 'Saldo a favor',
    debits: [{ month: '2026-11-01T00:00:00', amount: 100 }], resultingBalance: -90, previousBalance: 10, nextPaymentDate: "2026-11-01T00:00:00" }]
};
describe('PaymentService consultation', () => {
  let service: PaymentService;
  let http: HttpTestingController;
  let refresh: jasmine.Spy;
  const dto: CreatePaymentDTO = { clientId: 1, paymentMethodId: 1, movementType: 'CREDITO',
    concept: 'Pago', amount: 110, date: new Date(2026, 9, 5), isAdvancePayment: false };
  beforeEach(() => {
    refresh = jasmine.createSpy('notify');
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: DataRefreshService, useValue: { notify: refresh } }] });
    service = TestBed.inject(PaymentService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  const paymentRequest = (request: { url: string }) => request.url.endsWith('/Payment');
  it('retries only the confirmed action with its state token and refreshes once', async () => {
    const alert = spyOn(UiAlertService, 'fire').and.resolveTo({ isConfirmed: true, isDenied: false, isDismissed: false, value: 'credit_next_month' });
    const promise = firstValueFrom(service.CreatePayment(dto));
    http.expectOne(paymentRequest).flush(decision, { status: 422, statusText: 'Unprocessable Entity' });
    expect(refresh).not.toHaveBeenCalled();
    await new Promise<void>(resolve => setTimeout(resolve, 0));
    const retry = http.expectOne(paymentRequest);
    expect(retry.request.body.futureDebitAction).toBe('credit_next_month');
    expect(retry.request.body.paymentDecisionToken).toBe('decision-token');
    expect(retry.request.body.expectedPaymentStateToken).toBe('state-token');
    expect(retry.request.body.amount).toBe(110);
    retry.flush({ message: 'created' });
    await promise;
    expect(alert).toHaveBeenCalledTimes(1);
    expect(refresh).toHaveBeenCalledOnceWith(['finances', 'clients']);
    expect(dto.futureDebitAction).toBeUndefined();
  });
  it('cancellation leaves the payment editable without retry or refresh', async () => {
    spyOn(UiAlertService, 'fire').and.resolveTo({ isConfirmed: false, isDenied: false, isDismissed: true });
    const result = firstValueFrom(service.CreatePayment(dto)).catch(error => error);
    http.expectOne(paymentRequest).flush(decision, { status: 422, statusText: 'Unprocessable Entity' });
    expect((await result).paymentDecisionCancelled).toBeTrue();
    http.expectNone(paymentRequest);
    expect(refresh).not.toHaveBeenCalled();
  });
  it('does not retry a changed account or hide the conflict', async () => {
    spyOn(UiAlertService, 'fire').and.resolveTo({ isConfirmed: true, isDenied: false, isDismissed: false, value: 'credit_next_month' });
    const result = firstValueFrom(service.CreatePayment(dto)).catch(error => error);
    http.expectOne(paymentRequest).flush(decision, { status: 422, statusText: 'Unprocessable Entity' });
    await new Promise<void>(resolve => setTimeout(resolve, 0));
    http.expectOne(paymentRequest).flush({ code: 'PAYMENT_STATE_CHANGED' }, { status: 409, statusText: 'Conflict' });
    expect((await result).status).toBe(409);
    expect(refresh).not.toHaveBeenCalled();
  });
  it('ordinary payment never opens consultation', async () => {
    const alert = spyOn(UiAlertService, 'fire');
    const result = firstValueFrom(service.CreatePayment(dto));
    http.expectOne(paymentRequest).flush({ message: 'created' });
    await result;
    expect(alert).not.toHaveBeenCalled();
    expect(refresh).toHaveBeenCalledTimes(1);
  });
});
