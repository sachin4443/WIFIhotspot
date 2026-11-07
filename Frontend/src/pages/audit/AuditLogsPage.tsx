import { useState } from 'react';
import { FiActivity } from 'react-icons/fi';
import { auditApi } from '../../api/platform';
import { useAuth } from '../../auth/useAuth';
import { OrganizationSelect } from '../../components/common/OrganizationSelect';
import { PageHeader } from '../../components/common/PageHeader';
import { Badge } from '../../components/ui/Badge';
import { Card } from '../../components/ui/Card';
import { DataTable } from '../../components/ui/DataTable';
import { Pagination } from '../../components/ui/Pagination';
import { SearchInput } from '../../components/ui/SearchInput';
import { useApi } from '../../hooks/useApi';
import { formatDate, humanize } from '../../utils/format';

const PAGE_SIZE = 50;
const toneFor = (action: string) => (/Fail|Lockout|Deactivat|Suspend|Delet|Debit|Disconnect/i.test(action) ? 'warning' : /Created|Registered|Success|Credit/i.test(action) ? 'success' : 'neutral');

export function AuditLogsPage() {
  const { user } = useAuth();
  const [page, setPage] = useState(1);
  const [orgId, setOrgId] = useState('');
  const [action, setAction] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const toIso = (d: string, end: boolean) => (d ? new Date(`${d}T${end ? '23:59:59' : '00:00:00'}`).toISOString() : '');
  const list = useApi((s) => auditApi.list({ organizationId: orgId, action, from: toIso(from, false), to: toIso(to, true), page, pageSize: PAGE_SIZE }, s), [orgId, action, from, to, page]);

  return (
    <>
      <PageHeader title="Audit log" subtitle="Security-relevant actions by administrators. Entries never contain passwords, OTPs or API keys." />
      <Card>
        <div className="nxt-toolbar">
          <SearchInput value={action} onChange={(v) => { setAction(v); setPage(1); }} placeholder="Action, e.g. LoginFailed" />
          {user?.isPlatformAdmin && <OrganizationSelect value={orgId} onChange={(v) => { setOrgId(v); setPage(1); }} />}
          <input type="date" className="form-control" style={{ width: 'auto', minWidth: 150 }} value={from} onChange={(e) => { setFrom(e.target.value); setPage(1); }} aria-label="From date" />
          <input type="date" className="form-control" style={{ width: 'auto', minWidth: 150 }} value={to} onChange={(e) => { setTo(e.target.value); setPage(1); }} aria-label="To date" />
        </div>
        <DataTable
          rows={list.data?.items}
          loading={list.loading}
          error={list.error}
          onRetry={list.reload}
          rowKey={(a) => a.id}
          empty={{ title: 'No audit entries match', icon: <FiActivity /> }}
          mobileTitle={(a) => <span className="nxt-cell-primary">{humanize(a.action)}</span>}
          columns={[
            { key: 'time', header: 'Time', render: (a) => <span className="text-nowrap">{formatDate(a.createdAt)}</span> },
            { key: 'action', header: 'Action', hideOnMobile: true, render: (a) => <Badge tone={toneFor(a.action)}>{humanize(a.action)}</Badge> },
            { key: 'entity', header: 'Entity', render: (a) => a.entityType },
            { key: 'details', header: 'Details', render: (a) => <span className="text-muted-2 text-break">{a.details ?? '—'}</span> },
            { key: 'ip', header: 'IP', render: (a) => a.ipAddress ?? '—' },
          ]}
        />
        {list.data && <Pagination page={page} pageSize={PAGE_SIZE} totalCount={list.data.totalCount} onPageChange={setPage} />}
      </Card>
    </>
  );
}
