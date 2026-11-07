import { useCallback, useEffect, useRef, useState, type DependencyList } from 'react';
import { getErrorMessage } from '../api/errors';

export interface ApiState<T> {
  data: T | undefined;
  loading: boolean;
  error: string | null;
  reload: () => void;
  setData: (updater: (current: T | undefined) => T | undefined) => void;
}

/**
 * Loads data from an API call and re-runs when `deps` change. Stale responses are ignored.
 * Pass `enabled: false` to skip loading (e.g. until a required id is known).
 */
export function useApi<T>(loader: (signal: AbortSignal) => Promise<T>, deps: DependencyList, enabled = true): ApiState<T> {
  const [data, setDataState] = useState<T>();
  const [loading, setLoading] = useState(enabled);
  const [error, setError] = useState<string | null>(null);
  const [nonce, setNonce] = useState(0);
  const loaderRef = useRef(loader);
  loaderRef.current = loader;

  useEffect(() => {
    if (!enabled) {
      setLoading(false);
      return;
    }
    const controller = new AbortController();
    setLoading(true);
    setError(null);
    loaderRef.current(controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) setDataState(result);
      })
      .catch((err: unknown) => {
        if (!controller.signal.aborted) setError(getErrorMessage(err));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, nonce, enabled]);

  const reload = useCallback(() => setNonce((n) => n + 1), []);
  const setData = useCallback((updater: (current: T | undefined) => T | undefined) => setDataState(updater), []);

  return { data, loading, error, reload, setData };
}
