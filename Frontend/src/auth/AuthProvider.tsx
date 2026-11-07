import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import axios from 'axios';
import { authApi } from '../api/auth';
import { configureApiAuth } from '../api/client';
import { useToast } from '../toast/useToast';
import type { AuthSession, LoginRequest } from '../types/auth';
import { AuthContext, type AuthContextValue, type AuthStatus, type LogoutReason } from './authContext';
import { isExpired, sessionFromLogin, tokenStorage } from './tokenStorage';

/** Warn this long before the access token expires (the API has no refresh endpoint). */
const EXPIRY_WARNING_MS = 2 * 60 * 1000;
/** setTimeout's maximum delay; longer delays fire immediately. */
const MAX_TIMEOUT_MS = 2_147_483_647;

export function AuthProvider({ children }: { children: ReactNode }) {
  const toast = useToast();
  const [session, setSession] = useState<AuthSession | null>(null);
  const [status, setStatus] = useState<AuthStatus>('initializing');

  // The Axios interceptors read the token through this ref, so they always see the latest value.
  const sessionRef = useRef<AuthSession | null>(null);

  const endSession = useCallback(
    (reason: LogoutReason) => {
      const hadSession = sessionRef.current !== null;
      sessionRef.current = null;
      tokenStorage.clear();
      setSession(null);
      setStatus('anonymous');
      if (!hadSession) return;
      if (reason === 'expired') {
        toast.warning('Your session has expired. Please sign in again.', { title: 'Session expired', durationMs: 8000 });
      } else {
        toast.info('You have been signed out.', { title: 'Signed out' });
      }
    },
    [toast],
  );

  const startSession = useCallback((next: AuthSession) => {
    sessionRef.current = next;
    tokenStorage.save(next);
    setSession(next);
    setStatus('authenticated');
  }, []);

  // Wire the API client to the current token and to session expiry.
  useEffect(() => {
    configureApiAuth(
      () => sessionRef.current?.accessToken ?? null,
      () => endSession('expired'),
    );
  }, [endSession]);

  // Restore a stored session on first load and confirm the API still accepts the token.
  useEffect(() => {
    const stored = tokenStorage.load();
    if (!stored) {
      setStatus('anonymous');
      return;
    }
    sessionRef.current = stored;
    const controller = new AbortController();

    authApi
      .me(controller.signal)
      .then((me) => {
        if (controller.signal.aborted || sessionRef.current !== stored) return;
        // The server is the source of truth for permissions.
        startSession({ ...stored, user: { ...stored.user, permissions: me.permissions } });
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted || axios.isCancel(error)) return;
        if (axios.isAxiosError(error) && error.response?.status === 401) {
          // The response interceptor already ended the session.
          return;
        }
        // API unreachable: keep the unexpired session; pages will report their own errors.
        startSession(stored);
      });

    return () => controller.abort();
  }, [startSession]);

  // Expire the session exactly when the token does, with an early warning.
  useEffect(() => {
    if (!session) return;
    const remaining = session.expiresAt - Date.now();
    if (remaining <= 0) {
      endSession('expired');
      return;
    }

    const timers: number[] = [];
    timers.push(window.setTimeout(() => endSession('expired'), Math.min(remaining, MAX_TIMEOUT_MS)));
    if (remaining > EXPIRY_WARNING_MS) {
      timers.push(
        window.setTimeout(
          () => toast.warning('Your session expires in 2 minutes. Save your work and sign in again.', {
            title: 'Session ending soon',
            durationMs: 10_000,
          }),
          Math.min(remaining - EXPIRY_WARNING_MS, MAX_TIMEOUT_MS),
        ),
      );
    }
    return () => timers.forEach((t) => window.clearTimeout(t));
  }, [session, endSession, toast]);

  // A timer can be delayed while the tab sleeps; re-check when the user returns.
  useEffect(() => {
    const check = () => {
      const current = sessionRef.current;
      if (current && isExpired(current)) endSession('expired');
    };
    document.addEventListener('visibilitychange', check);
    window.addEventListener('focus', check);
    return () => {
      document.removeEventListener('visibilitychange', check);
      window.removeEventListener('focus', check);
    };
  }, [endSession]);

  const login = useCallback(
    async (request: LoginRequest) => {
      const response = await authApi.login(request);
      const next = sessionFromLogin(response);
      if (isExpired(next)) throw new Error('The server returned an expired session. Check the server clock.');
      startSession(next);
    },
    [startSession],
  );

  const logout = useCallback((reason: LogoutReason = 'manual') => endSession(reason), [endSession]);

  const value = useMemo<AuthContextValue>(() => {
    const permissions = new Set(session?.user.permissions ?? []);
    return {
      status,
      session,
      user: session?.user ?? null,
      login,
      logout,
      hasPermission: (code) => permissions.has(code),
      hasAnyPermission: (codes) => codes.some((c) => permissions.has(c)),
    };
  }, [status, session, login, logout]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
