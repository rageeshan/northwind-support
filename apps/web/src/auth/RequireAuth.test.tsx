import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { AuthProvider } from './AuthContext';
import { RequireAuth } from './RequireAuth';

describe('RequireAuth', () => {
  it('redirects unauthenticated visitors to login with the page they wanted', () => {
    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/tickets/12']}>
          <Routes>
            <Route path="/login" element={<p>Login page</p>} />
            <Route
              path="/tickets/:id"
              element={
                <RequireAuth>
                  <p>Ticket detail</p>
                </RequireAuth>
              }
            />
          </Routes>
        </MemoryRouter>
      </AuthProvider>,
    );

    expect(screen.getByText('Login page')).toBeInTheDocument();
    expect(screen.queryByText('Ticket detail')).not.toBeInTheDocument();
  });
});
