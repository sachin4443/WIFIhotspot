import type { AuthSession, LoginResponse } from '../types/auth';

/**
 * The session lives in sessionStorage: it survives a page reload but is cleared when the tab
 * or browser closes, which limits exposure of the bearer token. The API issues no refresh
 * tokens, so a session can never outlive the token's own expiry.
 */
const STORAGE_KEY = 'nxthotspot.session';

/** .NET serializes UTC DateTime with a trailing Z; guard against a value without an offset. */
export function parseUtc(value: string): number {
  const hasOffset = /([zZ]|[+-]\d{2}:?\d{2})$/.test(value);
  return Date.parse(hasOffset ? value : `${value}Z`);
}

export function sessionFromLogin(response: LoginResponse): AuthSession {
  return {
    accessToken: response.accessToken,
    expiresAt: parseUtc(response.expiresAtUtc),
    user: {
      adminUserId: response.adminUserId,
      username: response.username,
      fullName: response.fullName,
      organizationId: response.organizationId,
      isPlatformAdmin: response.isPlatformAdmin,
      roles: response.roles ?? [],
      permissions: response.permissions ?? [],
    },
  };
}

export function isExpired(session: AuthSession, now = Date.now()): boolean {
  return !Number.isFinite(session.expiresAt) || session.expiresAt <= now;
}

function isSession(value: unknown): value is AuthSession {
  if (!value || typeof value !== 'object') return false;
  const s = value as Partial<AuthSession>;
  return (
    typeof s.accessToken === 'string' &&
    typeof s.expiresAt === 'number' &&
    !!s.user &&
    typeof s.user.username === 'string' &&
    Array.isArray(s.user.permissions) &&
    Array.isArray(s.user.roles)
  );
}

export const tokenStorage = {
  load(): AuthSession | null {
    try {
      const raw = window.sessionStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      const parsed: unknown = JSON.parse(raw);
      if (!isSession(parsed) || isExpired(parsed)) {
        window.sessionStorage.removeItem(STORAGE_KEY);
        return null;
      }
      return parsed;
    } catch {
      return null;
    }
  },

  save(session: AuthSession): void {
    try {
      window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    } catch {
      // Storage unavailable (private mode, quota): the session still works until reload.
    }
  },

  clear(): void {
    try {
      window.sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      // ignore
    }
  },
};
