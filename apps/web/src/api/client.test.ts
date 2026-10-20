import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { StoredSession } from '../auth/session';
import { AUTH_STORAGE_KEY, writeSession } from '../auth/session';
import { request } from './client';

const session: StoredSession = {
  accessToken: 'test-token',
  expiresAtUtc: '2099-01-01T00:00:00Z',
  user: { id: 1, fullName: 'Alex Turner', email: 'alex.turner@northwind-support.example' },
};

describe('api client', () => {
  const originalLocation = window.location;

  beforeEach(() => {
    sessionStorage.clear();
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    sessionStorage.clear();
    Object.defineProperty(window, 'location', { configurable: true, value: originalLocation });
  });

  it('attaches the stored JWT to every request', async () => {
    writeSession(session);
    vi.mocked(fetch).mockResolvedValue(
      new Response(JSON.stringify({ ok: true }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    await request<{ ok: boolean }>('/tickets');

    expect(fetch).toHaveBeenCalledWith(
      '/api/tickets',
      expect.objectContaining({
        headers: expect.objectContaining({ Authorization: 'Bearer test-token' }),
      }),
    );
  });

  it('clears the session and redirects to login on 401 from a protected call', async () => {
    writeSession(session);

    const assign = vi.fn();
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: { pathname: '/tickets', search: '', assign },
    });

    vi.mocked(fetch).mockResolvedValue(
      new Response(JSON.stringify({ title: 'Unauthorized', detail: 'Invalid email or password.' }), {
        status: 401,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    await expect(request('/tickets')).rejects.toMatchObject({ status: 401 });

    expect(sessionStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
    expect(assign).toHaveBeenCalledWith('/login?from=%2Ftickets');
  });
});
