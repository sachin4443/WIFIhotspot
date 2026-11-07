import { useState, type FormEvent } from 'react';
import { FiCode, FiEdit2, FiKey, FiPlus, FiRadio, FiTrash2 } from 'react-icons/fi';
import { nasApi } from '../api/hotspot';
import { getErrorMessage } from '../api/errors';
import { CopyButton } from '../components/common/CopyButton';
import { SiteSelect } from '../components/common/SiteSelect';
import { ActiveBadge, Badge } from '../components/ui/Badge';
import { Card, CardHeader } from '../components/ui/Card';
import { DataTable } from '../components/ui/DataTable';
import { Field, Switch } from '../components/ui/Field';
import { Modal } from '../components/ui/Modal';
import { LoadingBlock, Notice } from '../components/ui/States';
import { useConfirm } from '../components/ui/useConfirm';
import { useApi } from '../hooks/useApi';
import { useToast } from '../toast/useToast';
import type { Nas, PortalKey, RouterConfig, UpsertNasInput } from '../types/hotspot';
import { formatRelative } from '../utils/format';

interface Form { siteId: string; name: string; ipAddress: string; vendor: 'MikroTik' | 'Other'; routerOsVersion: string; sharedSecret: string; authPort: string; acctPort: string; coaPort: string; description: string; isActive: boolean }
const EMPTY: Form = { siteId: '', name: '', ipAddress: '', vendor: 'MikroTik', routerOsVersion: 'v7', sharedSecret: '', authPort: '1812', acctPort: '1813', coaPort: '3799', description: '', isActive: true };
const IPV4 = /^(25[0-5]|2[0-4]\d|1?\d?\d)(\.(25[0-5]|2[0-4]\d|1?\d?\d)){3}$/;
const looksLikeIp = (v: string) => IPV4.test(v.trim()) || (v.includes(':') && /^[0-9a-fA-F:.]+$/.test(v.trim()));
const port = (v: string) => /^\d+$/.test(v) && +v >= 1 && +v <= 65535;

/** NAS devices (routers) of one organization. Shared secrets are write-only. */
export function NasPanel({ organizationId, canWrite }: { organizationId: string; canWrite: boolean }) {
  const toast = useToast();
  const confirm = useConfirm();
  const [siteFilter, setSiteFilter] = useState('');
  const list = useApi((s) => nasApi.list(organizationId, siteFilter, s), [organizationId, siteFilter]);
  const [editing, setEditing] = useState<Nas | 'new' | null>(null);
  const [form, setForm] = useState<Form>(EMPTY);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);
  const [portal, setPortal] = useState<PortalKey | null>(null);
  const [config, setConfig] = useState<{ nas: Nas; data?: RouterConfig } | null>(null);

  const open = (n: Nas | 'new') => {
    setEditing(n);
    setErrors({});
    setForm(n === 'new' ? { ...EMPTY, siteId: siteFilter } : {
      siteId: n.siteId, name: n.name, ipAddress: n.ipAddress, vendor: n.vendor, routerOsVersion: n.routerOsVersion ?? '', sharedSecret: '',
      authPort: String(n.authPort), acctPort: String(n.acctPort), coaPort: String(n.coaPort), description: n.description ?? '', isActive: n.isActive,
    });
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!editing) return;
    const errs: Record<string, string> = {};
    if (!form.siteId) errs.siteId = 'Choose the site where this router is installed.';
    if (form.name.trim().length < 2) errs.name = 'Enter a name.';
    if (!looksLikeIp(form.ipAddress)) errs.ipAddress = 'Enter the router’s IPv4 or IPv6 address.';
    if (editing === 'new' && form.sharedSecret.length < 12) errs.sharedSecret = 'At least 12 characters.';
    if (editing !== 'new' && form.sharedSecret && form.sharedSecret.length < 12) errs.sharedSecret = 'At least 12 characters, or leave empty to keep the current secret.';
    (['authPort', 'acctPort', 'coaPort'] as const).forEach((k) => { if (!port(form[k])) errs[k] = '1–65535'; });
    setErrors(errs);
    if (Object.keys(errs).length) return;
    const body: UpsertNasInput = {
      siteId: form.siteId, name: form.name.trim(), ipAddress: form.ipAddress.trim(), vendor: form.vendor, routerOsVersion: form.routerOsVersion || null,
      sharedSecret: form.sharedSecret || null, authPort: +form.authPort, acctPort: +form.acctPort, coaPort: +form.coaPort,
      description: form.description.trim() || null, isActive: form.isActive,
    };
    setSaving(true);
    try {
      if (editing === 'new') await nasApi.create(organizationId, body);
      else await nasApi.update(organizationId, editing.id, body);
      toast.success(editing === 'new' ? 'NAS device added. The shared secret is stored encrypted.' : 'NAS device updated.');
      setEditing(null);
      list.reload();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (n: Nas) => {
    if (!(await confirm({ title: 'Delete NAS device', message: `Delete ${n.name} (${n.ipAddress})? Routers with session history can only be deactivated.`, confirmLabel: 'Delete' }))) return;
    try { await nasApi.remove(organizationId, n.id); toast.success('NAS device deleted.'); list.reload(); }
    catch (err) { toast.error(getErrorMessage(err)); }
  };

  const regenerate = async (n: Nas) => {
    if (n.hasPortalKey && !(await confirm({ title: 'Generate a new portal key?', message: 'The current key stops working immediately. Update the router’s login page with the new URL.', confirmLabel: 'Generate', tone: 'primary' }))) return;
    try { setPortal(await nasApi.portalKey(organizationId, n.id)); list.reload(); }
    catch (err) { toast.error(getErrorMessage(err)); }
  };

  const showConfig = async (n: Nas) => {
    setConfig({ nas: n });
    try { const data = await nasApi.routerConfig(organizationId, n.id); setConfig({ nas: n, data }); }
    catch (err) { toast.error(getErrorMessage(err)); setConfig(null); }
  };

  const set = (k: keyof Form, v: string | boolean) => setForm({ ...form, [k]: v });

  return (
    <Card>
      <CardHeader
        title="NAS devices"
        subtitle="Routers that send RADIUS authentication and accounting to NXT. Each is identified by its source IP and shared secret."
        icon={<FiRadio />}
        actions={canWrite && <button type="button" className="btn btn-primary btn-sm btn-icon" onClick={() => open('new')}><FiPlus /> Add NAS</button>}
      />
      <div className="nxt-toolbar"><div style={{ minWidth: 220 }}><SiteSelect organizationId={organizationId} value={siteFilter} onChange={setSiteFilter} /></div></div>
      <DataTable
        rows={list.data}
        loading={list.loading}
        error={list.error}
        onRetry={list.reload}
        rowKey={(n) => n.id}
        empty={{ title: 'No NAS devices yet', icon: <FiRadio />, text: 'Add each MikroTik router that should authenticate hotspot users against NXT.' }}
        mobileBadge={(n) => <ActiveBadge active={n.isActive} />}
        columns={[
          { key: 'name', header: 'Device', render: (n) => <div style={{ minWidth: 160 }}><div className="nxt-cell-primary">{n.name}</div><div className="nxt-cell-secondary text-nowrap">{n.vendor}{n.routerOsVersion ? ` ${n.routerOsVersion}` : ''} · ports {n.authPort}/{n.acctPort}/{n.coaPort}</div></div> },
          { key: 'ip', header: 'IP address', render: (n) => <span className="font-monospace text-nowrap">{n.ipAddress}</span> },
          { key: 'site', header: 'Site', render: (n) => <span className="text-nowrap">{n.siteName}</span> },
          { key: 'sec', header: 'Security', render: (n) => <div className="d-flex flex-wrap gap-1">{n.hasSharedSecret ? <Badge tone="success">Secret set</Badge> : <Badge tone="danger">No secret</Badge>}{n.hasPortalKey ? <Badge tone="info">Portal key</Badge> : <Badge>No portal key</Badge>}</div> },
          { key: 'activity', header: 'Activity', render: (n) => <div className="text-nowrap"><div>{n.activeSessions} online</div><div className="nxt-cell-secondary">Last packet {formatRelative(n.lastSeenAt)}</div></div> },
          { key: 'status', header: 'Status', hideOnMobile: true, render: (n) => <ActiveBadge active={n.isActive} /> },
        ]}
        actions={(n) => (
          <>
            <button type="button" className="btn btn-light btn-sm btn-icon" onClick={() => showConfig(n)} title="RouterOS configuration" aria-label="Config"><FiCode /><span className="d-md-none">Config</span></button>
            {canWrite && <button type="button" className="btn btn-light btn-sm btn-icon" onClick={() => regenerate(n)} title="Generate captive-portal key" aria-label="Portal key"><FiKey /><span className="d-md-none">Portal key</span></button>}
            {canWrite && <button type="button" className="btn btn-light btn-sm btn-icon" onClick={() => open(n)} title="Edit" aria-label="Edit"><FiEdit2 /><span className="d-md-none">Edit</span></button>}
            {canWrite && <button type="button" className="btn btn-soft-danger btn-sm btn-icon" onClick={() => remove(n)} title="Delete" aria-label="Delete"><FiTrash2 /><span className="d-md-none">Delete</span></button>}
          </>
        )}
      />

      <Modal open={editing !== null} size="lg" title={editing === 'new' ? 'Add NAS device' : 'Edit NAS device'} onClose={() => setEditing(null)} busy={saving}
        footer={<><button type="button" className="btn btn-light" onClick={() => setEditing(null)} disabled={saving}>Cancel</button><button type="submit" form="nas-form" className="btn btn-primary" disabled={saving}>{saving ? 'Saving…' : 'Save NAS'}</button></>}>
        <form id="nas-form" onSubmit={submit} noValidate className="row g-3">
          <Field label="Name" required error={errors.name} className="col-12 col-md-6">
            {(id, inv) => <input id={id} className={`form-control ${inv ? 'is-invalid' : ''}`} value={form.name} maxLength={150} onChange={(e) => set('name', e.target.value)} placeholder="Lobby CCR1009" />}
          </Field>
          <Field label="Site" required error={errors.siteId} className="col-12 col-md-6">
            {(id, inv) => <SiteSelect id={id} organizationId={organizationId} value={form.siteId} onChange={(v) => set('siteId', v)} allLabel={null} invalid={inv} />}
          </Field>
          <Field label="IP address" required error={errors.ipAddress} hint="The source IP RADIUS packets come from (often the router’s WAN or VPN address)." className="col-12 col-md-6">
            {(id, inv) => <input id={id} className={`form-control font-monospace ${inv ? 'is-invalid' : ''}`} value={form.ipAddress} maxLength={45} onChange={(e) => set('ipAddress', e.target.value)} placeholder="203.0.113.25" />}
          </Field>
          <Field label="Shared secret" required={editing === 'new'} error={errors.sharedSecret} hint={editing === 'new' ? 'At least 12 characters. Use the same value on the router. Never shown again.' : 'Leave empty to keep the stored secret.'} className="col-12 col-md-6">
            {(id, inv) => <input id={id} type="password" autoComplete="new-password" className={`form-control ${inv ? 'is-invalid' : ''}`} value={form.sharedSecret} maxLength={128} placeholder={editing !== 'new' ? '•••••••• (unchanged)' : ''} onChange={(e) => set('sharedSecret', e.target.value)} />}
          </Field>
          <Field label="Vendor" className="col-6 col-md-3">
            {(id) => <select id={id} className="form-select" value={form.vendor} onChange={(e) => set('vendor', e.target.value)}><option>MikroTik</option><option>Other</option></select>}
          </Field>
          <Field label="RouterOS" className="col-6 col-md-3">
            {(id) => <select id={id} className="form-select" value={form.routerOsVersion} onChange={(e) => set('routerOsVersion', e.target.value)}><option value="v7">v7</option><option value="v6">v6</option><option value="">Not set</option></select>}
          </Field>
          {(['authPort', 'acctPort', 'coaPort'] as const).map((k) => (
            <Field key={k} label={{ authPort: 'Auth port', acctPort: 'Accounting port', coaPort: 'CoA / Disconnect port' }[k]} error={errors[k]} className="col-4 col-md-2">
              {(id, inv) => <input id={id} inputMode="numeric" className={`form-control ${inv ? 'is-invalid' : ''}`} value={form[k]} onChange={(e) => set(k, e.target.value)} />}
            </Field>
          ))}
          <Field label="Description" className="col-12">
            {(id) => <input id={id} className="form-control" value={form.description} maxLength={500} onChange={(e) => set('description', e.target.value)} />}
          </Field>
          <div className="col-12"><Switch label="Active" description="Inactive devices’ RADIUS requests are dropped." checked={form.isActive} onChange={(v) => set('isActive', v)} /></div>
        </form>
      </Modal>

      <Modal open={portal !== null} title="Captive portal key" onClose={() => setPortal(null)} footer={<button type="button" className="btn btn-primary" onClick={() => setPortal(null)}>Done</button>}>
        {portal && (
          <div className="d-grid gap-3">
            <Notice tone="warning">Copy this now. NXT stores only a hash, so the key cannot be shown again.</Notice>
            <div>
              <div className="form-label">Portal URL for the router login page</div>
              <pre className="nxt-code">{portal.portalUrl}</pre>
              <CopyButton text={portal.portalUrl} label="Copy URL" />
            </div>
          </div>
        )}
      </Modal>

      <Modal open={config !== null} size="lg" title={`RouterOS configuration · ${config?.nas.name ?? ''}`} onClose={() => setConfig(null)} footer={<button type="button" className="btn btn-primary" onClick={() => setConfig(null)}>Close</button>}>
        {!config?.data ? <LoadingBlock /> : (
          <div className="d-grid gap-3">
            <pre className="nxt-code">{config.data.script}</pre>
            <div><CopyButton text={config.data.script} label="Copy script" /></div>
            <Notice tone="warning"><ul className="mb-0 ps-3">{config.data.notes.map((n) => <li key={n}>{n}</li>)}</ul></Notice>
          </div>
        )}
      </Modal>
    </Card>
  );
}
