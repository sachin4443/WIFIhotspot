import { useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { FiBriefcase, FiPlus } from 'react-icons/fi';
import { organizationsApi, referenceApi } from '../../api/platform';
import { useAuth } from '../../auth/useAuth';
import { PageHeader } from '../../components/common/PageHeader';
import { StatusBadge } from '../../components/ui/Badge';
import { Card } from '../../components/ui/Card';
import { DataTable } from '../../components/ui/DataTable';
import { Pagination } from '../../components/ui/Pagination';
import { SearchInput } from '../../components/ui/SearchInput';
import { useApi } from '../../hooks/useApi';
import { ORGANIZATION_STATUSES, type OrganizationStatus } from '../../types/models';
import { Permissions } from '../../types/permissions';
import { formatDate, formatNumber } from '../../utils/format';

const PAGE_SIZE = 20;

export function OrganizationsPage() {
  const navigate = useNavigate();
  const { hasPermission } = useAuth();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<OrganizationStatus | ''>('');
  const [typeId, setTypeId] = useState<number | ''>('');
  const types = useApi((s) => referenceApi.organizationTypes(true, s), []);
  const list = useApi((s) => organizationsApi.list({ page, pageSize: PAGE_SIZE, search, status, organizationTypeId: typeId }, s), [page, search, status, typeId]);

  return (
    <>
      <PageHeader
        title="Organizations"
        subtitle="Every tenant on the platform: ISPs, hotels, campuses, enterprises and public Wi-Fi operators."
        actions={hasPermission(Permissions.OrganizationsWrite) && <Link to="/organizations/new" className="btn btn-primary btn-icon"><FiPlus /> Register organization</Link>}
      />
      <Card>
        <div className="nxt-toolbar">
          <SearchInput value={search} onChange={(v) => { setSearch(v); setPage(1); }} placeholder="Search name, contact or email" />
          <select className="form-select" value={status} onChange={(e) => { setStatus(e.target.value as OrganizationStatus | ''); setPage(1); }} aria-label="Status">
            <option value="">All statuses</option>
            {ORGANIZATION_STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
          <select className="form-select" value={typeId} onChange={(e) => { setTypeId(e.target.value ? Number(e.target.value) : ''); setPage(1); }} aria-label="Type">
            <option value="">All types</option>
            {types.data?.map((t) => <option key={t.organizationTypeId} value={t.organizationTypeId}>{t.typeName}</option>)}
          </select>
        </div>
        <DataTable
          rows={list.data?.items}
          loading={list.loading}
          error={list.error}
          onRetry={list.reload}
          rowKey={(o) => o.id}
          onRowClick={(o) => navigate(`/organizations/${o.id}`)}
          empty={{
            title: search || status || typeId ? 'No organizations match these filters' : 'No organizations yet', icon: <FiBriefcase />,
            action: hasPermission(Permissions.OrganizationsWrite) && !search ? <Link to="/organizations/new" className="btn btn-primary btn-sm">Register organization</Link> : undefined,
          }}
          mobileBadge={(o) => <StatusBadge status={o.status} />}
          columns={[
            { key: 'name', header: 'Organization', render: (o) => <div className="nxt-cell-primary">{o.name}</div> },
            { key: 'type', header: 'Type', render: (o) => o.organizationTypeName },
            { key: 'sites', header: 'Sites', render: (o) => formatNumber(o.siteCount) },
            { key: 'status', header: 'Status', hideOnMobile: true, render: (o) => <StatusBadge status={o.status} /> },
            { key: 'created', header: 'Registered', render: (o) => formatDate(o.createdAt, false) },
          ]}
        />
        {list.data && <Pagination page={page} pageSize={PAGE_SIZE} totalCount={list.data.totalCount} onPageChange={setPage} />}
      </Card>
    </>
  );
}
