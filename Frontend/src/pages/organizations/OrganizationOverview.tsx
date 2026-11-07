import { FiActivity, FiCreditCard, FiMapPin, FiRadio, FiUsers, FiWifi } from 'react-icons/fi';
import { BarChart } from '../../components/charts/Charts';
import { ActiveBadge } from '../../components/ui/Badge';
import { Card, CardBody, CardHeader } from '../../components/ui/Card';
import { DataTable } from '../../components/ui/DataTable';
import { StatCard } from '../../components/ui/StatCard';
import { EmptyState } from '../../components/ui/States';
import type { OrganizationStatistics } from '../../types/models';
import { formatMoney, formatNumber, formatRelative, humanize } from '../../utils/format';

const limit = (used: number, max: number | null) => (max == null ? `${formatNumber(used)} (no limit)` : `${formatNumber(used)} of ${formatNumber(max)}`);

/** Statistics block shared by the Super Admin organization page and the organization's own dashboard. */
export function OrganizationOverview({ stats, onOpenSite }: { stats: OrganizationStatistics | undefined; onOpenSite?: (siteId: string) => void }) {
  const s = stats;
  return (
    <div className="d-grid gap-3">
      <div className="row g-3">
        <div className="col-6 col-xl-3"><StatCard label="Sites" icon={<FiMapPin />} tone="blue" loading={!s} value={formatNumber(s?.sites.active)} hint={s ? `Active · ${limit(s.sites.total, s.maxSites)}` : undefined} /></div>
        <div className="col-6 col-xl-3"><StatCard label="Subscribers" icon={<FiUsers />} tone="green" loading={!s} value={formatNumber(s?.hotspot.subscribers)} hint={s ? `${formatNumber(s.hotspot.activeSubscribers)} active · limit ${s.maxUsers?.toLocaleString() ?? 'none'}` : undefined} /></div>
        <div className="col-6 col-xl-3"><StatCard label="Active sessions" icon={<FiWifi />} tone="cyan" loading={!s} value={formatNumber(s?.hotspot.activeSessions)} hint="From RADIUS accounting" /></div>
        <div className="col-6 col-xl-3"><StatCard label="Wallet balance" icon={<FiCreditCard />} tone="amber" loading={!s} value={formatMoney(s?.wallet.totalBalance, s?.wallet.currency)} hint={s ? `${s.wallet.walletCount} wallets · ${s.wallet.lowBalanceWallets} low` : undefined} /></div>
        <div className="col-6 col-xl-3"><StatCard label="NAS devices" icon={<FiRadio />} tone="slate" loading={!s} value={formatNumber(s?.hotspot.nasDevices)} hint={s ? limit(s.hotspot.nasDevices, s.maxNasDevices) : undefined} /></div>
        <div className="col-6 col-xl-3"><StatCard label="Administrators" icon={<FiUsers />} tone="indigo" loading={!s} value={formatNumber(s?.adminUsers.active)} hint={s ? `${s.adminUsers.total} accounts` : undefined} /></div>
        <div className="col-6 col-xl-3"><StatCard label="Internet packages" icon={<FiActivity />} tone="rose" loading={!s} value={formatNumber(s?.hotspot.packages)} /></div>
        <div className="col-6 col-xl-3"><StatCard label="Credits (30 days)" icon={<FiCreditCard />} tone="green" loading={!s} value={formatMoney(s?.wallet.creditsLast30Days, s?.wallet.currency)} hint={s ? `Debits ${formatMoney(s.wallet.debitsLast30Days, s.wallet.currency)}` : undefined} /></div>
      </div>
      <div className="row g-3">
        <div className="col-12 col-xl-7">
          <Card className="h-100">
            <CardHeader title="Sites" subtitle="Per-site footprint" />
            <DataTable
              rows={s?.siteBreakdown}
              loading={!s}
              rowKey={(x) => x.siteId}
              onRowClick={onOpenSite ? (x) => onOpenSite(x.siteId) : undefined}
              empty={{ title: 'No sites you can access', icon: <FiMapPin /> }}
              mobileBadge={(x) => <ActiveBadge active={x.isActive} />}
              columns={[
                { key: 'site', header: 'Site', render: (x) => <div><div className="nxt-cell-primary">{x.name}</div><div className="nxt-cell-secondary">{x.code}</div></div> },
                { key: 'nas', header: 'NAS', render: (x) => formatNumber(x.nasDevices) },
                { key: 'subs', header: 'Subscribers', render: (x) => formatNumber(x.subscribers) },
                { key: 'sessions', header: 'Sessions', render: (x) => formatNumber(x.activeSessions) },
                { key: 'wallet', header: 'Wallet', align: 'end', render: (x) => <span className="font-tabular">{formatMoney(x.walletBalance)}</span> },
              ]}
            />
          </Card>
        </div>
        <div className="col-12 col-xl-5">
          <Card className="h-100">
            <CardHeader title={s && s.siteBreakdown.length > 1 ? 'Wallet balance by site' : 'Recent activity'} />
            {s && s.siteBreakdown.length > 1 ? (
              <CardBody>
                <BarChart horizontal small ariaLabel="Wallet balance by site" labels={s.siteBreakdown.map((x) => x.name)} series={[{ label: 'Balance', data: s.siteBreakdown.map((x) => x.walletBalance) }]} valueFormatter={(v) => formatMoney(v)} />
              </CardBody>
            ) : s && s.recentActivity.length === 0 ? <EmptyState title="No activity to show" /> : (
              <ul className="list-unstyled mb-0">
                {(s?.recentActivity ?? []).map((a) => (
                  <li key={a.id} className="d-flex gap-3 px-3 py-2 border-bottom">
                    <div className="min-w-0 flex-grow-1"><div className="fs-sm fw-medium">{humanize(a.action)}</div><div className="fs-xs text-muted-2 text-truncate">{a.details ?? a.entityType}</div></div>
                    <span className="fs-xs text-muted-2 text-nowrap">{formatRelative(a.createdAt)}</span>
                  </li>
                ))}
              </ul>
            )}
          </Card>
        </div>
      </div>
    </div>
  );
}
