import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By, DomSanitizer } from '@angular/platform-browser';
import { QuillEditorComponent } from 'ngx-quill';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom, NEVER } from 'rxjs';
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

  it('loads the special template and preserves the audience and SMTP selection', () => {
    component.loadLogisticasTemplate();
    expect(component.isLoadingLogisticasTemplate()).toBeTrue();
    http.expectOne('assets/email-templates/logisticas/logisticas.html').flush(template);
    expect(component.isLoadingLogisticasTemplate()).toBeFalse();
    expect(component.isLogisticasTemplate()).toBeTrue();
    expect(component.activeDesignedTemplateLabel()).toBe('Logísticas');
    expect(component.formData().externalRecipientIds).toEqual([11, 22]);
    expect(component.formData().smtpConfigId).toBe(3);
    expect(component.formData().title).toBe('Tu logística, con más espacio.');
  });

  it('edits only the copy with Quill and keeps the inline flyer', () => {
    component.formData.update(data => ({ ...data, content: template }));
    component.updateLogisticasCopyContent('<p><strong>Texto editado</strong></p>');
    expect(component.getLogisticasCopyContent()).toBe('<p><strong>Texto editado</strong></p>');
    expect(component.formData().content).toContain('src="cid:guarde-logisticas-flyer" width="560"');
  });

  it('retains the current communication when the template request fails', () => {
    component.formData.update(data => ({ ...data, content: '<p>Anterior</p>' }));
    component.loadLogisticasTemplate();
    http.expectOne('assets/email-templates/logisticas/logisticas.html').flush('error', { status: 500, statusText: 'Error' });
    expect(component.formData().content).toBe('<p>Anterior</p>');
    expect(component.isLoadingLogisticasTemplate()).toBeFalse();
  });

  it('rejects invalid templates without losing the current text', () => {
    component.formData.update(data => ({ ...data, content: '<p>Anterior</p>' }));
    component.loadLogisticasTemplate();
    http.expectOne('assets/email-templates/logisticas/logisticas.html').flush('<p>No es la plantilla</p>');
    expect(component.formData().content).toBe('<p>Anterior</p>');
    expect(component.isLoadingLogisticasTemplate()).toBeFalse();
  });

  it('does not duplicate an in-flight template request', () => {
    component.loadLogisticasTemplate(); component.loadLogisticasTemplate();
    http.expectOne('assets/email-templates/logisticas/logisticas.html').flush(template);
    expect(component.isLoadingLogisticasTemplate()).toBeFalse();
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
