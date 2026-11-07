import { useMemo, useState, type FormEvent } from 'react';
import { FiMessageSquare, FiPlus } from 'react-icons/fi';
import { organizationsApi, sitesApi } from '../api/platform';
import { getErrorMessage } from '../api/errors';
import { Badge, StatusBadge } from '../components/ui/Badge';
import { Card, CardHeader } from '../components/ui/Card';
import { DataTable } from '../components/ui/DataTable';
import { Modal } from '../components/ui/Modal';
import { Notice } from '../components/ui/States';
import { useConfirm } from '../components/ui/useConfirm';
import { useApi } from '../hooks/useApi';
import { useToast } from '../toast/useToast';
import type { SmsConfig } from '../types/models';
import { SmsForm, emptySmsForm, smsFormFrom, validateSmsForm, toSmsInput, type SmsFormState } from './SmsForm';

/** Organization SMS provider plus optional per-site overrides. API keys are write-only. */
export function SmsPanel({ organizationId, canEdit }: { organizationId: string; canEdit: boolean }) {
  const toast = useToast();
  const confirm = useConfirm();
  const configs = useApi((s) => organizationsApi.sms(organizationId, s), [organizationId]);
  const sites = useApi((s) => sitesApi.list(organizationId, s), [organizationId]);
  const siteName = useMemo(() => new Map((sites.data ?? []).map((s) => [s.id, s.name])), [sites.data]);
  const [editing, setEditing] = useState<{ siteId: string | null; existing: SmsConfig | null } | null>(null);
  const [form, setForm] = useState<SmsFormState>(emptySmsForm());
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  const orgConfig = configs.data?.find((c) => c.siteId === null) ?? null;

  const open = (siteId: string | null, existing: SmsConfig | null) => {
    setEditing({ siteId, existing });
    setForm(existing ? smsFormFrom(existing) : emptySmsForm());
    setErrors({});
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!editing) return;
    const errs = validateSmsForm(form, !editing.existing?.hasApiKey);
    setErrors(errs);
    if (Object.keys(errs).length) return;
    setSaving(true);
    try {
      const body = toSmsInput(form);
      if (editing.siteId) await organizationsApi.upsertSiteSms(organizationId, editing.siteId, body);
      else await organizationsApi.upsertSms(organizationId, body);
      toast.success('SMS provider saved. The API key is stored encrypted.');
      setEditing(null);
      configs.reload();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (c: SmsConfig) => {
    const label = c.siteId ? `the override for ${siteName.get(c.siteId) ?? 'this site'}` : 'the organization SMS provider';
    if (!(await confirm({ title: 'Remove SMS configuration', message: `Remove ${label}? OTP messages will fall back to the next available provider.`, confirmLabel: 'Remove' }))) return;
    try {
      if (c.siteId) await organizationsApi.deleteSiteSms(organizationId, c.siteId);
      else await organizationsApi.deleteSms(organizationId);
      toast.success('SMS configuration removed.');
      configs.reload();
    } catch (err) {
      toast.error(getErrorMessage(err));
    }
  };

  const unusedSites = (sites.data ?? []).filter((s) => !configs.data?.some((c) => c.siteId === s.id));

  return (
    <Card>
      <CardHeader
        title="SMS provider"
        subtitle="Used to send OTP codes. Resolution order: site override → organization → platform default."
        icon={<FiMessageSquare />}
        actions={canEdit && (
          <>
            {!orgConfig && <button type="button" className="btn btn-primary btn-sm btn-icon" onClick={() => open(null, null)}><FiPlus /> Configure provider</button>}
            {unusedSites.length > 0 && (
              <select className="form-select form-select-sm" style={{ minWidth: 170 }} value="" onChange={(e) => e.target.value && open(e.target.value, null)} aria-label="Add site override">
                <option value="">+ Site override…</option>
                {unusedSites.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
              </select>
            )}
          </>
        )}
      />
      {!configs.loading && !orgConfig && (
        <div className="px-3 pt-3"><Notice>No organization provider is configured. OTP messages use the platform default provider if the Super Admin has set one.</Notice></div>
      )}
      <DataTable
        rows={configs.data}
        loading={configs.loading}
        error={configs.error}
        onRetry={configs.reload}
        rowKey={(c) => c.smsSettingId}
        empty={{ title: 'No SMS configuration', text: 'Add a provider to send OTP codes from your own sender ID.' }}
        mobileBadge={(c) => <StatusBadge status={c.isActive ? 'Active' : 'Inactive'} />}
        columns={[
          { key: 'scope', header: 'Scope', render: (c) => <span className="nxt-cell-primary">{c.siteId ? `Site: ${siteName.get(c.siteId) ?? '…'}` : 'Organization'}</span> },
          { key: 'provider', header: 'Provider', render: (c) => <div><div>{c.providerName}</div><div className="nxt-cell-secondary text-break">{c.apiEndpoint}</div></div> },
          { key: 'sender', header: 'Sender ID', render: (c) => c.senderId },
          { key: 'key', header: 'API key', render: (c) => c.hasApiKey ? <Badge tone="success">Stored · encrypted</Badge> : <Badge tone="warning">Missing</Badge> },
          { key: 'status', header: 'Status', hideOnMobile: true, render: (c) => <StatusBadge status={c.isActive ? 'Active' : 'Inactive'} /> },
        ]}
        actions={canEdit ? (c) => (
          <>
            <button type="button" className="btn btn-light btn-sm" onClick={() => open(c.siteId, c)}>Edit</button>
            <button type="button" className="btn btn-soft-danger btn-sm" onClick={() => remove(c)}>Remove</button>
          </>
        ) : undefined}
      />
      <Modal
        open={editing !== null}
        size="lg"
        title={editing?.siteId ? `SMS override · ${siteName.get(editing.siteId) ?? ''}` : 'Organization SMS provider'}
        onClose={() => setEditing(null)}
        busy={saving}
        footer={<><button type="button" className="btn btn-light" onClick={() => setEditing(null)} disabled={saving}>Cancel</button><button type="submit" form="sms-form" className="btn btn-primary" disabled={saving}>{saving ? 'Saving…' : 'Save provider'}</button></>}
      >
        <form id="sms-form" onSubmit={submit} noValidate>
          <SmsForm value={form} onChange={setForm} errors={errors} hasStoredKey={!!editing?.existing?.hasApiKey} />
        </form>
      </Modal>
    </Card>
  );
}
