import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { CommunicationTemplatesComponent, PickedCommunicationTemplate } from './communication-templates.component';
import { CommunicationTemplateService } from '../../../core/services/communication-template-service/communication-template.service';
import { CommunicationTemplate, CommunicationTemplateList } from '../../../core/models/communication-template';
import { ICBC_TEMPLATE_CONTENT } from '../../../shared/utils/communication-template-icbc';
import Swal from '../../../shared/services/ui-alert.service';

const API = 'http://localhost:5294/api/CommunicationTemplates';
const INMOBILIARIAS_URL = 'assets/email-templates/inmobiliarias/inmobiliarias.html';
const INMOBILIARIAS_HTML = '<!doctype html><html><body><!-- GUARDE_TEMPLATE:INMOBILIARIAS_V1 --><table><tr><td>Hola</td></tr></table></body></html>';

function tpl(partial: Partial<CommunicationTemplate>): CommunicationTemplate {
  return { id: 1, name: 'Plantilla', subject: 'Asunto', content: null, sourceKey: null, sortOrder: 1, updatedAt: null, ...partial };
}

const LIST: CommunicationTemplateList = {
  storageReady: true,
  templates: [
    tpl({ id: 1, name: 'ICBC', subject: 'Beneficio especial ICBC para clientes', sourceKey: 'icbc', sortOrder: 1 }),
    tpl({ id: 2, name: 'Inmobiliarias', subject: 'PUBLICIDAD | Inmobiliarias', sourceKey: 'inmobiliarias', sortOrder: 2 }),
    tpl({ id: 7, name: 'Aviso propio', subject: 'Aviso', content: '<p>Hola {data[0]}</p>', sortOrder: 3 }),
  ]
};

describe('CommunicationTemplateService', () => {
  let service: CommunicationTemplateService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(CommunicationTemplateService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('uses the edited content when the template has one (no request)', async () => {
    const content = await firstValueFrom(service.resolveContent(tpl({ sourceKey: 'inmobiliarias', content: '<p>Editada</p>' })));
    expect(content).toBe('<p>Editada</p>');
  });

  it('returns the inline original of ICBC without a request', async () => {
    const content = await firstValueFrom(service.resolveContent(tpl({ sourceKey: 'icbc' })));
    expect(content).toBe(ICBC_TEMPLATE_CONTENT);
  });

  it('downloads the asset of a designed system template', async () => {
    const promise = firstValueFrom(service.resolveContent(tpl({ sourceKey: 'inmobiliarias' })));
    http.expectOne(INMOBILIARIAS_URL).flush(INMOBILIARIAS_HTML);
    expect(await promise).toBe(INMOBILIARIAS_HTML);
  });

  it('rejects an asset that is not the template (e.g. the app index.html)', async () => {
    const promise = firstValueFrom(service.resolveContent(tpl({ sourceKey: 'inmobiliarias' })));
    http.expectOne(INMOBILIARIAS_URL).flush('<!doctype html><app-root></app-root>');
    await expectAsync(promise).toBeRejected();
  });

  it('propagates a failed asset request', async () => {
    const promise = firstValueFrom(service.resolveContent(tpl({ sourceKey: 'inmobiliarias' })));
    http.expectOne(INMOBILIARIAS_URL).flush('error', { status: 500, statusText: 'Error' });
    await expectAsync(promise).toBeRejected();
  });
});

describe('CommunicationTemplatesComponent', () => {
  let fixture: ComponentFixture<CommunicationTemplatesComponent>;
  let component: CommunicationTemplatesComponent;
  let http: HttpTestingController;

  async function create(mode: 'manage' | 'pick', list: CommunicationTemplateList = LIST, readOnly = false) {
    fixture = TestBed.createComponent(CommunicationTemplatesComponent);
    component = fixture.componentInstance;
    component.mode = mode;
    component.readOnly = readOnly;
    fixture.detectChanges();
    http.expectOne(API).flush(list);
    fixture.detectChanges();
  }

  beforeEach(async () => {
    spyOn(Swal, 'success').and.returnValue(Promise.resolve({} as never));
    spyOn(Swal, 'error').and.returnValue(Promise.resolve({} as never));
    await TestBed.configureTestingModule({
      imports: [CommunicationTemplatesComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists the templates in their order and previews the first one', async () => {
    await create('manage');
    const names = Array.from(fixture.nativeElement.querySelectorAll('.tpl-item-name')).map((el: any) => el.textContent.trim());
    expect(names).toEqual(['ICBC', 'Inmobiliarias', 'Aviso propio']);
    expect(component.selectedId()).toBe(1);
    expect(component.previewContent()).toBe(ICBC_TEMPLATE_CONTENT);
    expect(component.canEdit()).toBeTrue();
  });

  it('pick mode emits the template with its resolved content', async () => {
    await create('pick');
    const emitted: PickedCommunicationTemplate[] = [];
    component.picked.subscribe(value => emitted.push(value));
    component.select(LIST.templates[1]);
    http.expectOne(INMOBILIARIAS_URL).flush(INMOBILIARIAS_HTML);
    component.usePicked();
    expect(emitted.length).toBe(1);
    expect(emitted[0].template.id).toBe(2);
    expect(emitted[0].content).toBe(INMOBILIARIAS_HTML);
  });

  it('moves a template down and saves the new order', async () => {
    await create('manage');
    component.move(LIST.templates[0], 1);
    const request = http.expectOne(`${API}/order`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ templateIds: [2, 1, 7] });
    request.flush({ storageReady: true, templates: [LIST.templates[1], LIST.templates[0], LIST.templates[2]] });
    expect(component.templates().map(t => t.id)).toEqual([2, 1, 7]);
  });

  it('restores the previous order when saving the order fails', async () => {
    await create('manage');
    component.move(LIST.templates[2], -1);
    expect(component.templates().map(t => t.id)).toEqual([1, 7, 2]);
    http.expectOne(`${API}/order`).flush({ message: 'x' }, { status: 500, statusText: 'Error' });
    expect(component.templates().map(t => t.id)).toEqual([1, 2, 7]);
    expect(Swal.error).toHaveBeenCalled();
  });

  it('is read-only without the database table or for the Observer', async () => {
    await create('manage', { ...LIST, storageReady: false });
    expect(component.canEdit()).toBeFalse();
    expect(fixture.nativeElement.querySelector('.tpl-notice')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.tpl-grip')).toBeNull();

    fixture.destroy();
    await create('manage', LIST, true);
    expect(component.canEdit()).toBeFalse();
  });

  it('creates a new text template with name, subject and content', async () => {
    await create('manage');
    component.startNew();
    component.updateEditor('name', 'Recordatorio');
    component.updateEditor('subject', 'Tu cuota vence pronto');
    component.updateEditor('content', '<p>Hola {data[0]}</p>');
    expect(component.editorValid()).toBeTrue();
    component.save();
    const request = http.expectOne(API);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ name: 'Recordatorio', subject: 'Tu cuota vence pronto', content: '<p>Hola {data[0]}</p>' });
    request.flush(tpl({ id: 9, name: 'Recordatorio', subject: 'Tu cuota vence pronto', content: '<p>Hola {data[0]}</p>', sortOrder: 4 }));
    http.expectOne(API).flush({ storageReady: true, templates: [...LIST.templates, tpl({ id: 9, name: 'Recordatorio', content: '<p>x</p>', sortOrder: 4 })] });
    expect(component.editor()).toBeNull();
    expect(component.selectedId()).toBe(9);
  });

  it('edits a designed system template as HTML, never through Quill', async () => {
    await create('manage');
    component.select(LIST.templates[1]);
    http.expectOne(INMOBILIARIAS_URL).flush(INMOBILIARIAS_HTML);
    component.startEdit();
    expect(component.editor()?.designed).toBeTrue();
    expect(component.editor()?.content).toBe(INMOBILIARIAS_HTML);
  });

  it('renaming a system template keeps its original content (sends null)', async () => {
    await create('manage');
    component.startEdit(LIST.templates[0]);
    component.updateEditor('name', 'ICBC clientes');
    component.save();
    const request = http.expectOne(`${API}/1`);
    expect(request.request.body).toEqual({ name: 'ICBC clientes', subject: 'Beneficio especial ICBC para clientes', content: null });
    request.flush(tpl({ id: 1, name: 'ICBC clientes', sourceKey: 'icbc' }));
    http.expectOne(API).flush(LIST);
  });

  it('a changed system template is saved as an edited copy', async () => {
    await create('manage');
    component.startEdit(LIST.templates[0]);
    component.updateEditor('content', '<p>Texto nuevo</p>');
    component.save();
    const request = http.expectOne(`${API}/1`);
    expect(request.request.body.content).toBe('<p>Texto nuevo</p>');
    request.flush(tpl({ id: 1, sourceKey: 'icbc', content: '<p>Texto nuevo</p>' }));
    http.expectOne(API).flush(LIST);
  });

  it('does not save without a subject', async () => {
    await create('manage');
    component.startNew();
    component.updateEditor('name', 'Sin asunto');
    component.updateEditor('content', '<p>Texto</p>');
    expect(component.editorValid()).toBeFalse();
    component.save();
    http.expectNone(API);
  });
});
