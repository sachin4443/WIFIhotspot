import { useState } from 'react';
import { useNavigate, useParams } from 'react-router';
import { FiCreditCard } from 'react-icons/fi';
import { walletsApi } from '../../api/platform';
import { useAuth } from '../../auth/useAuth';
import { OrganizationSelect } from '../../components/common/OrganizationSelect';
import { PageHeader } from '../../components/common/PageHeader';
import { ActiveBadge } from '../../components/ui/Badge';
import { Card } from '../../components/ui/Card';
import { DataTable } from '../../components/ui/DataTable';
import { Pagination } from '../../components/ui/Pagination';
import { SearchInput } from '../../components/ui/SearchInput';
import { ErrorState, LoadingBlock } from '../../components/ui/States';
import { useApi } from '../../hooks/useApi';
import { WalletPanel } from '../../panels/WalletPanel';
import { formatDate, formatMoney } from '../../utils/format';

const PAGE_SIZE = 25;

export function WalletsPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [orgId, setOrgId] = useState('');
  const list = useApi((s) => walletsApi.overview({ organizationId: orgId, search, page, pageSize: PAGE_SIZE }, s), [orgId, search, page]);

  return (
    <>
      <PageHeader title="Wallets" subtitle="Prepaid balance per site. Open a wallet to see its ledger or adjust the balance." />
      <Card>
        <div className="nxt-toolbar">
          <SearchInput value={search} onChange={(v) => { setSearch(v); setPage(1); }} placeholder="Search site or organization" />
          {user?.isPlatformAdmin && <OrganizationSelect value={orgId} onChange={(v) => { setOrgId(v); setPage(1); }} />}
        </div>
        <DataTable
          rows={list.data?.items}
          loading={list.loading}
          error={list.error}
          onRetry={list.reload}
          rowKey={(w) => w.walletId}
          onRowClick={(w) => navigate(`/wallets/${w.siteId}`)}
          empty={{ title: 'No wallets found', icon: <FiCreditCard /> }}
          mobileBadge={(w) => <ActiveBadge active={w.isActive} />}
          columns={[
            { key: 'site', header: 'Site', render: (w) => <div><div className="nxt-cell-primary">{w.siteName}</div><div className="nxt-cell-secondary">{w.siteCode}</div></div> },
            ...(user?.isPlatformAdmin ? [{ key: 'org', header: 'Organization', render: (w: { organizationName: string }) => w.organizationName }] : []),
            { key: 'balance', header: 'Balance', align: 'end', render: (w) => <span className={`font-tabular fw-semibold ${w.balance < 100 ? 'text-danger' : ''}`}>{formatMoney(w.balance, w.currency)}</span> },
            { key: 'updated', header: 'Last change', render: (w) => formatDate(w.updatedAt) },
            { key: 'status', header: 'Status', hideOnMobile: true, render: (w) => <ActiveBadge active={w.isActive} /> },
          ]}
        />
        {list.data && <Pagination page={page} pageSize={PAGE_SIZE} totalCount={list.data.totalCount} onPageChange={setPage} />}
      </Card>
    </>
  );
}

export function WalletDetailPage() {
  const { siteId = '' } = useParams();
  const info = useApi((s) => walletsApi.overview({ search: '', page: 1, pageSize: 100 }, s).then((r) => r.items.find((w) => w.siteId === siteId) ?? null), [siteId]);
  if (info.error) return <Card><ErrorState message={info.error} onRetry={info.reload} /></Card>;
  if (info.loading && info.data === undefined) return <LoadingBlock />;
  const w = info.data;
  return (
    <>
      <PageHeader breadcrumbs={[{ label: 'Wallets', to: '/wallets' }, { label: w?.siteName ?? 'Wallet' }]} title={w ? `${w.siteName} wallet` : 'Site wallet'} subtitle={w?.organizationName} />
      <WalletPanel siteId={siteId} siteName={w?.siteName} />
    </>
  );
}
