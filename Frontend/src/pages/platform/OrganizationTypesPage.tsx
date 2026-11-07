import { useState } from 'react';
import { FiGrid, FiPlus } from 'react-icons/fi';
import { platformApi, referenceApi } from '../../api/platform';
import { getErrorMessage } from '../../api/errors';
import { useAuth } from '../../auth/useAuth';
import { PageHeader } from '../../components/common/PageHeader';
import { ActiveBadge, Badge } from '../../components/ui/Badge';
import { Card } from '../../components/ui/Card';
import { DataTable } from '../../components/ui/DataTable';
import { Field, Switch } from '../../components/ui/Field';
import { Modal } from '../../components/ui/Modal';
import { LoadingBlock } from '../../components/ui/States';
import { useApi } from '../../hooks/useApi';
import { useToast } from '../../toast/useToast';
import { AUTH_TYPES, authLabel, type OrganizationType, type OrganizationTypeLoginMethod } from '../../types/models';
import { Permissions } from '../../types/permissions';

interface Form { typeName: string; description: string; isActive: boolean; methods: OrganizationTypeLoginMethod[] }

export function OrganizationTypesPage() {
  const { hasPermission } = useAuth();
  const toast = useToast();
  const canWrite = hasPermission(Permissions.OrganizationTypesWrite);
  const types = useApi((s) => referenceApi.organizationTypes(true, s), []);
  const [editing, setEditing] = useState<OrganizationType | 'new' | null>(null);
  const [form, setForm] = useState<Form | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  const open = async (t: OrganizationType | 'new') => {
    setEditing(t);
    setErrors({});
    if (t === 'new') { setForm({ typeName: '', description: '', isActive: true, methods: [] }); return; }
    setForm(null);
    try {
      const d = await platformApi.organizationType(t.organizationTypeId);
      setForm({ typeName: d.typeName, description: d.description ?? '', isActive: d.isActive, methods: d.defaultLoginMethods });
    } catch (err) {
      toast.error(getErrorMessage(err));
      setEditing(null);
    }
  };

  const toggle = (value: OrganizationTypeLoginMethod['authenticationType']) => {
    if (!form) return;
    const exists = form.methods.some((m) => m.authenticationType === value);
    const methods = exists
      ? form.methods.filter((m) => m.authenticationType !== value)
      : [...form.methods, { authenticationType: value, isDefault: form.methods.length === 0, requireOTP: ['MobileOTP', 'StudentOTP', 'GuestOTP'].includes(value), requireBookingValidation: value === 'RoomMobile', allowMultipleDevices: true, maxDevices: 3 }];
    if (methods.length && !methods.some((m) => m.isDefault)) methods[0] = { ...methods[0], isDefault: true };
    setForm({ ...form, methods });
  };

  const save = async () => {
    if (!form || !editing) return;
    const errs: Record<string, string> = {};
    if (form.typeName.trim().length < 2) errs.typeName = 'Enter a type name.';
    if (form.methods.length === 0) errs.methods = 'Pick at least one default login method.';
    setErrors(errs);
    if (Object.keys(errs).length) return;
    setSaving(true);
    try {
      const body = { typeName: form.typeName.trim(), description: form.description.trim() || null, isActive: form.isActive, defaultLoginMethods: form.methods };
      if (editing === 'new') await platformApi.createOrganizationType(body);
      else await platformApi.updateOrganizationType(editing.organizationTypeId, body);
      toast.success('Organization type saved. Existing organizations keep their own login methods.');
      setEditing(null);
      types.reload();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <PageHeader title="Organization types" subtitle="Business verticals and the hotspot login methods each one starts with." actions={canWrite && <button type="button" className="btn btn-primary btn-icon" onClick={() => open('new')}><FiPlus /> New type</button>} />
      <Card>
        <DataTable
          rows={types.data}
          loading={types.loading}
          error={types.error}
          onRetry={types.reload}
          rowKey={(t) => String(t.organizationTypeId)}
          onRowClick={canWrite ? (t) => open(t) : undefined}
          empty={{ title: 'No organization types', icon: <FiGrid /> }}
          mobileBadge={(t) => <ActiveBadge active={t.isActive} />}
          columns={[
            { key: 'name', header: 'Type', render: (t) => <div className="nxt-cell-primary">{t.typeName}</div> },
            { key: 'desc', header: 'Description', render: (t) => <span className="text-muted-2">{t.description ?? '—'}</span> },
            { key: 'status', header: 'Status', hideOnMobile: true, render: (t) => <ActiveBadge active={t.isActive} /> },
          ]}
          actions={canWrite ? (t) => <button type="button" className="btn btn-light btn-sm" onClick={() => open(t)}>Edit</button> : undefined}
        />
      </Card>
      <Modal
        open={editing !== null}
        size="lg"
        title={editing === 'new' ? 'New organization type' : 'Edit organization type'}
        onClose={() => setEditing(null)}
        busy={saving}
        footer={<><button type="button" className="btn btn-light" onClick={() => setEditing(null)} disabled={saving}>Cancel</button><button type="button" className="btn btn-primary" onClick={save} disabled={saving || !form}>{saving ? 'Saving…' : 'Save type'}</button></>}
      >
        {!form ? <LoadingBlock /> : (
          <div className="row g-3">
            <Field label="Type name" required error={errors.typeName} className="col-12 col-md-6">
              {(id, inv) => <input id={id} className={`form-control ${inv ? 'is-invalid' : ''}`} value={form.typeName} maxLength={100} onChange={(e) => setForm({ ...form, typeName: e.target.value })} />}
            </Field>
            <Field label="Description" className="col-12 col-md-6">
              {(id) => <input id={id} className="form-control" value={form.description} maxLength={500} onChange={(e) => setForm({ ...form, description: e.target.value })} />}
            </Field>
            <div className="col-12"><Switch label="Available for new registrations" checked={form.isActive} onChange={(v) => setForm({ ...form, isActive: v })} /></div>
            <div className="col-12">
              <div className="nxt-form-section-title">Default login methods</div>
              <div className="nxt-form-section-desc">Copied into each new organization of this type. The star marks the default method.</div>
              {errors.methods && <div className="text-danger fs-sm mb-2">{errors.methods}</div>}
              <div className="row g-2">
                {AUTH_TYPES.map((t) => {
                  const m = form.methods.find((x) => x.authenticationType === t.value);
                  return (
                    <div key={t.value} className="col-12 col-md-6">
                      <div className={`nxt-choice ${m ? 'is-selected' : ''}`}>
                        <input type="checkbox" className="form-check-input mt-1" checked={!!m} onChange={() => toggle(t.value)} aria-label={t.label} />
                        <div className="min-w-0 flex-grow-1">
                          <div className="nxt-choice-title">{t.label} {m?.isDefault && <Badge tone="info">Default</Badge>}</div>
                          <div className="nxt-choice-desc">{t.description}</div>
                          {m && !m.isDefault && (
                            <button type="button" className="btn btn-link btn-sm p-0 mt-1" onClick={() => setForm({ ...form, methods: form.methods.map((x) => ({ ...x, isDefault: x.authenticationType === t.value })) })}>Make default</button>
                          )}
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
              {form.methods.length > 0 && <div className="fs-xs text-muted-2 mt-2">Selected: {form.methods.map((m) => authLabel(m.authenticationType)).join(', ')}</div>}
            </div>
          </div>
        )}
      </Modal>
    </>
  );
}
