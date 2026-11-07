import { useState } from 'react';
import { FiDatabase, FiDownload } from 'react-icons/fi';
import { getErrorMessage } from '../../api/errors';
import { sessionsApi } from '../../api/sessions';
import { useAuth } from '../../auth/useAuth';
import { PageHeader } from '../../components/common/PageHeader';
import { StatusBadge } from '../../components/ui/Badge';
import { Card } from '../../components/ui/Card';
import { DataTable } from '../../components/ui/DataTable';
import { Pagination } from '../../components/ui/Pagination';
import { SearchInput } from '../../components/ui/SearchInput';
import { useApi } from '../../hooks/useApi';
import { useToast } from '../../toast/useToast';
import type { RadiusSessionRow, SessionStatus } from '../../types/sessions';
import { formatBytes, formatDate, formatSeconds } from '../../utils/format';
import { ScopeFilters, daysAgo, endOfDay, isoDate, startOfDay } from './ScopeFilters';

const PAGE_SIZE = 50;

/** RADIUS accounting history: every session with start/stop, duration, traffic and terminate cause. */
export function AccountingPage() {
  const { user } = useAuth();
  const toast = useToast();
  const [orgId, setOrgId] = useState('');
  const [siteId, setSiteId] = useState('');
  const [status, setStatus] = useState<SessionStatus | ''>('');
  const [search, setSearch] = useState('');
  const [from, setFrom] = useState(daysAgo(7));
  const [to, setTo] = useState(isoDate(new Date()));
  const [page, setPage] = useState(1);
  const q = { organizationId: orgId, siteId, status, search, from: startOfDay(from), to: endOfDay(to) };
  const list = useApi((s) => sessionsApi.list({ ...q, page, pageSize: PAGE_SIZE }, s), [orgId, siteId, status, search, from, to, page]);
  const reset = () => setPage(1);

  const exportXlsx = async () => {
    try { await sessionsApi.exportXlsx(q); } catch (err) { toast.error(getErrorMessage(err)); }
  };

  return (
    <>
      <PageHeader title="RADIUS accounting" subtitle="Session records built from Accounting Start, Interim-Update and Stop packets."
        actions={<button type="button" className="btn btn-light btn-icon" onClick={exportXlsx}><FiDownload /> Export Excel</button>} />
      <Card>
        <div className="nxt-toolbar">
          <SearchInput value={search} onChange={(v) => { setSearch(v); reset(); }} placeholder="User, MAC, IP, room or session ID" />
          <ScopeFilters orgId={orgId} onOrgChange={(v) => { setOrgId(v); reset(); }} siteId={siteId} onSiteChange={(v) => { setSiteId(v); reset(); }} />
          <select className="form-select" value={status} onChange={(e) => { setStatus(e.target.value as SessionStatus | ''); reset(); }} aria-label="Status">
            <option value="">All sessions</option><option value="Active">Active</option><option value="Stopped">Stopped</option><option value="Stale">Stale (no updates)</option>
          </select>
          <input type="date" className="form-control" style={{ width: 'auto', minWidth: 140 }} value={from} max={to} onChange={(e) => { setFrom(e.target.value); reset(); }} aria-label="From" />
          <input type="date" className="form-control" style={{ width: 'auto', minWidth: 140 }} value={to} min={from} onChange={(e) => { setTo(e.target.value); reset(); }} aria-label="To" />
        </div>
        <DataTable
          rows={list.data?.items}
          loading={list.loading}
          error={list.error}
          onRetry={list.reload}
          rowKey={(s) => s.id}
          empty={{ title: 'No sessions in this period', icon: <FiDatabase /> }}
          mobileBadge={(s) => <StatusBadge status={s.status} />}
          columns={[
            { key: 'start', header: 'Started', render: (s) => <span className="text-nowrap">{formatDate(s.startedAt)}</span> },
            { key: 'user', header: 'User', render: (s) => <div><div className="nxt-cell-primary">{s.subscriberName ?? s.username}</div><div className="nxt-cell-secondary">{s.username}</div></div> },
            ...(user?.isPlatformAdmin ? [{ key: 'org', header: 'Organization', render: (s: RadiusSessionRow) => s.organizationName ?? '—' }] : []),
            { key: 'nas', header: 'NAS', render: (s) => <div><div>{s.nasName}</div><div className="nxt-cell-secondary">{s.siteName}</div></div> },
            { key: 'mac', header: 'MAC / IP', render: (s) => <div className="font-monospace fs-sm"><div>{s.macAddress ?? '—'}</div><div className="nxt-cell-secondary">{s.framedIpAddress ?? ''}</div></div> },
            { key: 'dur', header: 'Duration', render: (s) => formatSeconds(s.sessionTimeSeconds) },
            { key: 'data', header: 'Down / up', render: (s) => <span className="text-nowrap font-tabular">{formatBytes(s.outputBytes)} / {formatBytes(s.inputBytes)}</span> },
            { key: 'end', header: 'Ended', render: (s) => (s.stoppedAt ? <div><div>{formatDate(s.stoppedAt)}</div><div className="nxt-cell-secondary">{s.terminateCause ?? ''}</div></div> : '—') },
            { key: 'status', header: 'Status', hideOnMobile: true, render: (s) => <StatusBadge status={s.status} /> },
          ]}
        />
        {list.data && <Pagination page={page} pageSize={PAGE_SIZE} totalCount={list.data.totalCount} onPageChange={setPage} />}
      </Card>
    </>
  );
}
