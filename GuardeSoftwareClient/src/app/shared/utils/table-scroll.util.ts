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
