import { useAuth } from '../auth/useAuth';
import { DashboardPage } from './DashboardPage';
import { PlatformDashboardPage } from './platform/PlatformDashboardPage';

export function HomePage() {
  const { user } = useAuth();
  return user?.isPlatformAdmin ? <PlatformDashboardPage /> : <DashboardPage />;
}
