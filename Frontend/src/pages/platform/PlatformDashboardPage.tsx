import { Link, useNavigate } from 'react-router';
import { FiActivity, FiBriefcase, FiCreditCard, FiLayers, FiMapPin, FiPlus, FiRefreshCw, FiRadio, FiUsers, FiWifi } from 'react-icons/fi';
import { dashboardApi } from '../../api/platform';
import { useAuth } from '../../auth/useAuth';
import { BarChart } from '../../components/charts/Charts';
import { PageHeader } from '../../components/common/PageHeader';
import { StatusBadge } from '../../components/ui/Badge';
import { Card, CardBody, CardHeader } from '../../components/ui/Card';
import { DataTable } from '../../components/ui/DataTable';
import { StatCard } from '../../components/ui/StatCard';
import { EmptyState, ErrorState } from '../../components/ui/States';
import { useApi } from '../../hooks/useApi';
import { Permissions } from '../../types/permissions';
import { formatDate, formatMoney, formatNumber, formatRelative, humanize } from '../../utils/format';

const monthLabel = (m: string) => new Date(`${m}-01T00:00:00Z`).toLocaleString(undefined, { month: 'short', year: '2-digit', timeZone: 'UTC' });

export function PlatformDashboardPage() {
  const { user, hasPermission } = useAuth();
  const navigate = useNavigate();
  const { data: d, loading, error, reload } = useApi((s) => dashboardApi.platform(s), []);

  if (error) return <Card><ErrorState message={error} onRetry={reload} /></Card>;
  const active = d?.organizationsByStatus.find((s) => s.label === 'Active')?.count ?? 0;
  const pending = d?.organizationsByStatus.find((s) => s.label === 'Pending')?.count ?? 0;

  return (
    <>
      <PageHeader
        title={`Welcome back, ${user?.fullName.split(' ')[0] || user?.username}`}
        subtitle="Platform overview across every organization, site and hotspot."
        actions={
          <>
            <button type="button" className="btn btn-light btn-icon" onClick={reload} disabled={loading}><FiRefreshCw className={loading ? 'nxt-spin' : ''} /> Refresh</button>
            {hasPermission(Permissions.OrganizationsWrite) && <Link to="/organizations/new" className="btn btn-primary btn-icon"><FiPlus /> Register organization</Link>}
          </>
        }
      />

      <div className="row g-3 mb-3">
        <div className="col-6 col-xl-3"><StatCard label="Organizations" icon={<FiBriefcase />} tone="indigo" loading={!d} value={formatNumber(d?.totalOrganizations)} hint={`${active} active · ${pending} pending`} /></div>
        <div className="col-6 col-xl-3"><StatCard label="Active sites" icon={<FiMapPin />} tone="blue" loading={!d} value={formatNumber(d?.sites.active)} hint={`${formatNumber(d?.sites.total)} sites in total`} /></div>
        <div className="col-6 col-xl-3"><StatCard label="Registered subscribers" icon={<FiUsers />} tone="green" loading={!d} value={formatNumber(d?.hotspot.subscribers)} hint={`${formatNumber(d?.hotspot.activeSubscribers)} active`} /></div>
        <div className="col-6 col-xl-3"><StatCard label="Wallet balance (all sites)" icon={<FiCreditCard />} tone="amber" loading={!d} value={formatMoney(d?.wallet.totalBalance, d?.wallet.currency)} hint={d ? `${d.wallet.lowBalanceWallets} low-balance wallets` : undefined} /></div>
        <div className="col-6 col-xl-3"><StatCard label="Active sessions" icon={<FiWifi />} tone="cyan" loading={!d} value={formatNumber(d?.hotspot.activeSessions)} hint="Reported by RADIUS accounting" /></div>
        <div className="col-6 col-xl-3"><StatCard label="NAS devices" icon={<FiRadio />} tone="slate" loading={!d} value={formatNumber(d?.hotspot.nasDevices)} hint={`${formatNumber(d?.hotspot.activeNasDevices)} active`} /></div>
        <div className="col-6 col-xl-3"><StatCard label="Subscription plans" icon={<FiLayers />} tone="rose" loading={!d} value={formatNumber(d?.subscriptionPlans.active)} hint={`${formatNumber(d?.subscriptionPlans.total)} defined`} /></div>
        <div className="col-6 col-xl-3"><StatCard label="Administrators" icon={<FiUsers />} tone="indigo" loading={!d} value={formatNumber(d?.adminUsers.active)} hint={`${formatNumber(d?.adminUsers.total)} accounts`} /></div>
      </div>

      <div className="row g-3 mb-3">
        <div className="col-12 col-xl-7">
          <Card className="h-100">
            <CardHeader title="Organization registrations" subtitle="New organizations per month, last 6 months" />
            <CardBody>
              {d ? <BarChart ariaLabel="Organizations registered per month" labels={d.registrationsByMonth.map((m) => monthLabel(m.month))} series={[{ label: 'Registrations', data: d.registrationsByMonth.map((m) => m.count) }]} /> : <div className="nxt-chart nxt-skeleton" />}
            </CardBody>
          </Card>
        </div>
        <div className="col-12 col-xl-5">
          <Card className="h-100">
            <CardHeader title="Organizations by type" subtitle="Business verticals on the platform" />
            <CardBody>
              {!d ? <div className="nxt-chart nxt-skeleton" /> : d.organizationsByType.length === 0
                ? <EmptyState title="No organizations yet" text="Register the first organization to see the mix of venue types." />
                : <BarChart horizontal ariaLabel="Organizations by type" labels={d.organizationsByType.map((t) => t.label)} series={[{ label: 'Organizations', data: d.organizationsByType.map((t) => t.count) }]} />}
            </CardBody>
          </Card>
        </div>
      </div>

      <div className="row g-3">
        <div className="col-12 col-xl-7">
          <Card className="h-100">
            <CardHeader title="Recently registered" actions={<Link to="/organizations" className="btn btn-light btn-sm">View all</Link>} />
            <DataTable
              rows={d?.recentOrganizations}
              loading={loading}
              rowKey={(o) => o.id}
              onRowClick={(o) => navigate(`/organizations/${o.id}`)}
              empty={{ title: 'No organizations yet', icon: <FiBriefcase /> }}
              mobileBadge={(o) => <StatusBadge status={o.status} />}
              columns={[
                { key: 'name', header: 'Organization', render: (o) => <div><div className="nxt-cell-primary">{o.name}</div><div className="nxt-cell-secondary">{o.organizationTypeName}</div></div> },
                { key: 'sites', header: 'Sites', render: (o) => formatNumber(o.siteCount) },
                { key: 'status', header: 'Status', hideOnMobile: true, render: (o) => <StatusBadge status={o.status} /> },
                { key: 'created', header: 'Registered', render: (o) => formatDate(o.createdAt, false) },
              ]}
            />
          </Card>
        </div>
        <div className="col-12 col-xl-5">
          <Card className="h-100">
            <CardHeader title="Recent activity" icon={<FiActivity />} actions={hasPermission(Permissions.AuditRead) && <Link to="/audit-logs" className="btn btn-light btn-sm">Audit log</Link>} />
            {d && d.recentActivity.length === 0 ? <EmptyState title="No activity yet" /> : (
              <ul className="list-unstyled mb-0">
                {(d?.recentActivity ?? []).map((a) => (
                  <li key={a.id} className="d-flex gap-3 px-3 py-2 border-bottom">
                    <span className="nxt-stat-icon tone-slate" style={{ width: 32, height: 32, fontSize: '0.9rem' }}><FiActivity /></span>
                    <div className="min-w-0 flex-grow-1">
                      <div className="fs-sm fw-medium">{humanize(a.action)}</div>
                      <div className="fs-xs text-muted-2 text-truncate">{a.entityType}{a.details ? ` · ${a.details}` : ''}</div>
                    </div>
                    <span className="fs-xs text-muted-2 text-nowrap">{formatRelative(a.createdAt)}</span>
                  </li>
                ))}
              </ul>
            )}
          </Card>
        </div>
      </div>
    </>
  );
}
