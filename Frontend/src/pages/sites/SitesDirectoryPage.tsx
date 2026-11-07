import { useState } from 'react';
import { useNavigate } from 'react-router';
import { FiMapPin } from 'react-icons/fi';
import { sitesApi } from '../../api/platform';
import { useAuth } from '../../auth/useAuth';
import { OrganizationSelect } from '../../components/common/OrganizationSelect';
import { PageHeader } from '../../components/common/PageHeader';
import { ActiveBadge, Badge } from '../../components/ui/Badge';
import { Card } from '../../components/ui/Card';
import { DataTable } from '../../components/ui/DataTable';
import { Pagination } from '../../components/ui/Pagination';
import { SearchInput } from '../../components/ui/SearchInput';
import { useApi } from '../../hooks/useApi';
import { formatMoney } from '../../utils/format';

const PAGE_SIZE = 25;

/** Platform view of every site across organizations. */
export function SitesDirectoryPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [orgId, setOrgId] = useState('');
  const [active, setActive] = useState<'' | 'true' | 'false'>('');
  const list = useApi((s) => sitesApi.directory({ organizationId: orgId, search, isActive: active === '' ? '' : active === 'true', page, pageSize: PAGE_SIZE }, s), [orgId, search, active, page]);

  return (
    <>
      <PageHeader title="Sites" subtitle="All hotspot locations across organizations." />
      <Card>
        <div className="nxt-toolbar">
          <SearchInput value={search} onChange={(v) => { setSearch(v); setPage(1); }} placeholder="Search site, code, city or organization" />
          {user?.isPlatformAdmin && <OrganizationSelect value={orgId} onChange={(v) => { setOrgId(v); setPage(1); }} className="form-select" />}
          <select className="form-select" value={active} onChange={(e) => { setActive(e.target.value as typeof active); setPage(1); }} aria-label="Status">
            <option value="">All statuses</option><option value="true">Active</option><option value="false">Inactive</option>
          </select>
        </div>
        <DataTable
          rows={list.data?.items}
          loading={list.loading}
          error={list.error}
          onRetry={list.reload}
          rowKey={(s) => s.id}
          onRowClick={(s) => navigate(`/organizations/${s.organizationId}?tab=sites`)}
          empty={{ title: 'No sites found', icon: <FiMapPin /> }}
          mobileBadge={(s) => <ActiveBadge active={s.isActive} />}
          columns={[
            { key: 'site', header: 'Site', render: (s) => <div><div className="nxt-cell-primary">{s.name} {s.isPrimary && <Badge tone="info">Primary</Badge>}</div><div className="nxt-cell-secondary">{s.code}</div></div> },
            { key: 'org', header: 'Organization', render: (s) => s.organizationName },
            { key: 'city', header: 'City', render: (s) => s.city ?? '—' },
            { key: 'wallet', header: 'Wallet', align: 'end', render: (s) => <span className="font-tabular">{formatMoney(s.walletBalance)}</span> },
            { key: 'status', header: 'Status', hideOnMobile: true, render: (s) => <ActiveBadge active={s.isActive} /> },
          ]}
        />
        {list.data && <Pagination page={page} pageSize={PAGE_SIZE} totalCount={list.data.totalCount} onPageChange={setPage} />}
      </Card>
    </>
  );
}
