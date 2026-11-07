import { useEffect, useMemo, useState } from 'react';
import { FiDownload, FiLogOut, FiPlus, FiUpload, FiUsers } from 'react-icons/fi';
import { groupsApi, packagesApi, subscribersApi } from '../api/hotspot';
import { getErrorMessage } from '../api/errors';
import { SiteSelect } from '../components/common/SiteSelect';
import { Badge, StatusBadge } from '../components/ui/Badge';
import { Card } from '../components/ui/Card';
import { DataTable, type Column } from '../components/ui/DataTable';
import { Pagination } from '../components/ui/Pagination';
import { SearchInput } from '../components/ui/SearchInput';
import { Tabs } from '../components/ui/Tabs';
import { useConfirm } from '../components/ui/useConfirm';
import { useApi } from '../hooks/useApi';
import { useToast } from '../toast/useToast';
import { subscriberTypesFor, typeInfo, type Subscriber, type SubscriberStatus, type SubscriberType } from '../types/hotspot';
import { formatDate, formatRelative } from '../utils/format';
import { ImportModal } from './ImportModal';
import { SubscriberFormModal } from './SubscriberForm';

const PAGE_SIZE = 25;
const STATUSES: SubscriberStatus[] = ['Active', 'Suspended', 'Expired', 'CheckedOut'];

interface Props { organizationId: string; organizationTypeName: string | undefined; canWrite: boolean; initialType?: SubscriberType }

/** Subscribers by type, following the organization’s business workflow (hotel guests, students, employees, customers …). */
export function SubscribersPanel({ organizationId, organizationTypeName, canWrite, initialType }: Props) {
  const toast = useToast();
  const confirm = useConfirm();
  const types = useMemo(() => subscriberTypesFor(organizationTypeName), [organizationTypeName]);
  const [type, setType] = useState<SubscriberType>(initialType && types.includes(initialType) ? initialType : types[0]);
  useEffect(() => { if (!types.includes(type)) setType(types[0]); }, [types, type]);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<SubscriberStatus | ''>('');
  const [siteId, setSiteId] = useState('');
  const [groupId, setGroupId] = useState('');
  const groups = useApi((s) => groupsApi.list(organizationId, s), [organizationId]);
  const packages = useApi((s) => packagesApi.list(organizationId, undefined, s), [organizationId]);
  const list = useApi((s) => subscribersApi.list(organizationId, { type, status, siteId, groupId, search, page, pageSize: PAGE_SIZE }, s), [organizationId, type, status, siteId, groupId, search, page]);
  const [form, setForm] = useState<{ editing: Subscriber | null } | null>(null);
  const [importing, setImporting] = useState(false);
  const info = typeInfo(type);
  const isRoom = type === 'RoomGuest';

  const reset = () => setPage(1);
  const act = async (fn: () => Promise<unknown>, success: string) => {
    try { await fn(); toast.success(success); list.reload(); } catch (err) { toast.error(getErrorMessage(err)); }
  };

  const toggleSuspend = async (s: Subscriber) => {
    const next: SubscriberStatus = s.status === 'Active' ? 'Suspended' : 'Active';
    if (next === 'Suspended' && !(await confirm({ title: `Suspend ${s.fullName}?`, message: 'Their active sessions are disconnected and they cannot sign in until reactivated.', confirmLabel: 'Suspend' }))) return;
    await act(() => subscribersApi.setStatus(organizationId, s.id, next), next === 'Active' ? 'Subscriber reactivated.' : 'Subscriber suspended.');
  };
  const checkOut = async (s: Subscriber) => {
    if (!(await confirm({ title: `Check out room ${s.roomNumber}?`, message: `${s.fullName}’s Wi-Fi access ends now and active sessions are disconnected.`, confirmLabel: 'Check out' }))) return;
    await act(() => subscribersApi.checkOut(organizationId, s.id), 'Guest checked out.');
  };
  const remove = async (s: Subscriber) => {
    if (!(await confirm({ title: 'Delete subscriber', message: `Delete ${s.fullName} (${s.identifier})? Subscribers with session history can only be suspended.`, confirmLabel: 'Delete' }))) return;
    await act(() => subscribersApi.remove(organizationId, s.id), 'Subscriber deleted.');
  };
  const exportXlsx = async () => {
    try { await subscribersApi.exportXlsx(organizationId, { type, status, siteId, groupId, search }); }
    catch (err) { toast.error(getErrorMessage(err)); }
  };

  const columns: Column<Subscriber>[] = isRoom
    ? [
        { key: 'room', header: 'Room', render: (s) => <div><div className="nxt-cell-primary">Room {s.roomNumber}</div><div className="nxt-cell-secondary">{s.siteName}</div></div> },
        { key: 'guest', header: 'Guest', render: (s) => <div><div>{s.fullName}</div><div className="nxt-cell-secondary">{s.mobileNumber}</div></div> },
        { key: 'stay', header: 'Stay', render: (s) => <span className="fs-sm">{formatDate(s.checkInAt)} → {formatDate(s.checkOutAt)}</span> },
        { key: 'pkg', header: 'Package', render: (s) => s.packageName ?? 'Default' },
        { key: 'sess', header: 'Online', render: (s) => (s.activeSessions ? <Badge tone="success">{s.activeSessions} device{s.activeSessions > 1 ? 's' : ''}</Badge> : '—') },
        { key: 'status', header: 'Status', hideOnMobile: true, render: (s) => <StatusBadge status={s.status} /> },
      ]
    : [
        { key: 'id', header: info.idLabel, render: (s) => <div><div className="nxt-cell-primary">{s.fullName}</div><div className="nxt-cell-secondary font-monospace">{s.identifier}</div></div> },
        { key: 'contact', header: 'Contact', render: (s) => <div><div>{s.mobileNumber ?? '—'}</div>{s.email && <div className="nxt-cell-secondary">{s.email}</div>}</div> },
        ...(type !== 'Guest' ? [{ key: 'group', header: type === 'Student' ? 'Class / batch' : 'Department', render: (s: Subscriber) => s.groupName ?? '—' }] : []),
        { key: 'pkg', header: 'Package', render: (s) => s.packageName ?? 'Default' },
        { key: 'site', header: 'Site', render: (s) => s.siteName ?? 'All sites' },
        { key: 'auth', header: 'Sign-in', render: (s) => (s.hasPassword ? <Badge>Password</Badge> : <Badge tone="info">OTP</Badge>) },
        { key: 'last', header: 'Last online', render: (s) => (s.activeSessions ? <Badge tone="success">Online now</Badge> : formatRelative(s.lastLoginAt)) },
        { key: 'status', header: 'Status', hideOnMobile: true, render: (s) => <StatusBadge status={s.status} /> },
      ];

  return (
    <>
      <Tabs<SubscriberType> items={types.map((t) => ({ key: t, label: typeInfo(t).plural }))} active={type} onChange={(t) => { setType(t); setGroupId(''); reset(); }} />
      <Card>
        <div className="nxt-toolbar">
          <SearchInput value={search} onChange={(v) => { setSearch(v); reset(); }} placeholder={isRoom ? 'Search room, name or mobile' : `Search name, ${info.idLabel.toLowerCase()}, mobile`} />
          <select className="form-select" value={status} onChange={(e) => { setStatus(e.target.value as SubscriberStatus | ''); reset(); }} aria-label="Status">
            <option value="">All statuses</option>{STATUSES.filter((s) => isRoom || s !== 'CheckedOut').map((s) => <option key={s} value={s}>{s === 'CheckedOut' ? 'Checked out' : s}</option>)}
          </select>
          <div style={{ minWidth: 150 }} className="flex-grow-1 flex-md-grow-0"><SiteSelect organizationId={organizationId} value={siteId} onChange={(v) => { setSiteId(v); reset(); }} /></div>
          {!isRoom && type !== 'Guest' && (
            <select className="form-select" value={groupId} onChange={(e) => { setGroupId(e.target.value); reset(); }} aria-label="Group">
              <option value="">All groups</option>{groups.data?.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
            </select>
          )}
          <div className="d-flex flex-wrap gap-2 ms-md-auto">
            <button type="button" className="btn btn-light btn-icon" onClick={exportXlsx}><FiDownload /> Export</button>
            {canWrite && <button type="button" className="btn btn-light btn-icon" onClick={() => setImporting(true)}><FiUpload /> Import</button>}
            {canWrite && <button type="button" className="btn btn-primary btn-icon" onClick={() => setForm({ editing: null })}><FiPlus /> {isRoom ? 'Check in guest' : `Add ${info.label.toLowerCase()}`}</button>}
          </div>
        </div>
        <DataTable
          rows={list.data?.items}
          loading={list.loading}
          error={list.error}
          onRetry={list.reload}
          rowKey={(s) => s.id}
          onRowClick={canWrite ? (s) => setForm({ editing: s }) : undefined}
          empty={{ title: search || status || siteId || groupId ? `No ${info.plural.toLowerCase()} match` : `No ${info.plural.toLowerCase()} yet`, icon: <FiUsers />, text: canWrite && !search ? (isRoom ? 'Check in a guest to give them Wi-Fi access for their stay.' : 'Add them one by one or import an Excel file.') : undefined }}
          mobileBadge={(s) => <StatusBadge status={s.status} />}
          columns={columns}
          actions={canWrite ? (s) => (
            <>
              {isRoom && s.status === 'Active' && <button type="button" className="btn btn-light btn-sm btn-icon" onClick={() => checkOut(s)}><FiLogOut /> Check out</button>}
              {s.status !== 'CheckedOut' && <button type="button" className={`btn btn-sm ${s.status === 'Active' ? 'btn-light' : 'btn-outline-primary'}`} onClick={() => toggleSuspend(s)}>{s.status === 'Active' ? 'Suspend' : 'Activate'}</button>}
              <button type="button" className="btn btn-light btn-sm" onClick={() => setForm({ editing: s })}>Edit</button>
              <button type="button" className="btn btn-soft-danger btn-sm" onClick={() => remove(s)}>Delete</button>
            </>
          ) : undefined}
        />
        {list.data && <Pagination page={page} pageSize={PAGE_SIZE} totalCount={list.data.totalCount} onPageChange={setPage} />}
      </Card>
      {form && (
        <SubscriberFormModal key={form.editing?.id ?? `new-${type}`} open organizationId={organizationId} type={type} editing={form.editing}
          groups={groups.data ?? []} packages={packages.data ?? []} onClose={() => setForm(null)} onSaved={() => { setForm(null); list.reload(); }} />
      )}
      <ImportModal organizationId={organizationId} type={type} open={importing} onClose={() => setImporting(false)} onImported={() => { list.reload(); groups.reload(); }} />
    </>
  );
}
