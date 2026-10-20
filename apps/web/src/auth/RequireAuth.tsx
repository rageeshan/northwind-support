import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from './AuthContext';

/** Sends unsigned-in visitors to /login and remembers the page they wanted. */
export function RequireAuth({ children }: { children: React.ReactNode }) {
  const { isAuthenticated } = useAuth();
  const location = useLocation();

  if (!isAuthenticated) {
    const from = `${location.pathname}${location.search}`;
    return <Navigate to={`/login?from=${encodeURIComponent(from)}`} replace />;
  }

  return children;
}
