import { useState, type FormEvent } from 'react';
import { FiEdit2, FiMapPin, FiPlus } from 'react-icons/fi';
import { sitesApi } from '../api/platform';
import { getErrorMessage } from '../api/errors';
import { ActiveBadge, Badge } from '../components/ui/Badge';
import { Card, CardHeader } from '../components/ui/Card';
import { DataTable } from '../components/ui/DataTable';
import { Field, Switch } from '../components/ui/Field';
import { Modal } from '../components/ui/Modal';
import { useApi } from '../hooks/useApi';
import { useToast } from '../toast/useToast';
import type { Site } from '../types/models';
import { isSiteCode } from '../utils/validation';

interface Form { name: string; code: string; address: string; city: string; isActive: boolean }
const EMPTY: Form = { name: '', code: '', address: '', city: '', isActive: true };

export function SitesPanel({ organizationId, canWrite, onChanged, onOpenSite }: { organizationId: string; canWrite: boolean; onChanged?: () => void; onOpenSite?: (site: Site) => void }) {
  const toast = useToast();
  const { data, loading, error, reload } = useApi((s) => sitesApi.list(organizationId, s), [organizationId]);
  const [editing, setEditing] = useState<Site | 'new' | null>(null);
  const [form, setForm] = useState<Form>(EMPTY);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  const open = (site: Site | 'new') => {
    setEditing(site);
    setErrors({});
    setForm(site === 'new' ? EMPTY : { name: site.name, code: site.code, address: site.address ?? '', city: site.city ?? '', isActive: site.isActive });
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const errs: Record<string, string> = {};
    if (form.name.trim().length < 2) errs.name = 'Enter a site name.';
    if (editing === 'new' && !isSiteCode(form.code)) errs.code = '2–30 letters, digits, - or _.';
    setErrors(errs);
    if (Object.keys(errs).length || !editing) return;
    setSaving(true);
    try {
      if (editing === 'new') {
        await sitesApi.create(organizationId, { name: form.name.trim(), code: form.code.trim(), address: form.address.trim() || null, city: form.city.trim() || null });
        toast.success('Site created with its own wallet.');
      } else {
        await sitesApi.update(editing.id, { name: form.name.trim(), address: form.address.trim() || null, city: form.city.trim() || null, isActive: form.isActive });
        toast.success('Site updated.');
      }
      setEditing(null);
      reload();
      onChanged?.();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card>
      <CardHeader
        title="Sites"
        subtitle="Physical locations. Each site has its own wallet, NAS devices and login-method overrides."
        actions={canWrite && <button type="button" className="btn btn-primary btn-sm btn-icon" onClick={() => open('new')}><FiPlus /> Add site</button>}
      />
      <DataTable
        rows={data}
        loading={loading}
        error={error}
        onRetry={reload}
        rowKey={(s) => s.id}
        onRowClick={onOpenSite}
        empty={{ title: 'No sites yet', icon: <FiMapPin />, text: 'Create the first location for this organization.' }}
        mobileBadge={(s) => <ActiveBadge active={s.isActive} />}
        columns={[
          { key: 'name', header: 'Site', render: (s) => <div><div className="nxt-cell-primary">{s.name} {s.isPrimary && <Badge tone="info">Primary</Badge>}</div><div className="nxt-cell-secondary">{s.code}</div></div> },
          { key: 'city', header: 'City', render: (s) => s.city ?? '—' },
          { key: 'address', header: 'Address', render: (s) => <span className="text-muted-2">{s.address ?? '—'}</span> },
          { key: 'status', header: 'Status', hideOnMobile: true, render: (s) => <ActiveBadge active={s.isActive} /> },
        ]}
        actions={canWrite ? (s) => <button type="button" className="btn btn-light btn-sm btn-icon" onClick={() => open(s)}><FiEdit2 /> Edit</button> : undefined}
      />
      <Modal
        open={editing !== null}
        title={editing === 'new' ? 'Add site' : 'Edit site'}
        onClose={() => setEditing(null)}
        busy={saving}
        footer={<><button type="button" className="btn btn-light" onClick={() => setEditing(null)} disabled={saving}>Cancel</button><button type="submit" form="site-form" className="btn btn-primary" disabled={saving}>{saving ? 'Saving…' : 'Save site'}</button></>}
      >
        <form id="site-form" onSubmit={submit} noValidate className="row g-3">
          <Field label="Site name" required error={errors.name} className="col-12 col-md-7">
            {(id, inv) => <input id={id} className={`form-control ${inv ? 'is-invalid' : ''}`} value={form.name} maxLength={150} onChange={(e) => setForm({ ...form, name: e.target.value })} />}
          </Field>
          <Field label="Site code" required={editing === 'new'} error={errors.code} hint={editing === 'new' ? 'Unique within the organization. Cannot be changed later.' : 'Codes cannot be changed.'} className="col-12 col-md-5">
            {(id, inv) => <input id={id} className={`form-control text-uppercase ${inv ? 'is-invalid' : ''}`} value={form.code} maxLength={30} disabled={editing !== 'new'} onChange={(e) => setForm({ ...form, code: e.target.value })} />}
          </Field>
          <Field label="City" className="col-12 col-md-5">
            {(id) => <input id={id} className="form-control" value={form.city} maxLength={100} onChange={(e) => setForm({ ...form, city: e.target.value })} />}
          </Field>
          <Field label="Address" className="col-12 col-md-7">
            {(id) => <input id={id} className="form-control" value={form.address} maxLength={500} onChange={(e) => setForm({ ...form, address: e.target.value })} />}
          </Field>
          {editing !== 'new' && editing && (
            <div className="col-12">
              <Switch label="Site is active" description={editing.isPrimary ? 'The primary site cannot be deactivated.' : 'Inactive sites stop accepting hotspot sign-ins.'} checked={form.isActive} disabled={editing.isPrimary} onChange={(v) => setForm({ ...form, isActive: v })} />
            </div>
          )}
        </form>
      </Modal>
    </Card>
  );
}
