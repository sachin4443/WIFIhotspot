import { useEffect, useState, type FormEvent } from 'react';
import axios from 'axios';
import { platformApi } from '../../api/platform';
import { getErrorMessage } from '../../api/errors';
import { PageHeader } from '../../components/common/PageHeader';
import { Badge } from '../../components/ui/Badge';
import { Card, CardBody, CardHeader } from '../../components/ui/Card';
import { ErrorState, LoadingBlock, Notice } from '../../components/ui/States';
import { useApi } from '../../hooks/useApi';
import { SmsForm, emptySmsForm, smsFormFrom, toSmsInput, validateSmsForm, type SmsFormState } from '../../panels/SmsForm';
import { useToast } from '../../toast/useToast';
import type { SmsConfig } from '../../types/models';

/** Platform default SMS provider, used by organizations without their own. */
export function PlatformSmsPage() {
  const toast = useToast();
  const sms = useApi(async (s) => {
    try { return await platformApi.sms(s); }
    catch (err) { if (axios.isAxiosError(err) && err.response?.status === 404) return null; throw err; }
  }, []);
  const [form, setForm] = useState<SmsFormState>(emptySmsForm());
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);
  const existing: SmsConfig | null | undefined = sms.data;

  useEffect(() => { if (existing) setForm(smsFormFrom(existing)); }, [existing]);

  if (sms.error) return <Card><ErrorState message={sms.error} onRetry={sms.reload} /></Card>;
  if (sms.loading && sms.data === undefined) return <LoadingBlock />;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const errs = validateSmsForm(form, !existing?.hasApiKey);
    setErrors(errs);
    if (Object.keys(errs).length) return;
    setSaving(true);
    try {
      await platformApi.upsertSms(toSmsInput(form));
      toast.success('Platform SMS provider saved.');
      sms.reload();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <PageHeader title="Platform SMS provider" subtitle="Default OTP sender for organizations that have not configured their own provider." />
      <Card>
        <CardHeader title="Provider configuration" actions={existing ? (existing.hasApiKey ? <Badge tone="success">API key stored · encrypted</Badge> : <Badge tone="warning">API key missing</Badge>) : <Badge tone="warning">Not configured</Badge>} />
        <form onSubmit={submit} noValidate>
          <CardBody>
            {!existing && <Notice className="mb-3">No platform provider is configured yet. Organizations without their own SMS settings cannot send OTPs until one is saved.</Notice>}
            <fieldset disabled={saving}><SmsForm value={form} onChange={setForm} errors={errors} hasStoredKey={!!existing?.hasApiKey} /></fieldset>
          </CardBody>
          <div className="nxt-card-footer d-flex justify-content-end">
            <button type="submit" className="btn btn-primary" disabled={saving}>{saving ? 'Saving…' : 'Save provider'}</button>
          </div>
        </form>
      </Card>
    </>
  );
}
