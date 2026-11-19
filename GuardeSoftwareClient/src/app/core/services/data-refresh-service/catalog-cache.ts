import { Observable, defer, shareReplay } from 'rxjs';
import { DataRefreshService } from './data-refresh.service';

/** Share completed and in-flight requests until catalog/all invalidation. Errors are retriable. */
export function cacheCatalog<T>(refresh: DataRefreshService, load: () => Observable<T>): () => Observable<T> {
  let revision = -1;
  let cached: Observable<T> | undefined;
  return () => defer(() => {
    if (!cached || revision !== refresh.catalogRevision) {
      revision = refresh.catalogRevision;
      cached = load().pipe(shareReplay({ bufferSize: 1, refCount: false }));
    }
    return cached;
  });
}
