import type { ReactNode } from 'react';
import { Link, useNavigate } from 'react-router';
import { FiPlus, FiRefreshCw, FiUsers } from 'react-icons/fi';
import { dashboardApi } from '../../api/platform';
import { useAuth } from '../../auth/useAuth';
import { PageHeader } from '../../components/common/PageHeader';
import { StatusBadge } from '../../components/ui/Badge';
import { Card } from '../../components/ui/Card';
import { ErrorState, LoadingBlock, Notice } from '../../components/ui/States';
import { useApi } from '../../hooks/useApi';
import { BrandingPanel } from '../../panels/BrandingPanel';
import { GroupsPanel } from '../../panels/GroupsPanel';
import { LoginMethodsPanel } from '../../panels/LoginMethodsPanel';
import { NasPanel } from '../../panels/NasPanel';
import { PackagesPanel } from '../../panels/PackagesPanel';
import { ProfilePanel } from '../../panels/ProfilePanel';
import { SettingsPanel } from '../../panels/SettingsPanel';
import { SitesPanel } from '../../panels/SitesPanel';
import { SmsPanel } from '../../panels/SmsPanel';
import { SubscribersPanel } from '../../panels/SubscribersPanel';
import { Permissions as P } from '../../types/permissions';
import type { WorkspaceOrganization } from '../../types/workspace';
import { useWorkspace } from '../../workspace/useWorkspace';
import { OrganizationOverview } from '../organizations/OrganizationOverview';

/** Resolves the signed-in admin's organization before rendering an organization page. */
function WithOrganization({ children }: { children: (org: WorkspaceOrganization) => ReactNode }) {
  const { organization, loading, error, reload } = useWorkspace();
  if (error) return <Card><ErrorState message={error} onRetry={reload} /></Card>;
  if (loading || !organization) return <LoadingBlock />;
  return <>{children(organization)}</>;
}

export function OrgDashboardPage() {
  const { user, hasPermission } = useAuth();
  const { context } = useWorkspace();
  const navigate = useNavigate();
  return (
    <WithOrganization>
      {(org) => <OrgDashboardBody org={org} siteLimited={!context?.hasAllSites} name={user?.fullName ?? ''} canAddSubscribers={hasPermission(P.SubscribersWrite)} onOpenSite={() => navigate('/sites')} />}
    </WithOrganization>
  );
}

function OrgDashboardBody({ org, siteLimited, name, canAddSubscribers, onOpenSite }: { org: WorkspaceOrganization; siteLimited: boolean; name: string; canAddSubscribers: boolean; onOpenSite: () => void }) {
  const stats = useApi((s) => dashboardApi.organization(org.id, s), [org.id]);
  return (
    <>
      <PageHeader
        title={`Welcome, ${name.split(' ')[0] || 'admin'}`}
        subtitle={<span className="d-inline-flex flex-wrap align-items-center gap-2">{org.name} · {org.organizationTypeName} <StatusBadge status={org.status} /></span>}
        actions={
          <>
            <button type="button" className="btn btn-light btn-icon" onClick={stats.reload} disabled={stats.loading}><FiRefreshCw className={stats.loading ? 'nxt-spin' : ''} /> Refresh</button>
            {canAddSubscribers && <Link to="/subscribers" className="btn btn-primary btn-icon"><FiPlus /> Subscribers</Link>}
          </>
        }
      />
      {siteLimited && <Notice className="mb-3">You are a site administrator. Figures cover only the sites assigned to you.</Notice>}
      {stats.error ? <Card><ErrorState message={stats.error} onRetry={stats.reload} /></Card> : <OrganizationOverview stats={stats.data} onOpenSite={onOpenSite} />}
    </>
  );
}

export function OrgSitesPage() {
  const { hasPermission } = useAuth();
  const { context, reload } = useWorkspace();
  return (
    <WithOrganization>
      {(org) => (
        <>
          <PageHeader title="Sites" subtitle="Your hotspot locations. Each site has its own NAS devices, wallet and login-method overrides." />
          <SitesPanel organizationId={org.id} canWrite={hasPermission(P.SitesWrite) && !!context?.hasAllSites} onChanged={reload} />
        </>
      )}
    </WithOrganization>
  );
}

export function NasPage() {
  const { hasPermission } = useAuth();
  return (
    <WithOrganization>
      {(org) => (
        <>
          <PageHeader title="NAS devices" subtitle="MikroTik routers that authenticate hotspot users against the NXT RADIUS server." />
          <NasPanel organizationId={org.id} canWrite={hasPermission(P.NasWrite)} />
        </>
      )}
    </WithOrganization>
  );
}

export function PackagesPage() {
  const { hasPermission } = useAuth();
  return (
    <WithOrganization>
      {(org) => (
        <>
          <PageHeader title="Internet packages & bandwidth" subtitle="Speed limits, data quotas, validity and device limits for your subscribers." />
          <PackagesPanel organizationId={org.id} canWrite={hasPermission(P.PackagesWrite)} />
        </>
      )}
    </WithOrganization>
  );
}

export function SubscribersPage() {
  const { hasPermission } = useAuth();
  return (
    <WithOrganization>
      {(org) => (
        <>
          <PageHeader title={<span className="d-inline-flex align-items-center gap-2"><FiUsers className="text-primary" /> Subscribers</span>} subtitle={`Hotspot users of ${org.name}, organized by the ${org.organizationTypeName.toLowerCase()} workflow.`} />
          <SubscribersPanel organizationId={org.id} organizationTypeName={org.organizationTypeName} canWrite={hasPermission(P.SubscribersWrite)} />
        </>
      )}
    </WithOrganization>
  );
}

export function GroupsPage() {
  const { hasPermission } = useAuth();
  return (
    <WithOrganization>
      {(org) => {
        const n = org.organizationTypeName.toLowerCase();
        const def = /school|college|university/.test(n) ? 'Class' : /hotel|resort/.test(n) ? 'Floor' : 'Department';
        return (
          <>
            <PageHeader title="Groups & policies" subtitle="Organize subscribers and apply a default package per group." />
            <GroupsPanel organizationId={org.id} canWrite={hasPermission(P.SubscribersWrite)} defaultType={def} />
          </>
        );
      }}
    </WithOrganization>
  );
}

export function AuthenticationPage() {
  const { hasPermission } = useAuth();
  return (
    <WithOrganization>
      {(org) => (
        <>
          <PageHeader title="Authentication" subtitle="Captive-portal login methods, OTP rules and session defaults." />
          <div className="d-grid gap-3">
            <LoginMethodsPanel organizationId={org.id} canEdit={hasPermission(P.SettingsWrite)} />
            <SettingsPanel organizationId={org.id} canEdit={hasPermission(P.SettingsWrite)} />
          </div>
        </>
      )}
    </WithOrganization>
  );
}

export function SmsPage() {
  const { hasPermission } = useAuth();
  return (
    <WithOrganization>
      {(org) => (
        <>
          <PageHeader title="SMS provider" subtitle="Send OTP codes from your own sender ID. API keys are encrypted and never displayed." />
          <SmsPanel organizationId={org.id} canEdit={hasPermission(P.SettingsWrite)} />
        </>
      )}
    </WithOrganization>
  );
}

export function BrandingPage() {
  const { hasPermission } = useAuth();
  const { reload } = useWorkspace();
  return (
    <WithOrganization>
      {(org) => (
        <>
          <PageHeader title="Organization & branding" subtitle="Your profile and how the captive portal looks to visitors." />
          <div className="d-grid gap-3">
            <BrandingPanel organizationId={org.id} organizationName={org.name} canEdit={hasPermission(P.SettingsWrite)} />
            <ProfilePanel organizationId={org.id} canEdit={hasPermission(P.SettingsWrite)} onSaved={reload} />
          </div>
        </>
      )}
    </WithOrganization>
  );
}
