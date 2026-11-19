import { Directive, EmbeddedViewRef, Input, OnChanges, OnInit, TemplateRef, ViewContainerRef } from '@angular/core';

/** Keep background DOM in place while an overlay covers it; catch up on close. */
@Directive({ selector: '[appSuspendView]', standalone: true })
export class SuspendViewDirective implements OnInit, OnChanges {
  @Input() appSuspendView = false;
  private view?: EmbeddedViewRef<unknown>;
  constructor(private template: TemplateRef<unknown>, private container: ViewContainerRef) {}

  ngOnInit(): void {
    this.view = this.container.createEmbeddedView(this.template);
    this.updateState();
  }

  ngOnChanges(): void { this.updateState(); }

  private updateState(): void {
    if (this.appSuspendView) this.view?.detach();
    else {
      this.view?.reattach();
      this.view?.markForCheck();
    }
  }
}
