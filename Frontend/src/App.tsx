import { createBrowserRouter, RouterProvider } from 'react-router';
import { ProtectedRoute, PublicOnlyRoute } from './auth/ProtectedRoute';
import { RequireAccess } from './auth/RequireAccess';
import { AppLayout } from './components/layout/AppLayout';
import { AdminUsersPage } from './pages/admins/AdminUsersPage';
import { RolesPage } from './pages/admins/RolesPage';
import { AuditLogsPage } from './pages/audit/AuditLogsPage';
import { HomePage } from './pages/HomePage';
import { LoginPage } from './pages/LoginPage';
import { NotFoundPage } from './pages/NotFoundPage';
import { OrganizationDetailPage } from './pages/organizations/OrganizationDetailPage';
import { OrganizationsPage } from './pages/organizations/OrganizationsPage';
import { RegisterOrganizationPage } from './pages/organizations/RegisterOrganizationPage';
import { OrganizationTypesPage } from './pages/platform/OrganizationTypesPage';
import { PlansPage } from './pages/platform/PlansPage';
import { PlatformSmsPage } from './pages/platform/PlatformSmsPage';
import { SitesDirectoryPage } from './pages/sites/SitesDirectoryPage';
import { WalletDetailPage, WalletsPage } from './pages/wallets/WalletsPage';
import { Permissions as P } from './types/permissions';

const guard = (el: React.ReactNode, anyOf: string[] = [], opts: { platformOnly?: boolean; organizationOnly?: boolean } = {}) => (
  <RequireAccess anyOf={anyOf} {...opts}>{el}</RequireAccess>
);

const router = createBrowserRouter([
  {
    element: <PublicOnlyRoute />,
    children: [{ path: '/login', element: <LoginPage /> }],
  },
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppLayout />,
        children: [
          { index: true, element: <HomePage /> },
          { path: 'organizations', element: guard(<OrganizationsPage />, [P.OrganizationsRead], { platformOnly: true }) },
          { path: 'organizations/new', element: guard(<RegisterOrganizationPage />, [P.OrganizationsWrite], { platformOnly: true }) },
          { path: 'organizations/:id', element: guard(<OrganizationDetailPage />, [P.OrganizationsRead], { platformOnly: true }) },
          { path: 'organization-types', element: guard(<OrganizationTypesPage />, [P.OrganizationTypesRead], { platformOnly: true }) },
          { path: 'subscription-plans', element: guard(<PlansPage />, [P.SubscriptionPlansRead], { platformOnly: true }) },
          { path: 'platform-sms', element: guard(<PlatformSmsPage />, [P.PlatformSmsWrite], { platformOnly: true }) },
          { path: 'all-sites', element: guard(<SitesDirectoryPage />, [P.SitesRead], { platformOnly: true }) },
          { path: 'administrators', element: guard(<AdminUsersPage />, [P.AdminUsersRead]) },
          { path: 'roles', element: guard(<RolesPage />, [P.RolesRead]) },
          { path: 'wallets', element: guard(<WalletsPage />, [P.WalletRead]) },
          { path: 'wallets/:siteId', element: guard(<WalletDetailPage />, [P.WalletRead]) },
          { path: 'audit-logs', element: guard(<AuditLogsPage />, [P.AuditRead]) },
          { path: '*', element: <NotFoundPage /> },
        ],
      },
    ],
  },
]);

export function App() {
  return <RouterProvider router={router} />;
}
