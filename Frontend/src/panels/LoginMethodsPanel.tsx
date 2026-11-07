import { useMemo, useState } from 'react';
import { FiCheck, FiKey } from 'react-icons/fi';
import { organizationsApi, sitesApi } from '../api/platform';
import { getErrorMessage } from '../api/errors';
import { Badge } from '../components/ui/Badge';
import { Card, CardBody, CardHeader } from '../components/ui/Card';
import { Switch } from '../components/ui/Field';
import { Modal } from '../components/ui/Modal';
import { ErrorState, LoadingBlock, Notice } from '../components/ui/States';
import { useConfirm } from '../components/ui/useConfirm';
import { useApi } from '../hooks/useApi';
import { useToast } from '../toast/useToast';
import { AUTH_TYPES, authLabel, type AuthenticationType, type LoginMethod, type UpsertLoginMethodInput } from '../types/models';

const OTP_CAPABLE: AuthenticationType[] = ['MobileOTP', 'StudentOTP', 'GuestOTP', 'StudentPassword', 'StaffPassword', 'EmployeePassword', 'UsernamePassword', 'RoomMobile'];

/**
 * Login methods offered on the captive portal. Organization-level methods apply to every site
 * unless a site defines its own overrides.
 */
export function LoginMethodsPanel({ organizationId, canEdit }: { organizationId: string; canEdit: boolean }) {
  const toast = useToast();
  const confirm = useConfirm();
  const [siteId, setSiteId] = useState('');
  const sites = useApi((s) => sitesApi.list(organizationId, s), [organizationId]);
  const methods = useApi((s) => (siteId ? organizationsApi.siteLoginMethods(organizationId, siteId, s) : organizationsApi.loginMethods(organizationId, s)), [organizationId, siteId]);
  const [editing, setEditing] = useState<UpsertLoginMethodInput | null>(null);
  const [saving, setSaving] = useState(false);

  const byType = useMemo(() => new Map((methods.data ?? []).map((m) => [m.authenticationType, m])), [methods.data]);

  const openEditor = (type: AuthenticationType, existing?: LoginMethod) =>
    setEditing(existing
      ? { authenticationType: type, isEnabled: existing.isEnabled, isDefault: existing.isDefault, requireOTP: existing.requireOTP, requireBookingValidation: existing.requireBookingValidation, allowMultipleDevices: existing.allowMultipleDevices, maxDevices: existing.maxDevices }
      : { authenticationType: type, isEnabled: true, isDefault: (methods.data ?? []).length === 0, requireOTP: ['MobileOTP', 'StudentOTP', 'GuestOTP'].includes(type), requireBookingValidation: type === 'RoomMobile', allowMultipleDevices: true, maxDevices: 3 });

  const save = async () => {
    if (!editing) return;
    if (!Number.isInteger(editing.maxDevices) || editing.maxDevices < 1 || editing.maxDevices > 50) { toast.error('Devices per user must be 1–50.'); return; }
    setSaving(true);
    try {
      if (siteId) await organizationsApi.upsertSiteLoginMethod(organizationId, siteId, editing);
      else await organizationsApi.upsertLoginMethod(organizationId, editing);
      toast.success(`${authLabel(editing.authenticationType)} saved.`);
      setEditing(null);
      methods.reload();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (m: LoginMethod) => {
    if (!(await confirm({ title: 'Remove login method', message: `Remove ${authLabel(m.authenticationType)}${siteId ? ' from this site' : ''}? Visitors will no longer see it.`, confirmLabel: 'Remove' }))) return;
    try {
      await organizationsApi.deleteLoginMethod(organizationId, m.loginMethodId);
      toast.success('Login method removed.');
      methods.reload();
    } catch (err) {
      toast.error(getErrorMessage(err));
    }
  };

  return (
    <Card>
      <CardHeader
        title="Hotspot login methods"
        subtitle="Choose how visitors sign in on the captive portal."
        icon={<FiKey />}
        actions={
          <select className="form-select form-select-sm" style={{ minWidth: 200 }} value={siteId} onChange={(e) => setSiteId(e.target.value)} aria-label="Scope">
            <option value="">Organization default (all sites)</option>
            {sites.data?.map((s) => <option key={s.id} value={s.id}>Site override: {s.name}</option>)}
          </select>
        }
      />
      <CardBody>
        {siteId && (
          <Notice className="mb-3">
            {methods.data && methods.data.length === 0
              ? 'This site has no overrides, so it uses the organization defaults. Enabling a method here creates a site-specific list.'
              : 'This site uses its own list instead of the organization defaults.'}
          </Notice>
        )}
        {methods.error ? <ErrorState message={methods.error} onRetry={methods.reload} /> : methods.loading && !methods.data ? <LoadingBlock /> : (
          <div className="row g-3">
            {AUTH_TYPES.map((t) => {
              const m = byType.get(t.value);
              return (
                <div key={t.value} className="col-12 col-md-6 col-xxl-4">
                  <div className={`nxt-choice ${m?.isEnabled ? 'is-selected' : ''}`} style={{ cursor: 'default' }}>
                    <div className="min-w-0 flex-grow-1">
                      <div className="d-flex flex-wrap align-items-center gap-2 mb-1">
                        <span className="nxt-choice-title">{t.label}</span>
                        {m?.isDefault && <Badge tone="info">Default</Badge>}
                        {m && !m.isEnabled && <Badge>Disabled</Badge>}
                      </div>
                      <div className="nxt-choice-desc mb-2">{t.description}</div>
                      {m && (
                        <div className="d-flex flex-wrap gap-1 mb-2">
                          {m.requireOTP && <Badge>OTP verification</Badge>}
                          {m.requireBookingValidation && <Badge>Booking check</Badge>}
                          <Badge>{m.allowMultipleDevices ? `${m.maxDevices} devices` : 'Single device'}</Badge>
                        </div>
                      )}
                      {canEdit && (
                        <div className="d-flex flex-wrap gap-2">
                          <button type="button" className={`btn btn-sm ${m ? 'btn-light' : 'btn-outline-primary'}`} onClick={() => openEditor(t.value, m)}>{m ? 'Configure' : 'Enable'}</button>
                          {m && <button type="button" className="btn btn-sm btn-soft-danger" onClick={() => remove(m)}>Remove</button>}
                        </div>
                      )}
                    </div>
                    {m?.isEnabled && <FiCheck className="text-primary flex-shrink-0" aria-label="Enabled" />}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </CardBody>
      <Modal
        open={editing !== null}
        title={editing ? authLabel(editing.authenticationType) : ''}
        subtitle={siteId ? 'Site-specific setting' : 'Organization default'}
        onClose={() => setEditing(null)}
        busy={saving}
        footer={<><button type="button" className="btn btn-light" onClick={() => setEditing(null)} disabled={saving}>Cancel</button><button type="button" className="btn btn-primary" onClick={save} disabled={saving}>{saving ? 'Saving…' : 'Save'}</button></>}
      >
        {editing && (
          <div className="d-grid gap-3">
            <Switch label="Enabled" description="Visible on the captive portal." checked={editing.isEnabled} onChange={(v) => setEditing({ ...editing, isEnabled: v })} />
            <Switch label="Default method" description="Pre-selected when the portal opens." checked={editing.isDefault} onChange={(v) => setEditing({ ...editing, isDefault: v })} />
            {OTP_CAPABLE.includes(editing.authenticationType) && (
              <Switch label="Require SMS OTP" description="Send a one-time password to the subscriber's mobile as a second step." checked={editing.requireOTP} onChange={(v) => setEditing({ ...editing, requireOTP: v })} />
            )}
            {editing.authenticationType === 'RoomMobile' && (
              <Switch label="Validate against guest register" description="Only checked-in guests whose room and mobile match can sign in." checked={editing.requireBookingValidation} onChange={(v) => setEditing({ ...editing, requireBookingValidation: v })} />
            )}
            <Switch label="Allow multiple devices" checked={editing.allowMultipleDevices} onChange={(v) => setEditing({ ...editing, allowMultipleDevices: v, maxDevices: v ? Math.max(editing.maxDevices, 2) : 1 })} />
            <div>
              <label className="form-label" htmlFor="lm-max">Devices per user</label>
              <input id="lm-max" type="number" min={1} max={50} className="form-control" style={{ maxWidth: 140 }} value={editing.maxDevices} disabled={!editing.allowMultipleDevices} onChange={(e) => setEditing({ ...editing, maxDevices: Number(e.target.value) })} />
            </div>
          </div>
        )}
      </Modal>
    </Card>
  );
}
