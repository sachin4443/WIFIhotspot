import { lazy, Suspense } from 'react';
import { createBrowserRouter, RouterProvider } from 'react-router';
import { LoadingBlock } from './components/ui/States';
import { ProtectedRoute, PublicOnlyRoute } from './auth/ProtectedRoute';
import { RequireAccess } from './auth/RequireAccess';
import { AppLayout } from './components/layout/AppLayout';
import { LoginPage } from './pages/LoginPage';
import { NotFoundPage } from './pages/NotFoundPage';
import { Permissions as P } from './types/permissions';

// Pages are code-split: each route downloads its own chunk on first visit.
const AdminUsersPage = lazy(() => import('./pages/admins/AdminUsersPage').then((m) => ({ default: m.AdminUsersPage })));
const RolesPage = lazy(() => import('./pages/admins/RolesPage').then((m) => ({ default: m.RolesPage })));
const AuditLogsPage = lazy(() => import('./pages/audit/AuditLogsPage').then((m) => ({ default: m.AuditLogsPage })));
const HomePage = lazy(() => import('./pages/HomePage').then((m) => ({ default: m.HomePage })));
const OrganizationDetailPage = lazy(() => import('./pages/organizations/OrganizationDetailPage').then((m) => ({ default: m.OrganizationDetailPage })));
const OrganizationsPage = lazy(() => import('./pages/organizations/OrganizationsPage').then((m) => ({ default: m.OrganizationsPage })));
const RegisterOrganizationPage = lazy(() => import('./pages/organizations/RegisterOrganizationPage').then((m) => ({ default: m.RegisterOrganizationPage })));
const OrganizationTypesPage = lazy(() => import('./pages/platform/OrganizationTypesPage').then((m) => ({ default: m.OrganizationTypesPage })));
const PlansPage = lazy(() => import('./pages/platform/PlansPage').then((m) => ({ default: m.PlansPage })));
const PlatformSmsPage = lazy(() => import('./pages/platform/PlatformSmsPage').then((m) => ({ default: m.PlatformSmsPage })));
const SitesDirectoryPage = lazy(() => import('./pages/sites/SitesDirectoryPage').then((m) => ({ default: m.SitesDirectoryPage })));
const WalletDetailPage = lazy(() => import('./pages/wallets/WalletsPage').then((m) => ({ default: m.WalletDetailPage })));
const WalletsPage = lazy(() => import('./pages/wallets/WalletsPage').then((m) => ({ default: m.WalletsPage })));
const AuthenticationPage = lazy(() => import('./pages/org/OrgPages').then((m) => ({ default: m.AuthenticationPage })));
const BrandingPage = lazy(() => import('./pages/org/OrgPages').then((m) => ({ default: m.BrandingPage })));
const GroupsPage = lazy(() => import('./pages/org/OrgPages').then((m) => ({ default: m.GroupsPage })));
const NasPage = lazy(() => import('./pages/org/OrgPages').then((m) => ({ default: m.NasPage })));
const OrgSitesPage = lazy(() => import('./pages/org/OrgPages').then((m) => ({ default: m.OrgSitesPage })));
const PackagesPage = lazy(() => import('./pages/org/OrgPages').then((m) => ({ default: m.PackagesPage })));
const SmsPage = lazy(() => import('./pages/org/OrgPages').then((m) => ({ default: m.SmsPage })));
const SubscribersPage = lazy(() => import('./pages/org/OrgPages').then((m) => ({ default: m.SubscribersPage })));
const PortalPage = lazy(() => import('./pages/portal/PortalPage').then((m) => ({ default: m.PortalPage })));
const AccountingPage = lazy(() => import('./pages/sessions/AccountingPage').then((m) => ({ default: m.AccountingPage })));
const ActiveSessionsPage = lazy(() => import('./pages/sessions/ActiveSessionsPage').then((m) => ({ default: m.ActiveSessionsPage })));
const RadiusMonitorPage = lazy(() => import('./pages/sessions/RadiusMonitorPage').then((m) => ({ default: m.RadiusMonitorPage })));
const UsageReportsPage = lazy(() => import('./pages/sessions/UsageReportsPage').then((m) => ({ default: m.UsageReportsPage })));

const page = (el: React.ReactNode) => <Suspense fallback={<LoadingBlock />}>{el}</Suspense>;

const guard = (el: React.ReactNode, anyOf: string[] = [], opts: { platformOnly?: boolean; organizationOnly?: boolean } = {}) => (
  <RequireAccess anyOf={anyOf} {...opts}>{page(el)}</RequireAccess>
);

const router = createBrowserRouter([
  // Public captive portal for hotspot visitors (no admin session involved).
  { path: '/p/:key', element: page(<PortalPage />) },
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
          { index: true, element: page(<HomePage />) },
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
