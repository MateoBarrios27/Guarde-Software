/**
 * Which element scrolls the table vertically: the table viewer if it has a vertical scroll of its own, otherwise the page
 * (#main-scroll). On phones the viewer only scrolls sideways (see table-scroll.css), so the page is the one that moves.
 */
export function resolveTableScrollContainer(viewport?: HTMLElement): HTMLElement | null {
  if (viewport && viewport.scrollHeight > viewport.clientHeight &&
      /^(auto|scroll|overlay)$/.test(getComputedStyle(viewport).overflowY)) {
    return viewport;
  }
  return document.getElementById('main-scroll');
}

export function isTableScrollAtBottom(container: HTMLElement): boolean {
  return container.scrollTop > 0 &&
    container.scrollTop + container.clientHeight >= container.scrollHeight - 100;
}

function isNearBottom(element: HTMLElement): boolean {
  return element.scrollTop + element.clientHeight >= element.scrollHeight - 100;
}

/** Phones and small tablets: same breakpoint as table-scroll.css. */
function isNarrowScreen(): boolean {
  return typeof window !== 'undefined' && window.matchMedia('(max-width: 1023px)').matches;
}

/** A smooth scroll over thousands of pixels crawls on phones: only animate short trips (up to three screens). */
function behaviorFor(element: HTMLElement, top: number): ScrollBehavior {
  return Math.abs(top - element.scrollTop) <= element.clientHeight * 3 ? 'smooth' : 'auto';
}

/**
 * Whether the floating button should point up. If the table has a vertical scroll of its own there are two scrollers —
 * the table and the page that holds it — so «bottom» means both are at their end; otherwise it is only the page.
 */
export function isTableViewAtBottom(viewport?: HTMLElement): boolean {
  const container = resolveTableScrollContainer(viewport);
  if (!container) return false;
  if (container !== viewport) return isTableScrollAtBottom(container);
  const page = document.getElementById('main-scroll');
  return container.scrollTop > 0 && isNearBottom(container) && (!page || isNearBottom(page));
}

/**
 * Floating down/up button. With a table that scrolls vertically on its own, one tap takes both the table and the page
 * that holds it to the end (or back to the start). Otherwise it moves the page: on phones straight to the end or back to
 * the top in a single tap (a long table would otherwise take many taps); on desktop by `desktopStep` pixels when given.
 */
export function toggleTableScroll(viewport?: HTMLElement, desktopStep?: number): void {
  const container = resolveTableScrollContainer(viewport);
  if (!container) return;
  const atBottom = isTableViewAtBottom(viewport);

  if (container === viewport) {
    const page = document.getElementById('main-scroll');
    const tableTop = atBottom ? 0 : container.scrollHeight;
    container.scrollTo({ top: tableTop, behavior: behaviorFor(container, tableTop) });
    if (page) {
      const pageTop = atBottom ? 0 : page.scrollHeight;
      page.scrollTo({ top: pageTop, behavior: behaviorFor(page, pageTop) });
    }
  } else {
    // Desktop keeps its smooth scrolling; phones go instantly when the trip is long.
    const narrow = isNarrowScreen();
    const behavior = (top: number): ScrollBehavior => (narrow ? behaviorFor(container, top) : 'smooth');
    if (atBottom) {
      container.scrollTo({ top: 0, behavior: behavior(0) });
    } else if (desktopStep && !narrow) {
      container.scrollBy({ top: desktopStep, behavior: 'smooth' });
    } else {
      const end = container.scrollHeight;
      container.scrollTo({ top: end, behavior: behavior(end) });
    }
  }
}
