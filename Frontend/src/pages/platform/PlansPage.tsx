import { useState, type FormEvent } from 'react';
import { FiLayers, FiPlus } from 'react-icons/fi';
import { platformApi } from '../../api/platform';
import { getErrorMessage } from '../../api/errors';
import { useAuth } from '../../auth/useAuth';
import { PageHeader } from '../../components/common/PageHeader';
import { ActiveBadge } from '../../components/ui/Badge';
import { Card } from '../../components/ui/Card';
import { DataTable } from '../../components/ui/DataTable';
import { Field, Switch } from '../../components/ui/Field';
import { Modal } from '../../components/ui/Modal';
import { useApi } from '../../hooks/useApi';
import { useToast } from '../../toast/useToast';
import type { SubscriptionPlanDetail } from '../../types/models';
import { Permissions } from '../../types/permissions';
import { formatMoney, formatNumber } from '../../utils/format';
import { optionalNumber } from '../../utils/validation';

interface Form { name: string; description: string; monthlyPrice: string; maxSites: string; maxNasDevices: string; maxUsers: string; isActive: boolean }
const EMPTY: Form = { name: '', description: '', monthlyPrice: '0', maxSites: '', maxNasDevices: '', maxUsers: '', isActive: true };
const lim = (v: number | null) => (v == null ? 'Unlimited' : formatNumber(v));

export function PlansPage() {
  const { hasPermission } = useAuth();
  const toast = useToast();
  const canWrite = hasPermission(Permissions.SubscriptionPlansWrite);
  const plans = useApi((s) => platformApi.plans(s), []);
  const [editing, setEditing] = useState<SubscriptionPlanDetail | 'new' | null>(null);
  const [form, setForm] = useState<Form>(EMPTY);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  const open = (p: SubscriptionPlanDetail | 'new') => {
    setEditing(p);
    setErrors({});
    setForm(p === 'new' ? EMPTY : { name: p.name, description: p.description ?? '', monthlyPrice: String(p.monthlyPrice), maxSites: p.maxSites?.toString() ?? '', maxNasDevices: p.maxNasDevices?.toString() ?? '', maxUsers: p.maxUsers?.toString() ?? '', isActive: p.isActive });
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!editing) return;
    const errs: Record<string, string> = {};
    const price = Number(form.monthlyPrice);
    if (form.name.trim().length < 2) errs.name = 'Enter a plan name.';
    if (!Number.isFinite(price) || price < 0) errs.monthlyPrice = 'Enter 0 or more.';
    const limits = { maxSites: optionalNumber(form.maxSites), maxNasDevices: optionalNumber(form.maxNasDevices), maxUsers: optionalNumber(form.maxUsers) };
    (Object.keys(limits) as (keyof typeof limits)[]).forEach((k) => { const v = limits[k]; if (v !== null && (!Number.isInteger(v) || v < 1)) errs[k] = 'Whole number ≥ 1, or empty for unlimited.'; });
    setErrors(errs);
    if (Object.keys(errs).length) return;
    setSaving(true);
    try {
      const body = { name: form.name.trim(), description: form.description.trim() || null, monthlyPrice: price, ...limits, isActive: form.isActive };
      if (editing === 'new') await platformApi.createPlan(body);
      else await platformApi.updatePlan(editing.id, body);
      toast.success('Plan saved.');
      setEditing(null);
      plans.reload();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const numField = (k: keyof Form, label: string, hint?: string) => (
    <Field label={label} error={errors[k]} hint={hint} className="col-12 col-sm-6 col-md-4">
      {(id, inv) => <input id={id} inputMode="numeric" className={`form-control ${inv ? 'is-invalid' : ''}`} value={form[k] as string} placeholder="Unlimited" onChange={(e) => setForm({ ...form, [k]: e.target.value })} />}
    </Field>
  );

  return (
    <>
      <PageHeader title="Subscription plans" subtitle="Commercial packages that cap sites, NAS devices and subscribers per organization." actions={canWrite && <button type="button" className="btn btn-primary btn-icon" onClick={() => open('new')}><FiPlus /> New plan</button>} />
      <Card>
        <DataTable
          rows={plans.data}
          loading={plans.loading}
          error={plans.error}
          onRetry={plans.reload}
          rowKey={(p) => p.id}
          onRowClick={canWrite ? open : undefined}
          empty={{ title: 'No plans yet', icon: <FiLayers /> }}
          mobileBadge={(p) => <ActiveBadge active={p.isActive} />}
          columns={[
            { key: 'name', header: 'Plan', render: (p) => <div><div className="nxt-cell-primary">{p.name}</div><div className="nxt-cell-secondary">{p.description ?? ''}</div></div> },
            { key: 'price', header: 'Monthly', render: (p) => <span className="font-tabular">{formatMoney(p.monthlyPrice)}</span> },
            { key: 'sites', header: 'Sites', render: (p) => lim(p.maxSites) },
            { key: 'nas', header: 'NAS', render: (p) => lim(p.maxNasDevices) },
            { key: 'users', header: 'Subscribers', render: (p) => lim(p.maxUsers) },
            { key: 'orgs', header: 'Organizations', render: (p) => formatNumber(p.organizationCount) },
            { key: 'status', header: 'Status', hideOnMobile: true, render: (p) => <ActiveBadge active={p.isActive} /> },
          ]}
          actions={canWrite ? (p) => <button type="button" className="btn btn-light btn-sm" onClick={() => open(p)}>Edit</button> : undefined}
        />
      </Card>
      <Modal open={editing !== null} title={editing === 'new' ? 'New subscription plan' : 'Edit plan'} onClose={() => setEditing(null)} busy={saving}
        footer={<><button type="button" className="btn btn-light" onClick={() => setEditing(null)} disabled={saving}>Cancel</button><button type="submit" form="plan-form" className="btn btn-primary" disabled={saving}>{saving ? 'Saving…' : 'Save plan'}</button></>}>
        <form id="plan-form" onSubmit={submit} noValidate className="row g-3">
          <Field label="Plan name" required error={errors.name} className="col-12 col-md-7">
            {(id, inv) => <input id={id} className={`form-control ${inv ? 'is-invalid' : ''}`} value={form.name} maxLength={100} onChange={(e) => setForm({ ...form, name: e.target.value })} />}
          </Field>
          <Field label="Monthly price (INR)" required error={errors.monthlyPrice} className="col-12 col-md-5">
            {(id, inv) => <input id={id} inputMode="decimal" className={`form-control ${inv ? 'is-invalid' : ''}`} value={form.monthlyPrice} onChange={(e) => setForm({ ...form, monthlyPrice: e.target.value })} />}
          </Field>
          <Field label="Description" className="col-12">
            {(id) => <input id={id} className="form-control" value={form.description} maxLength={500} onChange={(e) => setForm({ ...form, description: e.target.value })} />}
          </Field>
          {numField('maxSites', 'Max sites')}
          {numField('maxNasDevices', 'Max NAS devices')}
          {numField('maxUsers', 'Max subscribers')}
          <div className="col-12"><Switch label="Plan is active" description="Inactive plans cannot be assigned to organizations." checked={form.isActive} onChange={(v) => setForm({ ...form, isActive: v })} /></div>
        </form>
      </Modal>
    </>
  );
}
