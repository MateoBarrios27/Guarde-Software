import { Injectable } from '@angular/core';
import {
  ActivatedRouteSnapshot,
  DetachedRouteHandle,
  RouteReuseStrategy,
} from '@angular/router';

interface CachedWorkspaceRoute {
  handle: DetachedRouteHandle;
  scrollTop: number;
}

/**
 * Conserva las pantallas principales del ERP mientras el usuario navega.
 * Las rutas deben habilitarlo de forma explicita con data.reuse = true.
 */
@Injectable({ providedIn: 'root' })
export class WorkspaceRouteReuseStrategy implements RouteReuseStrategy {
  private readonly cache = new Map<string, CachedWorkspaceRoute>();
  private readonly maxCachedRoutes = 8;
  private reuseEnabled = true;

  shouldDetach(route: ActivatedRouteSnapshot): boolean {
    if (!this.reuseEnabled || route.data['reuse'] !== true) return false;

    const key = this.getRouteKey(route);
    if (!key) return false;

    const existing = this.cache.get(key);
    if (existing) {
      existing.scrollTop = this.readScrollTop();
    }

    return true;
  }

  store(route: ActivatedRouteSnapshot, handle: DetachedRouteHandle | null): void {
    const key = this.getRouteKey(route);
    if (!key) return;

    // Angular llama a store(route, null) inmediatamente despues de volver a
    // adjuntar el handle. Desde ese momento la vista esta activa y ya no debe
    // permanecer referenciada por la cache.
    if (!handle) {
      this.cache.delete(key);
      return;
    }

    this.cache.delete(key);
    this.cache.set(key, {
      handle,
      scrollTop: this.readScrollTop(),
    });

    this.enforceCacheLimit();
  }

  shouldAttach(route: ActivatedRouteSnapshot): boolean {
    if (!this.reuseEnabled || route.data['reuse'] !== true) {
      this.scheduleScrollRestore(0);
      return false;
    }

    const key = this.getRouteKey(route);
    const cached = key ? this.cache.get(key) : undefined;

    this.scheduleScrollRestore(cached?.scrollTop ?? 0);
    return !!cached;
  }

  retrieve(route: ActivatedRouteSnapshot): DetachedRouteHandle | null {
    const key = this.getRouteKey(route);
    return key ? this.cache.get(key)?.handle ?? null : null;
  }

  shouldReuseRoute(
    future: ActivatedRouteSnapshot,
    current: ActivatedRouteSnapshot,
  ): boolean {
    return future.routeConfig === current.routeConfig;
  }

  /** Reactiva la conservacion de pantallas al iniciar una sesion. */
  startSession(): void {
    this.reuseEnabled = true;
  }

  /**
   * Destruye todas las pantallas almacenadas y evita que la pantalla actual
   * se vuelva a guardar durante la navegacion hacia Login.
   */
  clearForLogout(): void {
    this.reuseEnabled = false;

    for (const cached of this.cache.values()) {
      this.destroyHandle(cached.handle);
    }

    this.cache.clear();
  }

  private getRouteKey(route: ActivatedRouteSnapshot): string | null {
    return route.routeConfig?.path || null;
  }

  private readScrollTop(): number {
    if (typeof document === 'undefined') return 0;
    return document.getElementById('main-scroll')?.scrollTop ?? 0;
  }

  private scheduleScrollRestore(scrollTop: number): void {
    if (typeof document === 'undefined') return;

    setTimeout(() => {
      const container = document.getElementById('main-scroll');
      if (container) container.scrollTop = scrollTop;
    });
  }

  private enforceCacheLimit(): void {
    while (this.cache.size > this.maxCachedRoutes) {
      const oldestKey = this.cache.keys().next().value as string | undefined;
      if (!oldestKey) return;

      const oldest = this.cache.get(oldestKey);
      if (oldest) this.destroyHandle(oldest.handle);
      this.cache.delete(oldestKey);
    }
  }

  private destroyHandle(handle: DetachedRouteHandle): void {
    const componentRef = (handle as { componentRef?: { destroy: () => void } })
      .componentRef;
    componentRef?.destroy();
  }
}
