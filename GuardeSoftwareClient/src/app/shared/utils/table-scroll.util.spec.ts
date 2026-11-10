import { isTableViewAtBottom, resolveTableScrollContainer, toggleTableScroll } from './table-scroll.util';

// El botón flotante Bajar/Subir de las tablas. Se arma una «página» real (#main-scroll de 300 px con contenido más alto)
// y un visor de tabla; el ancho de pantalla se simula con matchMedia, que es lo que decide el comportamiento.
// Los métodos de desplazamiento tienen sobrecargas; para comprobar los argumentos se tratan como espías.
const spyOf = (fn: unknown): jasmine.Spy => fn as jasmine.Spy;

describe('table-scroll.util', () => {
  let page: HTMLElement;
  let viewport: HTMLElement;
  let content: HTMLElement;

  const setNarrow = (narrow: boolean) => {
    spyOn(window, 'matchMedia').and.callFake(((query: string) => ({ matches: narrow, media: query })) as typeof window.matchMedia);
  };

  const build = (contentHeight: number, viewportOverflowY: string, viewportMaxHeight?: number) => {
    page = document.createElement('div');
    page.id = 'main-scroll';
    page.style.cssText = 'height:300px;overflow:auto;position:fixed;top:0;left:0;width:200px';
    viewport = document.createElement('div');
    viewport.style.cssText = `overflow-x:auto;overflow-y:${viewportOverflowY};${viewportMaxHeight ? `max-height:${viewportMaxHeight}px;` : ''}`;
    content = document.createElement('div');
    content.style.height = `${contentHeight}px`;
    viewport.appendChild(content);
    page.appendChild(viewport);
    if (viewportMaxHeight) {
      // Con un visor acotado, la página necesita contenido propio para tener también algo que desplazar.
      const spacer = document.createElement('div');
      spacer.style.height = '4000px';
      page.appendChild(spacer);
    }
    document.body.appendChild(page);
  };

  afterEach(() => page?.remove());

  describe('resolveTableScrollContainer', () => {
    it('usa la página cuando el visor no se desplaza en vertical (celular)', () => {
      build(5000, 'hidden');
      expect(resolveTableScrollContainer(viewport)).toBe(page);
    });

    it('usa el visor cuando tiene scroll vertical propio', () => {
      build(5000, 'auto', 200);
      expect(resolveTableScrollContainer(viewport)).toBe(viewport);
    });

    it('usa la página si no hay visor', () => {
      build(5000, 'hidden');
      expect(resolveTableScrollContainer(undefined)).toBe(page);
    });
  });

  describe('toggleTableScroll en celular (la página es la que se mueve)', () => {
    beforeEach(() => setNarrow(true));

    it('baja hasta el final de una vez, aunque el escritorio avance por bloques', () => {
      build(5000, 'hidden');
      spyOn(page, 'scrollTo');
      spyOn(page, 'scrollBy');
      toggleTableScroll(viewport, 3850);
      expect(spyOf(page.scrollBy)).not.toHaveBeenCalled();
      expect(spyOf(page.scrollTo)).toHaveBeenCalledOnceWith({ top: page.scrollHeight, behavior: 'auto' });
    });

    it('vuelve arriba de una vez cuando ya está abajo, y la flecha apunta hacia arriba', () => {
      build(5000, 'hidden');
      page.scrollTop = page.scrollHeight;
      expect(isTableViewAtBottom(viewport)).toBeTrue();
      spyOn(page, 'scrollTo');
      toggleTableScroll(viewport, 3850);
      expect(spyOf(page.scrollTo)).toHaveBeenCalledOnceWith({ top: 0, behavior: 'auto' });
    });

    it('anima sólo los viajes cortos (hasta tres pantallas)', () => {
      build(800, 'hidden');
      spyOn(page, 'scrollTo');
      toggleTableScroll(viewport);
      expect(spyOf(page.scrollTo)).toHaveBeenCalledOnceWith({ top: page.scrollHeight, behavior: 'smooth' });
    });
  });

  describe('toggleTableScroll en escritorio', () => {
    beforeEach(() => setNarrow(false));

    it('avanza por bloques cuando se pide un paso', () => {
      build(9000, 'hidden');
      spyOn(page, 'scrollBy');
      toggleTableScroll(viewport, 3850);
      expect(spyOf(page.scrollBy)).toHaveBeenCalledOnceWith({ top: 3850, behavior: 'smooth' });
    });

    it('sin paso baja hasta el final con animación, aunque el viaje sea largo (no cambia)', () => {
      build(9000, 'hidden');
      spyOn(page, 'scrollTo');
      toggleTableScroll(viewport);
      expect(spyOf(page.scrollTo)).toHaveBeenCalledOnceWith({ top: page.scrollHeight, behavior: 'smooth' });
    });
  });

  describe('toggleTableScroll con un visor que se desplaza solo', () => {
    it('lleva el visor y la página al final, y los trae de vuelta', () => {
      setNarrow(true);
      build(5000, 'auto', 200);
      spyOn(viewport, 'scrollTo');
      spyOn(page, 'scrollTo');
      toggleTableScroll(viewport);
      expect(spyOf(viewport.scrollTo)).toHaveBeenCalledOnceWith({ top: viewport.scrollHeight, behavior: 'auto' });
      expect(spyOf(page.scrollTo)).toHaveBeenCalledOnceWith({ top: page.scrollHeight, behavior: 'auto' });
    });
  });
});
