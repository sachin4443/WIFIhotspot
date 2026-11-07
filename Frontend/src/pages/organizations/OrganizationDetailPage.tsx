import { useState } from 'react';
import { useParams, useSearchParams } from 'react-router';
import { FiCheckCircle, FiPauseCircle, FiXCircle } from 'react-icons/fi';
import { dashboardApi, organizationsApi, referenceApi, sitesApi } from '../../api/platform';
import { getErrorMessage } from '../../api/errors';
import { useAuth } from '../../auth/useAuth';
import { PageHeader } from '../../components/common/PageHeader';
import { StatusBadge } from '../../components/ui/Badge';
import { Card, CardBody, CardHeader } from '../../components/ui/Card';
import { ErrorState, LoadingBlock } from '../../components/ui/States';
import { Tabs } from '../../components/ui/Tabs';
import { useConfirm } from '../../components/ui/useConfirm';
import { useApi } from '../../hooks/useApi';
import { AdminUsersPanel } from '../../panels/AdminUsersPanel';
import { BrandingPanel } from '../../panels/BrandingPanel';
import { LoginMethodsPanel } from '../../panels/LoginMethodsPanel';
import { NasPanel } from '../../panels/NasPanel';
import { PackagesPanel } from '../../panels/PackagesPanel';
import { SubscribersPanel } from '../../panels/SubscribersPanel';
import { ProfilePanel } from '../../panels/ProfilePanel';
import { SettingsPanel } from '../../panels/SettingsPanel';
import { SitesPanel } from '../../panels/SitesPanel';
import { SmsPanel } from '../../panels/SmsPanel';
import { WalletPanel } from '../../panels/WalletPanel';
import { useToast } from '../../toast/useToast';
import type { OrganizationStatus } from '../../types/models';
import { Permissions } from '../../types/permissions';
import { formatDate, formatMoney } from '../../utils/format';
import { OrganizationOverview } from './OrganizationOverview';

type TabKey = 'overview' | 'profile' | 'sites' | 'nas' | 'packages' | 'subscribers' | 'admins' | 'login' | 'sms' | 'settings' | 'branding' | 'wallet';

export function OrganizationDetailPage() {
  const { id = '' } = useParams();
  const [params, setParams] = useSearchParams();
  const tab = (params.get('tab') as TabKey) || 'overview';
  const setTab = (t: TabKey) => setParams((p) => { p.set('tab', t); return p; }, { replace: true });
  const { hasPermission } = useAuth();
  const toast = useToast();
  const confirm = useConfirm();
  const org = useApi((s) => organizationsApi.get(id, s), [id]);
  const stats = useApi((s) => dashboardApi.organization(id, s), [id]);
  const plans = useApi((s) => referenceApi.subscriptionPlans(s), []);
  const sites = useApi((s) => sitesApi.list(id, s), [id]);
  const [walletSite, setWalletSite] = useState('');
  const [busy, setBusy] = useState(false);

  if (org.error) return <Card><ErrorState message={org.error} onRetry={org.reload} /></Card>;
  if (!org.data) return <LoadingBlock />;
  const o = org.data;
  const canWriteOrg = hasPermission(Permissions.OrganizationsWrite);
  const canSettings = hasPermission(Permissions.SettingsWrite);
  const selectedWalletSite = walletSite || o.primarySiteId;

  const refresh = () => { org.reload(); stats.reload(); sites.reload(); };

  const changeStatus = async (status: OrganizationStatus) => {
    const verbs: Record<OrganizationStatus, string> = { Active: 'Activate', Suspended: 'Suspend', Inactive: 'Deactivate', Pending: 'Mark pending' };
    const consequences: Record<OrganizationStatus, string> = {
      Active: 'Administrators can sign in and hotspots accept logins.',
      Suspended: 'Administrators are blocked from signing in and no new sites can be added.',
      Inactive: 'The organization is switched off. Its administrators cannot sign in.',
      Pending: 'The organization waits for activation.',
    };
    if (!(await confirm({ title: `${verbs[status]} ${o.name}?`, message: consequences[status], confirmLabel: verbs[status], tone: status === 'Active' ? 'primary' : 'danger' }))) return;
    setBusy(true);
    try {
      await organizationsApi.setStatus(o.id, status);
      toast.success(`Organization is now ${status.toLowerCase()}.`);
      refresh();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const assignPlan = async (planId: string) => {
    setBusy(true);
    try {
      await organizationsApi.assignPlan(o.id, planId || null);
      toast.success('Subscription plan updated.');
      refresh();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <PageHeader
        breadcrumbs={[{ label: 'Organizations', to: '/organizations' }, { label: o.name }]}
        title={<span className="d-inline-flex flex-wrap align-items-center gap-2">{o.name} <StatusBadge status={o.status} /></span>}
        subtitle={`${o.organizationTypeName} · registered ${formatDate(o.createdAt, false)} · admin ${o.adminUsername ?? '—'}`}
        actions={canWriteOrg && (
          <>
            {o.status !== 'Active' && <button type="button" className="btn btn-primary btn-icon" disabled={busy} onClick={() => changeStatus('Active')}><FiCheckCircle /> Activate</button>}
            {o.status === 'Active' && <button type="button" className="btn btn-light btn-icon" disabled={busy} onClick={() => changeStatus('Suspended')}><FiPauseCircle /> Suspend</button>}
            {o.status !== 'Inactive' && <button type="button" className="btn btn-soft-danger btn-icon" disabled={busy} onClick={() => changeStatus('Inactive')}><FiXCircle /> Deactivate</button>}
          </>
        )}
      />
      <Tabs<TabKey>
        active={tab}
        onChange={setTab}
        items={[
          { key: 'overview', label: 'Overview' },
          { key: 'profile', label: 'Profile' },
          { key: 'sites', label: 'Sites' },
          { key: 'nas', label: 'NAS devices', hidden: !hasPermission(Permissions.NasRead) },
          { key: 'packages', label: 'Packages', hidden: !hasPermission(Permissions.PackagesRead) },
          { key: 'subscribers', label: 'Subscribers', hidden: !hasPermission(Permissions.SubscribersRead) },
          { key: 'admins', label: 'Administrators', hidden: !hasPermission(Permissions.AdminUsersRead) },
          { key: 'login', label: 'Login methods' },
          { key: 'sms', label: 'SMS' },
          { key: 'settings', label: 'OTP & sessions' },
          { key: 'branding', label: 'Branding' },
          { key: 'wallet', label: 'Wallet', hidden: !hasPermission(Permissions.WalletRead) },
        ]}
      />

      {tab === 'overview' && (
        <div className="d-grid gap-3">
          <Card>
            <CardHeader title="Subscription" subtitle="The plan limits sites, NAS devices and subscribers." />
            <CardBody className="d-flex flex-wrap align-items-end gap-3">
              <div style={{ minWidth: 240 }} className="flex-grow-1 flex-md-grow-0">
                <label className="form-label" htmlFor="plan-select">Plan</label>
                <select id="plan-select" className="form-select" value={o.subscriptionPlanId ?? ''} disabled={!canWriteOrg || busy} onChange={(e) => assignPlan(e.target.value)}>
                  <option value="">No plan (unlimited)</option>
                  {plans.data?.map((p) => <option key={p.id} value={p.id}>{p.name} · {formatMoney(p.monthlyPrice)}/month</option>)}
                </select>
              </div>
              <div className="fs-sm text-muted-2">Contact: {o.contactPerson} · {o.mobileNumber} · {o.email}</div>
            </CardBody>
          </Card>
          {stats.error ? <Card><ErrorState message={stats.error} onRetry={stats.reload} /></Card> : <OrganizationOverview stats={stats.data} onOpenSite={(siteId) => { setWalletSite(siteId); setTab('wallet'); }} />}
        </div>
      )}
      {tab === 'profile' && <ProfilePanel organizationId={o.id} canEdit={canSettings} onSaved={org.reload} />}
      {tab === 'sites' && <SitesPanel organizationId={o.id} canWrite={hasPermission(Permissions.SitesWrite)} onChanged={refresh} />}
      {tab === 'nas' && <NasPanel organizationId={o.id} canWrite={hasPermission(Permissions.NasWrite)} />}
      {tab === 'packages' && <PackagesPanel organizationId={o.id} canWrite={hasPermission(Permissions.PackagesWrite)} />}
      {tab === 'subscribers' && <SubscribersPanel organizationId={o.id} organizationTypeName={o.organizationTypeName} canWrite={hasPermission(Permissions.SubscribersWrite)} />}
      {tab === 'admins' && <AdminUsersPanel organizationId={o.id} canWrite={hasPermission(Permissions.AdminUsersWrite)} />}
      {tab === 'login' && <LoginMethodsPanel organizationId={o.id} canEdit={canSettings} />}
      {tab === 'sms' && <SmsPanel organizationId={o.id} canEdit={canSettings} />}
      {tab === 'settings' && <SettingsPanel organizationId={o.id} canEdit={canSettings} />}
      {tab === 'branding' && <BrandingPanel organizationId={o.id} organizationName={o.name} canEdit={canSettings} />}
      {tab === 'wallet' && (
        <div className="d-grid gap-3">
          <div style={{ maxWidth: 360 }}>
            <label className="form-label" htmlFor="wallet-site">Site</label>
            <select id="wallet-site" className="form-select" value={selectedWalletSite} onChange={(e) => setWalletSite(e.target.value)}>
              {sites.data?.map((s) => <option key={s.id} value={s.id}>{s.name}{s.isPrimary ? ' (primary)' : ''}</option>)}
            </select>
          </div>
          <WalletPanel key={selectedWalletSite} siteId={selectedWalletSite} siteName={sites.data?.find((s) => s.id === selectedWalletSite)?.name} />
        </div>
      )}
    </>
  );
}
