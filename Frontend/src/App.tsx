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
import { AuthenticationPage, BrandingPage, GroupsPage, NasPage, OrgSitesPage, PackagesPage, SmsPage, SubscribersPage } from './pages/org/OrgPages';
import { PortalPage } from './pages/portal/PortalPage';
import { AccountingPage } from './pages/sessions/AccountingPage';
import { ActiveSessionsPage } from './pages/sessions/ActiveSessionsPage';
import { RadiusMonitorPage } from './pages/sessions/RadiusMonitorPage';
import { UsageReportsPage } from './pages/sessions/UsageReportsPage';
import { Permissions as P } from './types/permissions';

const guard = (el: React.ReactNode, anyOf: string[] = [], opts: { platformOnly?: boolean; organizationOnly?: boolean } = {}) => (
  <RequireAccess anyOf={anyOf} {...opts}>{el}</RequireAccess>
);

const router = createBrowserRouter([
  // Public captive portal for hotspot visitors (no admin session involved).
  { path: '/p/:key', element: <PortalPage /> },
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
          { path: 'sites', element: guard(<OrgSitesPage />, [P.SitesRead], { organizationOnly: true }) },
          { path: 'nas', element: guard(<NasPage />, [P.NasRead], { organizationOnly: true }) },
          { path: 'packages', element: guard(<PackagesPage />, [P.PackagesRead], { organizationOnly: true }) },
          { path: 'subscribers', element: guard(<SubscribersPage />, [P.SubscribersRead], { organizationOnly: true }) },
          { path: 'groups', element: guard(<GroupsPage />, [P.SubscribersRead], { organizationOnly: true }) },
          { path: 'authentication', element: guard(<AuthenticationPage />, [P.SettingsRead], { organizationOnly: true }) },
          { path: 'sms', element: guard(<SmsPage />, [P.SettingsRead], { organizationOnly: true }) },
          { path: 'branding', element: guard(<BrandingPage />, [P.SettingsRead], { organizationOnly: true }) },
          { path: 'active-sessions', element: guard(<ActiveSessionsPage />, [P.SessionsRead]) },
          { path: 'accounting', element: guard(<AccountingPage />, [P.SessionsRead]) },
          { path: 'reports', element: guard(<UsageReportsPage />, [P.ReportsRead]) },
          { path: 'radius', element: guard(<RadiusMonitorPage />, [P.SessionsRead]) },
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
