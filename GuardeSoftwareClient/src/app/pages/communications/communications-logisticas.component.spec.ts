import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By, DomSanitizer } from '@angular/platform-browser';
import { QuillEditorComponent } from 'ngx-quill';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom, NEVER, throwError } from 'rxjs';
import { CommunicationsComponent } from './communications.component';
import { CommunicationService } from '../../core/services/communication-service/communication.service';
import { ClientService } from '../../core/services/client-service/client.service';
import { MassCommunicationRecipientService } from '../../core/services/mass-communication-recipient-service/mass-communication-recipient.service';
import { DeleteConfirmationService } from '../../shared/services/delete-confirmation.service';
import { DataRefreshService } from '../../core/services/data-refresh-service/data-refresh.service';
import { AuthService } from '../../core/services/auth-service/auth.service';

describe('Communications logistics template', () => {
  let component: CommunicationsComponent;
  let fixture: ComponentFixture<CommunicationsComponent>;
  let http: HttpTestingController;
  let createCommunication: jasmine.Spy;
  const logisticas = { id: 3, name: 'Logísticas', subject: 'Tu logística, con más espacio.', content: null, sourceKey: 'logisticas', sortOrder: 3, updatedAt: null };
  const template = '<!-- GUARDE_TEMPLATE:LOGISTICAS_V1 --><table><!--GUARDE_LOGISTICAS_COPY_START--><p>Hola</p><!--GUARDE_LOGISTICAS_COPY_END--><img src="cid:guarde-logisticas-flyer" width="560"></table>';

  beforeEach(async () => {
    createCommunication = jasmine.createSpy('createCommunication').and.returnValue(NEVER);
    await TestBed.configureTestingModule({
      imports: [CommunicationsComponent],
      providers: [
        provideHttpClient(), provideHttpClientTesting(),
        { provide: CommunicationService, useValue: { createCommunication, stopSignalRConnection: () => Promise.resolve() } },
        { provide: ClientService, useValue: {} },
        { provide: MassCommunicationRecipientService, useValue: {} },
        { provide: DeleteConfirmationService, useValue: {} },
        { provide: DataRefreshService, useValue: {} },
        { provide: AuthService, useValue: { isObserver: () => false } }
      ]
    }).compileComponents();
    // Do not initialize network/SignalR polling for a template unit test.
    spyOn(CommunicationsComponent.prototype, 'ngOnInit').and.stub();
    fixture = TestBed.createComponent(CommunicationsComponent);
    component = fixture.componentInstance;
    spyOn(component as unknown as { showToast: (...args: unknown[]) => void }, 'showToast');
    http = TestBed.inject(HttpTestingController);
    component.formData.update(data => ({ ...data, externalRecipientIds: [11, 22], smtpConfigId: 3 }));
  });

  afterEach(() => http.verify());

  it('applies a picked template and preserves the audience and SMTP selection', () => {
    component.formData.update(data => ({ ...data, isAccountStatement: true, isNextMonthStatement: true }));
    component.applyTemplate({ template: logisticas, content: template });
    expect(component.isLogisticasTemplate()).toBeTrue();
    expect(component.activeDesignedTemplateLabel()).toBe('Logísticas');
    expect(component.formData().externalRecipientIds).toEqual([11, 22]);
    expect(component.formData().smtpConfigId).toBe(3);
    expect(component.formData().title).toBe('Tu logística, con más espacio.');
    expect(component.formData().channels).toEqual(['Email']);
    expect(component.formData().isAccountStatement).toBeFalse();
    expect(component.formData().isNextMonthStatement).toBeFalse();
  });

  it('edits only the copy with Quill and keeps the inline flyer', () => {
    component.formData.update(data => ({ ...data, content: template }));
    component.updateLogisticasCopyContent('<p><strong>Texto editado</strong></p>');
    expect(component.getLogisticasCopyContent()).toBe('<p><strong>Texto editado</strong></p>');
    expect(component.formData().content).toContain('src="cid:guarde-logisticas-flyer" width="560"');
  });

  it('opens the picker from the form and closes it after applying', () => {
    component.openTemplatePicker();
    expect(component.templatesView()).toBe('pick');
    component.onTemplatePicked({ template: logisticas, content: template });
    expect(component.templatesView()).toBeNull();
    expect(component.formData().content).toBe(template);
  });

  it('renders the real Quill editor and updates copy without losing the flyer', async () => {
    // The preview layout/resources are checked separately; this spec exercises Quill.
    spyOn(component, 'getFormContentPreviewDocument').and.returnValue(
      TestBed.inject(DomSanitizer).bypassSecurityTrustHtml('<p>Vista previa de prueba</p>')
    );
    component.formData.update(data => ({ ...data, title: 'Logísticas', content: template }));
    component.currentModal.set('add');
    component.isEditingLogisticasText.set(true);
    fixture.detectChanges();
    const editor = fixture.debugElement.query(By.directive(QuillEditorComponent)).componentInstance as QuillEditorComponent;
    const quill = editor.quillEditor ?? await firstValueFrom(editor.onEditorCreated);
    expect(quill.root.innerHTML).toContain('Hola');
    quill.clipboard.dangerouslyPasteHTML('<p>Texto del editor real</p>', 'user');
    await fixture.whenStable();
    fixture.detectChanges();
    expect(component.getLogisticasCopyContent().replace(/&nbsp;/g, ' ')).toContain('Texto del editor real');
    expect(component.formData().content).toContain('src="cid:guarde-logisticas-flyer" width="560"');
  });

  describe('seguro contra envíos dobles', () => {
    const valid = () => component.formData.update(data => ({
      ...data, title: 'Aviso', content: '<p>Hola</p>', type: 'enviar_ahora', recipients: ['Ana'], externalRecipientIds: []
    }));

    it('ignores repeated clicks while the request is in flight', () => {
      valid();
      component.addCommunication();
      component.addCommunication();
      component.sendTestCommunication();
      expect(createCommunication).toHaveBeenCalledTimes(1);
      expect(component.isSubmitting()).toBeTrue();
    });

    it('shows the API duplicate notice (409) and reloads the list instead of a generic error', () => {
      valid();
      const reload = spyOn(component, 'loadCommunications').and.stub();
      const toast = (component as unknown as { showToast: jasmine.Spy }).showToast;
      createCommunication.and.returnValue(throwError(() => ({
        status: 409, error: { code: 'DUPLICATE_COMMUNICATION', message: 'Este comunicado ya se creó hace instantes.' }
      })));
      component.currentModal.set('add');
      component.addCommunication();
      expect(toast).toHaveBeenCalledWith('Envío ya registrado', 'Este comunicado ya se creó hace instantes.', 'alert-triangle', 'error');
      expect(reload).toHaveBeenCalled();
      expect(component.currentModal()).toBe('none');
    });

    it('keeps a 3 s cooldown after the request ends, also after an error', () => {
      jasmine.clock().install();
      jasmine.clock().mockDate(new Date(2026, 10, 12, 10, 0, 0));
      try {
        valid();
        createCommunication.and.returnValue(throwError(() => new Error('500')));
        component.addCommunication();
        expect(component.isSubmitting()).toBeFalse();
        component.addCommunication();
        expect(createCommunication).toHaveBeenCalledTimes(1);
        jasmine.clock().tick(3001);
        valid();
        component.addCommunication();
        expect(createCommunication).toHaveBeenCalledTimes(2);
      } finally {
        jasmine.clock().uninstall();
      }
    });
  });

  it('returns the same preview object for the same content (no iframe reload loop)', () => {
    component.selectedCommunication.set({ id: 1, content: '<p>Hola</p>' } as never);
    const first = component.getCommunicationPreviewDocument();
    expect(component.getCommunicationPreviewDocument()).toBe(first);
    component.selectedCommunication.set({ id: 1, content: '<p>Otro</p>' } as never);
    expect(component.getCommunicationPreviewDocument()).not.toBe(first);
  });

  it('does not count pending (scheduled) recipients as failed', () => {
    const dispatches = [
      { dispatchId: 1, status: 'Pendiente', channel: 'Email', isTest: false },
      { dispatchId: 2, status: 'Pendiente', channel: 'Email', isTest: false }
    ] as never[];
    expect(component.getFailedCount(dispatches)).toBe(0);
    expect(component.getPendingCount(dispatches)).toBe(2);
    expect(component.getViewFilter(dispatches)).toBe('pending');
    expect(component.getFailedDispatches({ dispatches, channel: 'Email' } as never)).toEqual([]);
  });

  it('asks for a future date and time before scheduling a saved communication', () => {
    component.sendMode.set('now');
    expect(component.getScheduleProblem()).toBe('');
    component.sendMode.set('schedule');
    expect(component.getScheduleProblem()).toBe('Elegí fecha y hora');
    component.sendScheduleDate.set('2020-01-01');
    component.sendScheduleTime.set('10:00');
    expect(component.getScheduleProblem()).toBe('Elegí un horario futuro');
    component.sendScheduleDate.set('2999-01-01');
    expect(component.getScheduleProblem()).toBe('');
  });

  describe('reenvío de fallidos', () => {
    const comm = {
      id: 9, channel: 'Email', status: 'Finished w/ Errors', recipients: [], creationDate: '2026-11-12',
      dispatches: [
        { dispatchId: 1, clientId: 10, clientName: 'Ana', channel: 'Email', status: 'Fallido', errorMessage: 'rebotó' },
        { dispatchId: 2, clientId: 11, clientName: 'Beto', channel: 'Email', status: 'Fallido', errorMessage: 'buzón lleno' },
        { dispatchId: 3, clientId: 12, clientName: 'Carla', channel: 'Email', status: 'Exitoso' }
      ]
    } as never;

    it('selects every failed dispatch by default and toggles them without touching the DTO', () => {
      component.selectedCommunication.set(comm);
      expect(component.getSelectedRetryCount()).toBe(2);
      component.toggleRetry(component.getFailedDispatches(comm)[0]);
      expect(component.getSelectedRetryCount()).toBe(1);
      component.toggleAllRetrySelection(false);
      expect(component.getSelectedRetryCount()).toBe(0);
      component.toggleAllRetrySelection(true);
      expect(component.getSelectedRetryCount()).toBe(2);
    });

    it('filters the list by name or reason and keeps the selection', () => {
      component.selectedCommunication.set(comm);
      component.retrySearch.set('buzón');
      expect(component.getRetryRows(comm).map(d => d.clientName)).toEqual(['Beto']);
      expect(component.getSelectedRetryCount()).toBe(2);
    });
  });

  it('keeps the template and audience in the test-send request without sending email', () => {
    component.formData.update(data => ({ ...data, title: 'Logísticas', content: template, type: 'enviar_ahora' }));
    component.sendTestCommunication();
    expect(createCommunication).toHaveBeenCalled();
    const request = createCommunication.calls.mostRecent().args[0];
    expect(request.content).toBe(template);
    expect(request.externalRecipientIds).toEqual([11, 22]);
    expect(request.isTestMode).toBeTrue();
    expect(request.channels).toEqual(['Email']);
  });
});
