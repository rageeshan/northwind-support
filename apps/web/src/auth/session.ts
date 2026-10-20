import type { AuthUser } from '../types/api';

/**
 * Session is kept in sessionStorage (see README). Survives a refresh, cleared when the
 * tab closes. XSS can still read it while the tab is open; that is the trade-off versus
 * an httpOnly cookie.
 */
export const AUTH_STORAGE_KEY = 'northwind.auth';

export interface StoredSession {
  accessToken: string;
  expiresAtUtc: string;
  user: AuthUser;
}

export function readSession(): StoredSession | null {
  const raw = sessionStorage.getItem(AUTH_STORAGE_KEY);
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as StoredSession;
  } catch {
    sessionStorage.removeItem(AUTH_STORAGE_KEY);
    return null;
  }
}

export function writeSession(session: StoredSession): void {
  sessionStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(session));
}

export function clearSession(): void {
  sessionStorage.removeItem(AUTH_STORAGE_KEY);
}

export function getAccessToken(): string | null {
  return readSession()?.accessToken ?? null;
}

/** Leaves a protected page for /login, remembering where the user was. */
export function redirectToLogin(): void {
  if (typeof window === 'undefined') {
    return;
  }

  const { pathname, search } = window.location;
  if (pathname === '/login') {
    return;
  }

  const from = `${pathname}${search}`;
  window.location.assign(`/login?from=${encodeURIComponent(from)}`);
}
