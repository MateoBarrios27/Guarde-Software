import { Directive, ElementRef, EventEmitter, NgZone, OnDestroy, OnInit, Output } from '@angular/core';

/** Native high frequency events must not schedule a whole application tick. */
@Directive({ selector: '[appOutsideEvents]', standalone: true })
export class OutsideEventsDirective implements OnInit, OnDestroy {
  @Output() quietScroll = new EventEmitter<Event>();
  @Output() quietMouseenter = new EventEmitter<Event>();
  @Output() quietMouseleave = new EventEmitter<Event>();
  private readonly cleanups: (() => void)[] = [];

  constructor(private element: ElementRef<HTMLElement>, private zone: NgZone) {}

  ngOnInit(): void {
    this.zone.runOutsideAngular(() => {
      for (const [name, output] of [
        ['scroll', this.quietScroll], ['mouseenter', this.quietMouseenter], ['mouseleave', this.quietMouseleave],
      ] as const) {
        if (!output.observed) continue;
        const listener = (event: Event): void => output.emit(event);
        this.element.nativeElement.addEventListener(name, listener, { passive: true });
        this.cleanups.push(() => this.element.nativeElement.removeEventListener(name, listener));
      }
    });
  }

  ngOnDestroy(): void { this.cleanups.forEach(cleanup => cleanup()); }
}
