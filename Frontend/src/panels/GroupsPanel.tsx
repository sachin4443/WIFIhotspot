import { useState, type FormEvent } from 'react';
import { FiLayers, FiPlus } from 'react-icons/fi';
import { groupsApi, packagesApi } from '../api/hotspot';
import { getErrorMessage } from '../api/errors';
import { ActiveBadge, Badge } from '../components/ui/Badge';
import { Card, CardHeader } from '../components/ui/Card';
import { DataTable } from '../components/ui/DataTable';
import { Field, Switch } from '../components/ui/Field';
import { Modal } from '../components/ui/Modal';
import { useConfirm } from '../components/ui/useConfirm';
import { useApi } from '../hooks/useApi';
import { useToast } from '../toast/useToast';
import { GROUP_TYPES, type SubscriberGroup, type SubscriberGroupType } from '../types/hotspot';

interface Form { name: string; groupType: SubscriberGroupType; description: string; defaultPackageId: string; isActive: boolean }

/** Departments, classes, courses, batches and floors with an optional default package (policy). */
export function GroupsPanel({ organizationId, canWrite, defaultType = 'Department' }: { organizationId: string; canWrite: boolean; defaultType?: SubscriberGroupType }) {
  const toast = useToast();
  const confirm = useConfirm();
  const list = useApi((s) => groupsApi.list(organizationId, s), [organizationId]);
  const packages = useApi((s) => packagesApi.list(organizationId, undefined, s), [organizationId]);
  const [editing, setEditing] = useState<SubscriberGroup | 'new' | null>(null);
  const [form, setForm] = useState<Form>({ name: '', groupType: defaultType, description: '', defaultPackageId: '', isActive: true });
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const open = (g: SubscriberGroup | 'new') => {
    setEditing(g);
    setError(null);
    setForm(g === 'new' ? { name: '', groupType: defaultType, description: '', defaultPackageId: '', isActive: true }
      : { name: g.name, groupType: g.groupType, description: g.description ?? '', defaultPackageId: g.defaultPackageId ?? '', isActive: g.isActive });
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!editing) return;
    if (!form.name.trim()) { setError('Enter a name.'); return; }
    setSaving(true);
    try {
      const body = { name: form.name.trim(), groupType: form.groupType, description: form.description.trim() || null, defaultPackageId: form.defaultPackageId || null, isActive: form.isActive };
      if (editing === 'new') await groupsApi.create(organizationId, body);
      else await groupsApi.update(organizationId, editing.id, body);
      toast.success('Group saved.');
      setEditing(null);
      list.reload();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (g: SubscriberGroup) => {
    if (!(await confirm({ title: 'Delete group', message: `Delete “${g.name}”? Groups with subscribers cannot be deleted.`, confirmLabel: 'Delete' }))) return;
    try { await groupsApi.remove(organizationId, g.id); toast.success('Group deleted.'); list.reload(); }
    catch (err) { toast.error(getErrorMessage(err)); }
  };

  return (
    <Card>
      <CardHeader title="Groups & policies" subtitle="Departments, classes, courses, batches or floors. A group’s default package is applied to new members." icon={<FiLayers />}
        actions={canWrite && <button type="button" className="btn btn-primary btn-sm btn-icon" onClick={() => open('new')}><FiPlus /> New group</button>} />
      <DataTable
        rows={list.data}
        loading={list.loading}
        error={list.error}
        onRetry={list.reload}
        rowKey={(g) => g.id}
        onRowClick={canWrite ? open : undefined}
        empty={{ title: 'No groups yet', icon: <FiLayers />, text: 'Groups are also created automatically when you import subscribers with a Group column.' }}
        mobileBadge={(g) => <ActiveBadge active={g.isActive} />}
        columns={[
          { key: 'name', header: 'Group', render: (g) => <div><div className="nxt-cell-primary">{g.name}</div><div className="nxt-cell-secondary">{g.description ?? ''}</div></div> },
          { key: 'type', header: 'Type', render: (g) => <Badge>{g.groupType}</Badge> },
          { key: 'pkg', header: 'Default package', render: (g) => g.defaultPackageName ?? '—' },
          { key: 'members', header: 'Members', render: (g) => g.subscriberCount },
          { key: 'status', header: 'Status', hideOnMobile: true, render: (g) => <ActiveBadge active={g.isActive} /> },
        ]}
        actions={canWrite ? (g) => <><button type="button" className="btn btn-light btn-sm" onClick={() => open(g)}>Edit</button><button type="button" className="btn btn-soft-danger btn-sm" onClick={() => remove(g)}>Delete</button></> : undefined}
      />
      <Modal open={editing !== null} title={editing === 'new' ? 'New group' : 'Edit group'} onClose={() => setEditing(null)} busy={saving}
        footer={<><button type="button" className="btn btn-light" onClick={() => setEditing(null)} disabled={saving}>Cancel</button><button type="submit" form="group-form" className="btn btn-primary" disabled={saving}>{saving ? 'Saving…' : 'Save group'}</button></>}>
        <form id="group-form" onSubmit={submit} noValidate className="row g-3">
          <Field label="Name" required error={error} className="col-12 col-md-7">
            {(id, inv) => <input id={id} className={`form-control ${inv ? 'is-invalid' : ''}`} value={form.name} maxLength={100} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="Computer Science 2026" />}
          </Field>
          <Field label="Type" className="col-12 col-md-5">
            {(id) => <select id={id} className="form-select" value={form.groupType} onChange={(e) => setForm({ ...form, groupType: e.target.value as SubscriberGroupType })}>{GROUP_TYPES.map((t) => <option key={t}>{t}</option>)}</select>}
          </Field>
          <Field label="Default package" hint="Policy applied to new members of this group." className="col-12">
            {(id) => <select id={id} className="form-select" value={form.defaultPackageId} onChange={(e) => setForm({ ...form, defaultPackageId: e.target.value })}><option value="">None</option>{packages.data?.filter((p) => p.isActive).map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}</select>}
          </Field>
          <Field label="Description" className="col-12">
            {(id) => <input id={id} className="form-control" value={form.description} maxLength={500} onChange={(e) => setForm({ ...form, description: e.target.value })} />}
          </Field>
          <div className="col-12"><Switch label="Active" checked={form.isActive} onChange={(v) => setForm({ ...form, isActive: v })} /></div>
        </form>
      </Modal>
    </Card>
  );
}
