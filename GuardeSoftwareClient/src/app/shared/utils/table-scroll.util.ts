/** Responsive tables scroll locally; desktop lists keep the page scroll. */
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

/**
 * Whether the floating button should point up. When the table has its own scroll (phones) there are two scrollers:
 * the table and the page that holds it, so «bottom» means both are at their end; on desktop it is only the page.
 */
export function isTableViewAtBottom(viewport?: HTMLElement): boolean {
  const container = resolveTableScrollContainer(viewport);
  if (!container) return false;
  if (container !== viewport) return isTableScrollAtBottom(container);
  const page = document.getElementById('main-scroll');
  return container.scrollTop > 0 && isNearBottom(container) && (!page || isNearBottom(page));
}

/**
 * Floating down/up button. With a table that scrolls on its own, one tap takes both the table and the page that holds
 * it to the end (or back to the start); otherwise the table sits below the fold and the tap would move content that is
 * not on screen. On desktop it moves the page, by `desktopStep` pixels when given or straight to the end.
 */
export function toggleTableScroll(viewport?: HTMLElement, desktopStep?: number): void {
  const container = resolveTableScrollContainer(viewport);
  if (!container) return;
  const atBottom = isTableViewAtBottom(viewport);

  if (container === viewport) {
    const page = document.getElementById('main-scroll');
    // A smooth scroll over thousands of pixels crawls on phones: only animate short trips.
    const behaviorFor = (element: HTMLElement, top: number): ScrollBehavior =>
      Math.abs(top - element.scrollTop) <= element.clientHeight * 3 ? 'smooth' : 'auto';
    const tableTop = atBottom ? 0 : container.scrollHeight;
    container.scrollTo({ top: tableTop, behavior: behaviorFor(container, tableTop) });
    if (page) {
      const pageTop = atBottom ? 0 : page.scrollHeight;
      page.scrollTo({ top: pageTop, behavior: behaviorFor(page, pageTop) });
    }
  } else if (atBottom) {
    container.scrollTo({ top: 0, behavior: 'smooth' });
  } else if (desktopStep) {
    container.scrollBy({ top: desktopStep, behavior: 'smooth' });
  } else {
    container.scrollTo({ top: container.scrollHeight, behavior: 'smooth' });
  }
}
