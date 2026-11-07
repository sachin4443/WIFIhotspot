import { useAuth } from '../auth/useAuth';
import { OrgDashboardPage } from './org/OrgPages';
import { PlatformDashboardPage } from './platform/PlatformDashboardPage';

export function HomePage() {
  const { user } = useAuth();
  return user?.isPlatformAdmin ? <PlatformDashboardPage /> : <OrgDashboardPage />;
}
