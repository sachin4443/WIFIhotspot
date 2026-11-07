import { useEffect, useState } from 'react';
import { FiPower, FiRefreshCw, FiWifi } from 'react-icons/fi';
import { getErrorMessage } from '../../api/errors';
import { sessionsApi } from '../../api/sessions';
import { useAuth } from '../../auth/useAuth';
import { PageHeader } from '../../components/common/PageHeader';
import { Badge, StatusBadge } from '../../components/ui/Badge';
import { Card } from '../../components/ui/Card';
import { DataTable } from '../../components/ui/DataTable';
import { Switch } from '../../components/ui/Field';
import { Pagination } from '../../components/ui/Pagination';
import { SearchInput } from '../../components/ui/SearchInput';
import { StatCard } from '../../components/ui/StatCard';
import { useConfirm } from '../../components/ui/useConfirm';
import { useApi } from '../../hooks/useApi';
import { useNow } from '../../hooks/useNow';
import { useToast } from '../../toast/useToast';
import { Permissions } from '../../types/permissions';
import type { RadiusSessionRow } from '../../types/sessions';
import { formatBytes, formatDate, formatSeconds } from '../../utils/format';
import { ScopeFilters } from './ScopeFilters';

const PAGE_SIZE = 25;

export function ActiveSessionsPage() {
  const { hasPermission, user } = useAuth();
  const toast = useToast();
  const confirm = useConfirm();
  const [orgId, setOrgId] = useState('');
  const [siteId, setSiteId] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [auto, setAuto] = useState(true);
  const list = useApi((s) => sessionsApi.list({ organizationId: orgId, siteId, search, status: 'Active', page, pageSize: PAGE_SIZE }, s), [orgId, siteId, search, page]);
  const now = useNow(1000);

  useEffect(() => {
    if (!auto) return;
    const t = window.setInterval(list.reload, 15_000);
    return () => window.clearInterval(t);
  }, [auto, list.reload]);

  const canDisconnect = hasPermission(Permissions.SessionsDisconnect);
  const rows = list.data?.items ?? [];
  const traffic = rows.reduce((a, r) => a + r.inputBytes + r.outputBytes, 0);

  const disconnect = async (s: RadiusSessionRow) => {
    if (!(await confirm({ title: 'Disconnect session?', message: `${s.subscriberName ?? s.username} (${s.macAddress ?? 'unknown device'}) will be disconnected by ${s.nasName}. They can sign in again unless their account is suspended.`, confirmLabel: 'Disconnect' }))) return;
    try {
      await sessionsApi.disconnect(s.id);
      toast.info('Disconnect requested. The RADIUS server is sending it to the router.', { title: 'Disconnect queued' });
      list.reload();
    } catch (err) {
      toast.error(getErrorMessage(err));
    }
  };

  const live = (s: RadiusSessionRow) => s.sessionTimeSeconds + Math.max(0, Math.floor((now - new Date(s.lastUpdateAt).getTime()) / 1000));

  return (
    <>
      <PageHeader title="Active sessions" subtitle="Devices currently online, as reported by RADIUS accounting (Start and Interim-Update)."
        actions={<button type="button" className="btn btn-light btn-icon" onClick={list.reload} disabled={list.loading}><FiRefreshCw className={list.loading ? 'nxt-spin' : ''} /> Refresh</button>} />
      <div className="row g-3 mb-3">
        <div className="col-6 col-xl-3"><StatCard label="Online now" icon={<FiWifi />} tone="green" loading={!list.data} value={list.data?.totalCount ?? 0} /></div>
        <div className="col-6 col-xl-3"><StatCard label="Traffic (this page)" icon={<FiRefreshCw />} tone="indigo" loading={!list.data} value={formatBytes(traffic)} hint="Since each session started" /></div>
      </div>
      <Card>
        <div className="nxt-toolbar">
          <SearchInput value={search} onChange={(v) => { setSearch(v); setPage(1); }} placeholder="Search user, MAC, IP or room" />
          <ScopeFilters orgId={orgId} onOrgChange={(v) => { setOrgId(v); setPage(1); }} siteId={siteId} onSiteChange={(v) => { setSiteId(v); setPage(1); }} />
          <div className="ms-md-auto"><Switch label="Auto-refresh (15 s)" checked={auto} onChange={setAuto} /></div>
        </div>
        <DataTable
          rows={list.data?.items}
          loading={list.loading}
          error={list.error}
          onRetry={list.reload}
          rowKey={(s) => s.id}
          empty={{ title: 'No active sessions', icon: <FiWifi />, text: 'Sessions appear here when routers send RADIUS accounting Start/Interim-Update packets.' }}
          mobileBadge={(s) => (s.pendingDisconnect === 'Pending' ? <Badge tone="warning">Disconnecting</Badge> : <StatusBadge status={s.status} />)}
          columns={[
            { key: 'user', header: 'User', render: (s) => <div><div className="nxt-cell-primary">{s.subscriberName ?? s.username}</div><div className="nxt-cell-secondary">{s.subscriberType ?? 'Unlinked'} · {s.username}</div></div> },
            ...(user?.isPlatformAdmin ? [{ key: 'org', header: 'Organization', render: (s: RadiusSessionRow) => s.organizationName ?? '—' }] : []),
            { key: 'where', header: 'NAS / site', render: (s) => <div><div>{s.nasName}</div><div className="nxt-cell-secondary">{s.siteName}</div></div> },
            { key: 'device', header: 'Device', render: (s) => <div className="font-monospace fs-sm"><div>{s.framedIpAddress ?? '—'}</div><div className="nxt-cell-secondary">{s.macAddress ?? ''}</div></div> },
            { key: 'time', header: 'Online for', render: (s) => <div><div className="font-tabular">{formatSeconds(live(s))}</div><div className="nxt-cell-secondary">since {formatDate(s.startedAt)}</div></div> },
            { key: 'traffic', header: 'Download / upload', render: (s) => <span className="text-nowrap font-tabular">{formatBytes(s.outputBytes)} / {formatBytes(s.inputBytes)}</span> },
            { key: 'status', header: 'Status', hideOnMobile: true, render: (s) => (s.pendingDisconnect === 'Pending' ? <Badge tone="warning">Disconnecting</Badge> : <StatusBadge status={s.status} />) },
          ]}
          actions={canDisconnect ? (s) => (
            <button type="button" className="btn btn-soft-danger btn-sm btn-icon" disabled={s.pendingDisconnect === 'Pending'} onClick={() => disconnect(s)}><FiPower /> Disconnect</button>
          ) : undefined}
        />
        {list.data && <Pagination page={page} pageSize={PAGE_SIZE} totalCount={list.data.totalCount} onPageChange={setPage} />}
      </Card>
    </>
  );
}
