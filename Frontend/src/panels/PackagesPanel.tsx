import { useState, type FormEvent } from 'react';
import { FiPackage, FiPlus } from 'react-icons/fi';
import { packagesApi } from '../api/hotspot';
import { getErrorMessage } from '../api/errors';
import { SiteSelect } from '../components/common/SiteSelect';
import { ActiveBadge, Badge } from '../components/ui/Badge';
import { Card, CardHeader } from '../components/ui/Card';
import { DataTable } from '../components/ui/DataTable';
import { Field, Switch } from '../components/ui/Field';
import { Modal } from '../components/ui/Modal';
import { useConfirm } from '../components/ui/useConfirm';
import { useApi } from '../hooks/useApi';
import { useToast } from '../toast/useToast';
import type { InternetPackage } from '../types/hotspot';
import { formatKbps, formatMoney } from '../utils/format';

interface Form {
  siteId: string; name: string; description: string; downloadMbps: string; uploadMbps: string; dataLimitGb: string;
  validity: string; validityUnit: 'minutes' | 'hours' | 'days'; sessionTimeoutMinutes: string; idleTimeoutMinutes: string; maxDevices: string; price: string; isActive: boolean;
}
const EMPTY: Form = { siteId: '', name: '', description: '', downloadMbps: '10', uploadMbps: '5', dataLimitGb: '', validity: '1', validityUnit: 'days', sessionTimeoutMinutes: '', idleTimeoutMinutes: '15', maxDevices: '1', price: '0', isActive: true };
const UNIT = { minutes: 1, hours: 60, days: 1440 };

export const describeValidity = (m: number | null) => (m == null ? 'No time limit' : m % 1440 === 0 ? `${m / 1440} day${m === 1440 ? '' : 's'}` : m % 60 === 0 ? `${m / 60} h` : `${m} min`);
const describeData = (mb: number | null) => (mb == null ? 'Unlimited data' : mb >= 1024 ? `${+(mb / 1024).toFixed(1)} GB` : `${mb} MB`);

/** Internet packages: bandwidth (MikroTik rate limit), quota, validity, session limits, devices and price. */
export function PackagesPanel({ organizationId, canWrite }: { organizationId: string; canWrite: boolean }) {
  const toast = useToast();
  const confirm = useConfirm();
  const list = useApi((s) => packagesApi.list(organizationId, undefined, s), [organizationId]);
  const [editing, setEditing] = useState<InternetPackage | 'new' | null>(null);
  const [form, setForm] = useState<Form>(EMPTY);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  const open = (p: InternetPackage | 'new') => {
    setEditing(p);
    setErrors({});
    if (p === 'new') { setForm(EMPTY); return; }
    const v = p.validityMinutes;
    const unit: Form['validityUnit'] = v == null ? 'days' : v % 1440 === 0 ? 'days' : v % 60 === 0 ? 'hours' : 'minutes';
    setForm({
      siteId: p.siteId ?? '', name: p.name, description: p.description ?? '', downloadMbps: p.downloadKbps ? String(+(p.downloadKbps / 1024).toFixed(2)) : '0',
      uploadMbps: p.uploadKbps ? String(+(p.uploadKbps / 1024).toFixed(2)) : '0', dataLimitGb: p.dataLimitMb == null ? '' : String(+(p.dataLimitMb / 1024).toFixed(2)),
      validity: v == null ? '' : String(v / UNIT[unit]), validityUnit: unit, sessionTimeoutMinutes: p.sessionTimeoutMinutes?.toString() ?? '',
      idleTimeoutMinutes: p.idleTimeoutMinutes?.toString() ?? '', maxDevices: String(p.maxDevices), price: String(p.price), isActive: p.isActive,
    });
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!editing) return;
    const errs: Record<string, string> = {};
    const num = (v: string) => (v.trim() === '' ? null : Number(v));
    const down = num(form.downloadMbps) ?? 0, up = num(form.uploadMbps) ?? 0, data = num(form.dataLimitGb), validity = num(form.validity);
    const sess = num(form.sessionTimeoutMinutes), idle = num(form.idleTimeoutMinutes), devices = Number(form.maxDevices), price = Number(form.price);
    if (form.name.trim().length < 2) errs.name = 'Enter a package name.';
    if (!Number.isFinite(down) || down < 0) errs.downloadMbps = '0 or more (0 = unlimited).';
    if (!Number.isFinite(up) || up < 0) errs.uploadMbps = '0 or more (0 = unlimited).';
    if (data !== null && (!Number.isFinite(data) || data <= 0)) errs.dataLimitGb = 'Positive number, or empty for unlimited.';
    if (validity !== null && (!Number.isFinite(validity) || validity <= 0)) errs.validity = 'Positive number, or empty for no limit.';
    if (sess !== null && (!Number.isInteger(sess) || sess < 1)) errs.sessionTimeoutMinutes = 'Whole minutes ≥ 1, or empty.';
    if (idle !== null && (!Number.isInteger(idle) || idle < 1)) errs.idleTimeoutMinutes = 'Whole minutes ≥ 1, or empty.';
    if (!Number.isInteger(devices) || devices < 1 || devices > 50) errs.maxDevices = '1–50.';
    if (!Number.isFinite(price) || price < 0) errs.price = '0 or more.';
    setErrors(errs);
    if (Object.keys(errs).length) return;
    setSaving(true);
    try {
      const body = {
        siteId: form.siteId || null, name: form.name.trim(), description: form.description.trim() || null,
        downloadKbps: Math.round(down * 1024), uploadKbps: Math.round(up * 1024), dataLimitMb: data == null ? null : Math.round(data * 1024),
        validityMinutes: validity == null ? null : Math.round(validity * UNIT[form.validityUnit]), sessionTimeoutMinutes: sess, idleTimeoutMinutes: idle,
        maxDevices: devices, price, isActive: form.isActive,
      };
      if (editing === 'new') await packagesApi.create(organizationId, body);
      else await packagesApi.update(organizationId, editing.id, body);
      toast.success('Package saved.');
      setEditing(null);
      list.reload();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (p: InternetPackage) => {
    if (!(await confirm({ title: 'Delete package', message: `Delete “${p.name}”? Packages used by active subscribers cannot be deleted.`, confirmLabel: 'Delete' }))) return;
    try { await packagesApi.remove(organizationId, p.id); toast.success('Package deleted.'); list.reload(); }
    catch (err) { toast.error(getErrorMessage(err)); }
  };

  const set = (k: keyof Form, v: string | boolean) => setForm({ ...form, [k]: v });
  const text = (k: keyof Form, label: string, opts: { hint?: string; col?: string; suffix?: string; placeholder?: string } = {}) => (
    <Field label={label} error={errors[k]} hint={opts.hint} className={opts.col ?? 'col-6 col-md-4'}>
      {(id, inv) => (
        <div className="input-group">
          <input id={id} inputMode="decimal" className={`form-control ${inv ? 'is-invalid' : ''}`} value={form[k] as string} placeholder={opts.placeholder} onChange={(e) => set(k, e.target.value)} />
          {opts.suffix && <span className="input-group-text">{opts.suffix}</span>}
        </div>
      )}
    </Field>
  );

  return (
    <Card>
      <CardHeader title="Internet packages" subtitle="Bandwidth and usage policies assigned to subscribers and enforced through RADIUS replies." icon={<FiPackage />}
        actions={canWrite && <button type="button" className="btn btn-primary btn-sm btn-icon" onClick={() => open('new')}><FiPlus /> New package</button>} />
      <DataTable
        rows={list.data}
        loading={list.loading}
        error={list.error}
        onRetry={list.reload}
        rowKey={(p) => p.id}
        onRowClick={canWrite ? open : undefined}
        empty={{ title: 'No packages yet', icon: <FiPackage />, text: 'Create packages such as “Guest 24 h · 10 Mbps” or “Student 5 GB / month”.' }}
        mobileBadge={(p) => <ActiveBadge active={p.isActive} />}
        columns={[
          { key: 'name', header: 'Package', render: (p) => <div><div className="nxt-cell-primary">{p.name}</div><div className="nxt-cell-secondary">{p.siteName ? `Site: ${p.siteName}` : 'All sites'}</div></div> },
          { key: 'speed', header: 'Bandwidth ↓ / ↑', render: (p) => <span className="text-nowrap">{formatKbps(p.downloadKbps)} / {formatKbps(p.uploadKbps)}</span> },
          { key: 'data', header: 'Data', render: (p) => describeData(p.dataLimitMb) },
          { key: 'validity', header: 'Validity', render: (p) => describeValidity(p.validityMinutes) },
          { key: 'limits', header: 'Session limits', render: (p) => <div className="d-flex flex-wrap gap-1"><Badge>{p.maxDevices} device{p.maxDevices > 1 ? 's' : ''}</Badge>{p.sessionTimeoutMinutes && <Badge>Session {describeValidity(p.sessionTimeoutMinutes)}</Badge>}{p.idleTimeoutMinutes && <Badge>Idle {p.idleTimeoutMinutes} min</Badge>}</div> },
          { key: 'price', header: 'Price', render: (p) => (p.price ? formatMoney(p.price) : 'Free') },
          { key: 'subs', header: 'Subscribers', render: (p) => p.subscriberCount },
          { key: 'status', header: 'Status', hideOnMobile: true, render: (p) => <ActiveBadge active={p.isActive} /> },
        ]}
        actions={canWrite ? (p) => <><button type="button" className="btn btn-light btn-sm" onClick={() => open(p)}>Edit</button><button type="button" className="btn btn-soft-danger btn-sm" onClick={() => remove(p)}>Delete</button></> : undefined}
      />
      <Modal open={editing !== null} size="lg" title={editing === 'new' ? 'New internet package' : 'Edit package'} onClose={() => setEditing(null)} busy={saving}
        footer={<><button type="button" className="btn btn-light" onClick={() => setEditing(null)} disabled={saving}>Cancel</button><button type="submit" form="pkg-form" className="btn btn-primary" disabled={saving}>{saving ? 'Saving…' : 'Save package'}</button></>}>
        <form id="pkg-form" onSubmit={submit} noValidate>
          <div className="row g-3">
            <Field label="Package name" required error={errors.name} className="col-12 col-md-7">
              {(id, inv) => <input id={id} className={`form-control ${inv ? 'is-invalid' : ''}`} value={form.name} maxLength={100} onChange={(e) => set('name', e.target.value)} placeholder="Guest 24 h · 10 Mbps" />}
            </Field>
            <Field label="Available at" className="col-12 col-md-5">
              {(id) => <SiteSelect id={id} organizationId={organizationId} value={form.siteId} onChange={(v) => set('siteId', v)} allLabel="All sites" />}
            </Field>
            <Field label="Description" className="col-12">
              {(id) => <input id={id} className="form-control" value={form.description} maxLength={500} onChange={(e) => set('description', e.target.value)} />}
            </Field>
          </div>
          <div className="nxt-form-section mt-3">
            <div className="nxt-form-section-title">Bandwidth</div>
            <div className="nxt-form-section-desc">Sent to MikroTik as Mikrotik-Rate-Limit. 0 = unlimited.</div>
            <div className="row g-3">{text('downloadMbps', 'Download', { suffix: 'Mbps' })}{text('uploadMbps', 'Upload', { suffix: 'Mbps' })}{text('dataLimitGb', 'Data quota', { suffix: 'GB', placeholder: 'Unlimited' })}</div>
          </div>
          <div className="nxt-form-section">
            <div className="nxt-form-section-title">Time and session limits</div>
            <div className="row g-3">
              <Field label="Validity after first sign-in" error={errors.validity} className="col-12 col-md-6">
                {(id, inv) => (
                  <div className="input-group">
                    <input id={id} inputMode="decimal" className={`form-control ${inv ? 'is-invalid' : ''}`} value={form.validity} placeholder="No limit" onChange={(e) => set('validity', e.target.value)} />
                    <select className="form-select" style={{ maxWidth: 120 }} value={form.validityUnit} onChange={(e) => set('validityUnit', e.target.value)} aria-label="Unit"><option value="minutes">minutes</option><option value="hours">hours</option><option value="days">days</option></select>
                  </div>
                )}
              </Field>
              {text('maxDevices', 'Devices at once', { col: 'col-6 col-md-3' })}
              {text('price', 'Price (INR)', { col: 'col-6 col-md-3' })}
              {text('sessionTimeoutMinutes', 'Max session length', { suffix: 'min', placeholder: 'None', hint: 'Session-Timeout per connection.' })}
              {text('idleTimeoutMinutes', 'Idle timeout', { suffix: 'min', placeholder: 'None' })}
            </div>
          </div>
          <div className="mt-3"><Switch label="Package is active" description="Inactive packages cannot be assigned to new subscribers." checked={form.isActive} onChange={(v) => set('isActive', v)} /></div>
        </form>
      </Modal>
    </Card>
  );
}
