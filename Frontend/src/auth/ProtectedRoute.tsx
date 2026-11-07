import { Navigate, Outlet, useLocation } from 'react-router';
import { LoadingScreen } from '../components/common/LoadingScreen';
import { useAuth } from './useAuth';

/** Renders child routes only for a signed-in admin; otherwise redirects to /login. */
export function ProtectedRoute() {
  const { status } = useAuth();
  const location = useLocation();

  if (status === 'initializing') return <LoadingScreen message="Restoring your session…" />;
  if (status !== 'authenticated') {
    const from = `${location.pathname}${location.search}${location.hash}`;
    return <Navigate to="/login" replace state={{ from }} />;
  }
  return <Outlet />;
}

/** Keeps signed-in admins away from the login page. */
export function PublicOnlyRoute() {
  const { status } = useAuth();
  const location = useLocation();
  if (status === 'initializing') return <LoadingScreen message="Loading…" />;
  if (status === 'authenticated') return <Navigate to={safeRedirect(location.state)} replace />;
  return <Outlet />;
}

/** Only follow in-app paths from router state, never an absolute or protocol-relative URL. */
export function safeRedirect(state: unknown): string {
  const from = (state as { from?: unknown } | null)?.from;
  return typeof from === 'string' && from.startsWith('/') && !from.startsWith('//') && from !== '/login' ? from : '/';
}
