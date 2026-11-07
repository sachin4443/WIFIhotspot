import { useEffect, useState } from 'react';
import { FiAlertTriangle, FiCheckCircle, FiRadio, FiRefreshCw, FiServer, FiSlash, FiXCircle } from 'react-icons/fi';
import { radiusApi } from '../../api/sessions';
import { useAuth } from '../../auth/useAuth';
import { OrganizationSelect } from '../../components/common/OrganizationSelect';
import { PageHeader } from '../../components/common/PageHeader';
import { ActiveBadge, Badge } from '../../components/ui/Badge';
import { Card, CardBody, CardHeader } from '../../components/ui/Card';
import { DataTable } from '../../components/ui/DataTable';
import { Pagination } from '../../components/ui/Pagination';
import { SearchInput } from '../../components/ui/SearchInput';
import { StatCard } from '../../components/ui/StatCard';
import { EmptyState, Notice } from '../../components/ui/States';
import { useApi } from '../../hooks/useApi';
import type { RadiusRequestLog, RadiusResult } from '../../types/sessions';
import { formatDate, formatNumber, formatRelative } from '../../utils/format';

const RESULT_TONE: Record<RadiusResult, 'success' | 'danger' | 'warning' | 'info' | 'neutral'> = {
  Accept: 'success', Reject: 'danger', Dropped: 'warning', Accounted: 'info', Ack: 'success', Nak: 'danger', Timeout: 'warning',
};

/** RADIUS server health (heartbeat), 24-hour counters, NAS health and the request log. Nothing here is simulated. */
export function RadiusMonitorPage() {
  const { user } = useAuth();
  const [orgId, setOrgId] = useState('');
  const [result, setResult] = useState<RadiusResult | ''>('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const overview = useApi((s) => radiusApi.overview(orgId || undefined, s), [orgId]);
  const logs = useApi((s) => radiusApi.requests({ organizationId: orgId, result, search, page, pageSize: 50 }, s), [orgId, result, search, page]);
  const o = overview.data;

  useEffect(() => {
    const t = window.setInterval(() => { overview.reload(); logs.reload(); }, 20_000);
    return () => window.clearInterval(t);
  }, [overview.reload, logs.reload]);

  return (
    <>
      <PageHeader title="RADIUS monitor" subtitle="Live status of the NXT RADIUS server, router activity and authentication results."
        actions={
          <>
            {user?.isPlatformAdmin && <div style={{ minWidth: 220 }}><OrganizationSelect value={orgId} onChange={(v) => { setOrgId(v); setPage(1); }} /></div>}
            <button type="button" className="btn btn-light btn-icon" onClick={() => { overview.reload(); logs.reload(); }}><FiRefreshCw className={overview.loading ? 'nxt-spin' : ''} /> Refresh</button>
          </>
        } />

      {o && !o.anyServerOnline && (
        <Notice tone="danger" className="mb-3">
          <strong>No RADIUS server is reporting.</strong> No heartbeat has been received in the last minute, so routers cannot authenticate users.
          Start the <code>NxtHotspotRadius</code> service (or <code>dotnet run --project RadiusServer/NxtHotspot.RadiusServer</code>) with the same database and encryption key as the API.
        </Notice>
      )}

      <div className="row g-3 mb-3">
        <div className="col-6 col-xl-3"><StatCard label="RADIUS server" icon={o?.anyServerOnline ? <FiCheckCircle /> : <FiXCircle />} tone={o?.anyServerOnline ? 'green' : 'rose'} loading={!o}
          value={o?.anyServerOnline ? 'Online' : 'Offline'} hint={o ? `${o.servers.filter((s) => s.isOnline).length} of ${o.servers.length} instances` : undefined} /></div>
        <div className="col-6 col-xl-3"><StatCard label="Accepted (24 h)" icon={<FiCheckCircle />} tone="green" loading={!o} value={formatNumber(o?.acceptsLast24h)} hint={o ? `${formatNumber(o.requestsLast24h)} requests logged` : undefined} /></div>
        <div className="col-6 col-xl-3"><StatCard label="Rejected (24 h)" icon={<FiSlash />} tone="rose" loading={!o} value={formatNumber(o?.rejectsLast24h)} /></div>
        <div className="col-6 col-xl-3"><StatCard label="Dropped (24 h)" icon={<FiAlertTriangle />} tone="amber" loading={!o} value={formatNumber(o?.droppedLast24h)} hint="Unknown NAS or bad secret" /></div>
      </div>

      <div className="row g-3 mb-3">
        <div className="col-12 col-xl-7">
          <Card className="h-100">
            <CardHeader title="Server instances" icon={<FiServer />} />
            <DataTable
              rows={o?.servers}
              loading={!o}
              rowKey={(s) => s.instanceId}
              empty={{ title: 'No RADIUS server has ever reported', text: 'Instances appear after the RADIUS service starts and writes its first heartbeat.' }}
              mobileBadge={(s) => <Badge tone={s.isOnline ? 'success' : 'danger'} dot>{s.isOnline ? 'Online' : 'Offline'}</Badge>}
              columns={[
                { key: 'id', header: 'Instance', render: (s) => <div><div className="nxt-cell-primary">{s.instanceId}</div><div className="nxt-cell-secondary">v{s.version}{s.hostName ? ` · ${s.hostName}` : ''} · UDP {s.authPort}/{s.acctPort}</div></div> },
                { key: 'hb', header: 'Heartbeat', render: (s) => formatRelative(s.lastHeartbeatAt) },
                { key: 'auth', header: 'Auth (accept / reject)', render: (s) => <span className="font-tabular">{formatNumber(s.authAccepts)} / {formatNumber(s.authRejects)}</span> },
                { key: 'acct', header: 'Accounting', render: (s) => formatNumber(s.acctRequests) },
                { key: 'drop', header: 'Dropped', render: (s) => formatNumber(s.dropped) },
                { key: 'st', header: 'Status', hideOnMobile: true, render: (s) => <Badge tone={s.isOnline ? 'success' : 'danger'} dot>{s.isOnline ? 'Online' : 'Offline'}</Badge> },
              ]}
            />
          </Card>
        </div>
        <div className="col-12 col-xl-5">
          <Card className="h-100">
            <CardHeader title="Top reject and drop reasons (24 h)" />
            <CardBody>
              {o && o.topRejectReasons.length === 0 ? <EmptyState title="No rejected requests" /> : (
                <ul className="list-unstyled mb-0 d-grid gap-2">
                  {o?.topRejectReasons.map((r) => (
                    <li key={r.label} className="d-flex justify-content-between gap-3 fs-sm"><span className="text-break">{r.label || 'Unspecified'}</span><Badge tone="danger">{r.count}</Badge></li>
                  ))}
                </ul>
              )}
            </CardBody>
          </Card>
        </div>
      </div>

      <Card className="mb-3">
        <CardHeader title="NAS health" subtitle="Last RADIUS packet received from each router" icon={<FiRadio />} />
        <DataTable
          rows={o?.nas}
          loading={!o}
          rowKey={(n) => n.id}
          empty={{ title: 'No NAS devices' }}
          mobileBadge={(n) => <ActiveBadge active={n.isActive} />}
          columns={[
            { key: 'n', header: 'NAS', render: (n) => <div><div className="nxt-cell-primary">{n.name}</div><div className="nxt-cell-secondary font-monospace">{n.ipAddress}</div></div> },
            ...(user?.isPlatformAdmin ? [{ key: 'o', header: 'Organization', render: (n: { organizationName: string }) => n.organizationName }] : []),
            { key: 's', header: 'Site', render: (n) => n.siteName },
            { key: 'seen', header: 'Last packet', render: (n) => (n.lastSeenAt ? formatRelative(n.lastSeenAt) : <span className="text-muted-2">Never</span>) },
            { key: 'sess', header: 'Online', render: (n) => n.activeSessions },
            { key: 'a', header: 'Status', hideOnMobile: true, render: (n) => <ActiveBadge active={n.isActive} /> },
          ]}
        />
      </Card>

      <Card>
        <CardHeader title="Request log" subtitle="Authentication, accounting and disconnect results. Passwords and OTPs are never logged." />
        <div className="nxt-toolbar">
          <SearchInput value={search} onChange={(v) => { setSearch(v); setPage(1); }} placeholder="User, MAC, IP or reason" />
          <select className="form-select" value={result} onChange={(e) => { setResult(e.target.value as RadiusResult | ''); setPage(1); }} aria-label="Result">
            <option value="">All results</option>{(['Accept', 'Reject', 'Dropped', 'Accounted', 'Ack', 'Nak', 'Timeout'] as RadiusResult[]).map((r) => <option key={r}>{r}</option>)}
          </select>
        </div>
        <DataTable
          rows={logs.data?.items}
          loading={logs.loading}
          error={logs.error}
          onRetry={logs.reload}
          rowKey={(l: RadiusRequestLog) => String(l.id)}
          empty={{ title: 'No requests logged yet' }}
          mobileBadge={(l) => <Badge tone={RESULT_TONE[l.result]}>{l.result}</Badge>}
          columns={[
            { key: 't', header: 'Time', render: (l) => <span className="text-nowrap">{formatDate(l.createdAt)}</span> },
            { key: 'k', header: 'Packet', render: (l) => l.packetKind.replace('Request', '') },
            { key: 'r', header: 'Result', hideOnMobile: true, render: (l) => <Badge tone={RESULT_TONE[l.result]}>{l.result}</Badge> },
            { key: 'u', header: 'User / device', render: (l) => <div><div>{l.username ?? '—'}</div><div className="nxt-cell-secondary font-monospace">{l.macAddress ?? ''}</div></div> },
            { key: 'n', header: 'NAS', render: (l) => <div><div>{l.nasName ?? 'Unknown'}</div><div className="nxt-cell-secondary font-monospace">{l.sourceIp}</div></div> },
            { key: 'why', header: 'Detail', render: (l) => <span className="text-muted-2 text-break">{l.reason ?? ''}</span> },
          ]}
        />
        {logs.data && <Pagination page={page} pageSize={50} totalCount={logs.data.totalCount} onPageChange={setPage} />}
      </Card>
    </>
  );
}
