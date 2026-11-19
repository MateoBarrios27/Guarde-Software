import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnDestroy,
  OnInit,
  Output,
  ViewChild,
  computed,
  signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { HttpErrorResponse } from '@angular/common/http';
import { QuillModule } from 'ngx-quill';
import { Subscription } from 'rxjs';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import { CommunicationTemplateService } from '../../../core/services/communication-template-service/communication-template.service';
import { CommunicationTemplate } from '../../../core/models/communication-template';
import { getSystemTemplate, isDesignedTemplateContent } from '../../../shared/utils/communication-template-catalog';
import { buildCommunicationPreviewDocument } from '../../../shared/utils/communication-preview.util';
import { DeleteConfirmationService } from '../../../shared/services/delete-confirmation.service';
import Swal from '../../../shared/services/ui-alert.service';

/** Plantilla elegida y su contenido ya resuelto (editado o el original del sistema). */
export interface PickedCommunicationTemplate {
  template: CommunicationTemplate;
  content: string;
}

interface TemplateEditorState {
  /** null = plantilla nueva. */
  id: number | null;
  sourceKey: string | null;
  name: string;
  subject: string;
  content: string;
  /** Documento de correo completo: se edita como HTML (Quill lo destruiría). */
  designed: boolean;
  /**
   * Plantilla del sistema que todavía usaba su original: si el contenido no cambia se guarda null, así
   * renombrarla no la marca como «editada» ni congela una copia del original.
   */
  untouchedOriginal: string | null;
}

/**
 * Comunicaciones → Plantillas.
 * - mode 'manage': lista ordenable (arrastrar o flechas), vista previa, editar, crear, restaurar y eliminar.
 * - mode 'pick': elegir una plantilla para el comunicado que se está creando.
 * Es un overlay propio para poder abrirse encima del modal de alta sin cerrarlo.
 */
@Component({
  selector: 'app-communication-templates',
  standalone: true,
  imports: [CommonModule, FormsModule, DragDropModule, QuillModule, IconComponent],
  templateUrl: './communication-templates.component.html',
  styleUrl: './communication-templates.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CommunicationTemplatesComponent implements OnInit, AfterViewInit, OnDestroy {
  @Input() mode: 'manage' | 'pick' = 'manage';
  @Input() readOnly = false;
  @Output() closed = new EventEmitter<void>();
  /** Modo 'pick': la plantilla elegida. Modo 'manage': «Usar en un comunicado nuevo». */
  @Output() picked = new EventEmitter<PickedCommunicationTemplate>();

  @ViewChild('dialog') private dialogRef?: ElementRef<HTMLElement>;

  templates = signal<CommunicationTemplate[]>([]);
  storageReady = signal(true);
  isLoading = signal(true);
  loadError = signal('');

  selectedId = signal<number | null>(null);
  /** En celular se alterna entre la lista y el detalle. */
  showDetailOnMobile = signal(false);

  previewHtml = signal<SafeHtml | null>(null);
  previewContent = signal<string>('');
  previewLoading = signal(false);
  previewError = signal('');

  editor = signal<TemplateEditorState | null>(null);
  editorTab = signal<'preview' | 'html'>('preview');
  editorPreview = signal<SafeHtml | null>(null);
  isSaving = signal(false);
  editorError = signal('');
  isReordering = signal(false);
  isResolvingPick = signal(false);

  selectedTemplate = computed(() => this.templates().find(t => t.id === this.selectedId()) ?? null);
  canEdit = computed(() => this.mode === 'manage' && !this.readOnly && this.storageReady());

  private previewSubscription?: Subscription;
  private pickSubscription?: Subscription;
  private readonly previousFocus = document.activeElement as HTMLElement | null;

  constructor(
    private templateService: CommunicationTemplateService,
    private sanitizer: DomSanitizer,
    private deleteConfirmation: DeleteConfirmationService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  ngAfterViewInit(): void {
    this.dialogRef?.nativeElement.focus();
  }

  ngOnDestroy(): void {
    this.previewSubscription?.unsubscribe();
    this.pickSubscription?.unsubscribe();
    this.previousFocus?.focus?.();
  }

  load(selectId: number | null = this.selectedId()): void {
    this.isLoading.set(true);
    this.loadError.set('');
    this.templateService.getAll().subscribe({
      next: list => {
        this.templates.set(list.templates ?? []);
        this.storageReady.set(list.storageReady);
        this.isLoading.set(false);
        const keep = list.templates.find(t => t.id === selectId);
        const next = keep ?? list.templates[0] ?? null;
        if (next) this.select(next, false);
        else this.clearPreview();
      },
      error: () => {
        this.isLoading.set(false);
        this.loadError.set('No se pudieron cargar las plantillas.');
      }
    });
  }

  close(): void {
    if (this.isSaving()) return;
    this.closed.emit();
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key !== 'Escape') return;
    event.stopPropagation();
    if (this.editor()) {
      this.cancelEdit();
      return;
    }
    this.close();
  }

  // ---------- selección y vista previa ----------

  select(template: CommunicationTemplate, showDetail = true): void {
    if (this.editor()) return;
    this.selectedId.set(template.id);
    if (showDetail) this.showDetailOnMobile.set(true);
    this.loadPreview(template);
  }

  backToList(): void {
    this.showDetailOnMobile.set(false);
  }

  private loadPreview(template: CommunicationTemplate): void {
    this.previewSubscription?.unsubscribe();
    this.previewLoading.set(true);
    this.previewError.set('');
    this.previewSubscription = this.templateService.resolveContent(template).subscribe({
      next: content => {
        this.previewContent.set(content);
        this.previewHtml.set(this.trusted(content));
        this.previewLoading.set(false);
      },
      error: () => {
        this.previewContent.set('');
        this.previewHtml.set(null);
        this.previewLoading.set(false);
        this.previewError.set('No se pudo abrir el contenido de esta plantilla.');
      }
    });
  }

  private clearPreview(): void {
    this.selectedId.set(null);
    this.previewContent.set('');
    this.previewHtml.set(null);
  }

  isSystem(template: CommunicationTemplate): boolean {
    return !!template.sourceKey;
  }

  isEdited(template: CommunicationTemplate): boolean {
    return !!template.sourceKey && template.content !== null;
  }

  kindLabel(template: CommunicationTemplate): string {
    if (template.content !== null) return isDesignedTemplateContent(template.content) ? 'Diseñada' : 'Texto';
    const system = getSystemTemplate(template.sourceKey);
    return system?.assetUrl || isDesignedTemplateContent(system?.inlineContent) ? 'Diseñada' : 'Texto';
  }

  // ---------- usar ----------

  usePicked(template: CommunicationTemplate | null = this.selectedTemplate()): void {
    if (!template || this.isResolvingPick()) return;
    const ready = this.selectedId() === template.id && !this.previewLoading() && !this.previewError();
    if (ready) {
      this.picked.emit({ template, content: this.previewContent() });
      return;
    }
    this.isResolvingPick.set(true);
    this.pickSubscription?.unsubscribe();
    this.pickSubscription = this.templateService.resolveContent(template).subscribe({
      next: content => {
        this.isResolvingPick.set(false);
        this.picked.emit({ template, content });
      },
      error: () => {
        this.isResolvingPick.set(false);
        this.previewError.set('No se pudo abrir el contenido de esta plantilla.');
      }
    });
  }

  // ---------- ordenar ----------

  drop(event: CdkDragDrop<CommunicationTemplate[]>): void {
    if (event.previousIndex === event.currentIndex) return;
    const list = [...this.templates()];
    moveItemInArray(list, event.previousIndex, event.currentIndex);
    this.saveOrder(list);
  }

  move(template: CommunicationTemplate, delta: -1 | 1, event?: Event): void {
    event?.stopPropagation();
    const list = [...this.templates()];
    const index = list.findIndex(t => t.id === template.id);
    const target = index + delta;
    if (index < 0 || target < 0 || target >= list.length) return;
    moveItemInArray(list, index, target);
    this.saveOrder(list);
  }

  private saveOrder(list: CommunicationTemplate[]): void {
    if (!this.canEdit() || this.isReordering()) return;
    const previous = this.templates();
    this.templates.set(list);
    this.isReordering.set(true);
    this.templateService.reorder(list.map(t => t.id)).subscribe({
      next: result => {
        this.templates.set(result.templates);
        this.isReordering.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.templates.set(previous);
        this.isReordering.set(false);
        void Swal.error('No se pudo guardar el orden', this.errorMessage(error, 'Volvé a intentarlo.'));
        if (error.status === 409) this.load();
      }
    });
  }

  // ---------- editar / crear ----------

  startNew(): void {
    if (!this.canEdit()) return;
    this.editorError.set('');
    this.editorTab.set('preview');
    this.editor.set({ id: null, sourceKey: null, name: '', subject: '', content: '', designed: false, untouchedOriginal: null });
    this.showDetailOnMobile.set(true);
  }

  startEdit(template: CommunicationTemplate | null = this.selectedTemplate()): void {
    if (!template || !this.canEdit() || this.previewLoading() || this.previewError()) return;
    const content = this.previewContent();
    const designed = isDesignedTemplateContent(content);
    this.editorError.set('');
    this.editorTab.set('preview');
    this.editor.set({
      id: template.id,
      sourceKey: template.sourceKey,
      name: template.name,
      subject: template.subject,
      content,
      designed,
      untouchedOriginal: template.sourceKey && template.content === null ? content : null
    });
    this.refreshEditorPreview();
    this.showDetailOnMobile.set(true);
  }

  duplicate(template: CommunicationTemplate | null = this.selectedTemplate()): void {
    if (!template || !this.canEdit() || this.previewLoading() || this.previewError()) return;
    const content = this.previewContent();
    this.editorError.set('');
    this.editorTab.set('preview');
    this.editor.set({
      id: null,
      sourceKey: null,
      name: `${template.name} (copia)`.slice(0, 150),
      subject: template.subject,
      content,
      designed: isDesignedTemplateContent(content),
      untouchedOriginal: null
    });
    this.refreshEditorPreview();
  }

  cancelEdit(): void {
    if (this.isSaving()) return;
    const wasNew = this.editor()?.id === null;
    this.editor.set(null);
    this.editorError.set('');
    if (wasNew && !this.selectedTemplate()) this.showDetailOnMobile.set(false);
  }

  updateEditor(field: 'name' | 'subject' | 'content', value: string | null): void {
    this.editor.update(state => state ? { ...state, [field]: value ?? '' } : state);
    if (field === 'content') this.refreshEditorPreview();
  }

  setDesigned(designed: boolean): void {
    this.editor.update(state => state ? { ...state, designed } : state);
    this.editorTab.set(designed ? 'html' : 'preview');
    this.refreshEditorPreview();
  }

  private refreshEditorPreview(): void {
    const state = this.editor();
    this.editorPreview.set(state && state.designed ? this.trusted(state.content) : null);
  }

  editorValid = computed(() => {
    const state = this.editor();
    if (!state) return false;
    const hasContent = state.content.replace(/<[^>]*>/g, '').trim().length > 0 || /<img\b/i.test(state.content);
    return state.name.trim().length > 0 && state.subject.trim().length > 0 && hasContent;
  });

  save(): void {
    const state = this.editor();
    if (!state || !this.editorValid() || this.isSaving()) return;
    this.isSaving.set(true);
    this.editorError.set('');
    const keepsOriginal = state.untouchedOriginal !== null && state.content === state.untouchedOriginal;
    const request = { name: state.name.trim(), subject: state.subject.trim(), content: keepsOriginal ? null : state.content };
    const call = state.id === null
      ? this.templateService.create(request)
      : this.templateService.update(state.id, request);
    call.subscribe({
      next: saved => {
        this.isSaving.set(false);
        this.editor.set(null);
        void Swal.success(state.id === null ? 'Plantilla creada' : 'Plantilla guardada', saved.name);
        this.load(saved.id);
      },
      error: (error: HttpErrorResponse) => {
        this.isSaving.set(false);
        this.editorError.set(this.errorMessage(error, 'No se pudo guardar la plantilla.'));
      }
    });
  }

  async restoreOriginal(template: CommunicationTemplate | null = this.selectedTemplate()): Promise<void> {
    if (!template || !this.isEdited(template) || !this.canEdit()) return;
    const confirmed = await this.deleteConfirmation.confirm({
      headerTitle: 'Restaurar plantilla',
      title: '¿Restaurar el contenido original?',
      message: 'Se descartan los cambios hechos al contenido de',
      highlightedText: template.name,
      messageSuffix: 'y vuelve el original. El nombre y el asunto se conservan.',
      confirmText: 'Restaurar original'
    });
    if (!confirmed) return;
    this.templateService.update(template.id, { name: template.name, subject: template.subject, content: null }).subscribe({
      next: () => {
        void Swal.success('Plantilla restaurada', template.name);
        this.load(template.id);
      },
      error: (error: HttpErrorResponse) =>
        void Swal.error('No se pudo restaurar', this.errorMessage(error, 'Volvé a intentarlo.'))
    });
  }

  async remove(template: CommunicationTemplate | null = this.selectedTemplate()): Promise<void> {
    if (!template || !this.canEdit()) return;
    const confirmed = await this.deleteConfirmation.confirm({
      headerTitle: 'Eliminar plantilla',
      title: '¿Eliminar la plantilla?',
      message: 'Se elimina la plantilla',
      highlightedText: template.name,
      messageSuffix: 'de la lista. Los comunicados ya creados con ella no cambian.',
      confirmText: 'Eliminar plantilla'
    });
    if (!confirmed) return;
    this.templateService.delete(template.id).subscribe({
      next: () => {
        void Swal.success('Plantilla eliminada', template.name);
        this.showDetailOnMobile.set(false);
        this.load(null);
      },
      error: (error: HttpErrorResponse) =>
        void Swal.error('No se pudo eliminar', this.errorMessage(error, 'Volvé a intentarlo.'))
    });
  }

  trackById = (_: number, template: CommunicationTemplate) => template.id;

  private trusted(content: string): SafeHtml {
    // Igual que la vista previa de comunicados: iframe sin scripts ni formularios.
    return this.sanitizer.bypassSecurityTrustHtml(buildCommunicationPreviewDocument(content));
  }

  private errorMessage(error: HttpErrorResponse, fallback: string): string {
    const body = error?.error;
    if (typeof body?.message === 'string' && body.message) return body.message;
    if (body?.errors) {
      const first = Object.values(body.errors as Record<string, string[]>)[0];
      if (Array.isArray(first) && first[0]) return first[0];
    }
    return fallback;
  }
}
