import { ComponentFixture, TestBed } from '@angular/core/testing';

import { IconComponent, ICONS } from './icon.component';

const DRAWING_ELEMENTS = 'circle, path, line, polyline, polygon, rect, ellipse';

describe('IconComponent', () => {
  let component: IconComponent;
  let fixture: ComponentFixture<IconComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [IconComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(IconComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render the svg of a known icon', () => {
    fixture.componentRef.setInput('name', 'info');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('svg')).not.toBeNull();
  });

  describe('icon library markup', () => {
    const icons = Object.entries(ICONS);

    it('should have icons to check', () => {
      expect(icons.length).toBeGreaterThan(0);
    });

    for (const [name, svg] of icons) {
      it(`${name} is well-formed and paints every shape with the current color`, () => {
        const doc = new DOMParser().parseFromString(svg, 'image/svg+xml');
        // Atributos pegados («fill="none"stroke=...») u otros errores de marcado rompen el XML.
        expect(doc.querySelector('parsererror')?.textContent).toBeUndefined();

        const root = doc.documentElement;
        const rootPaints = root.hasAttribute('fill') || root.hasAttribute('stroke');
        // Una forma sin fill ni stroke se pinta de negro y no sigue el color del texto
        // (el punto de «info», «wallet» o «alert-octagon» salía negro sobre cualquier fondo).
        const unpainted = Array.from(doc.querySelectorAll(DRAWING_ELEMENTS))
          .filter(shape => !rootPaints && !shape.hasAttribute('fill') && !shape.hasAttribute('stroke'))
          .map(shape => shape.outerHTML);
        expect(unpainted).toEqual([]);
      });
    }
  });
});
