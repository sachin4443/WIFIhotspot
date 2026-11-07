import { useEffect, useState, type FormEvent } from 'react';
import { organizationsApi } from '../api/platform';
import { getErrorMessage } from '../api/errors';
import { Card, CardBody, CardHeader } from '../components/ui/Card';
import { Field } from '../components/ui/Field';
import { ErrorState, LoadingBlock } from '../components/ui/States';
import { useApi } from '../hooks/useApi';
import { useToast } from '../toast/useToast';

interface Form { otpExpirySeconds: string; otpResendCooldownSeconds: string; otpMaxAttempts: string; sessionValidityHours: string; defaultMaxDevices: string; defaultDataLimitMb: string; timeZoneId: string }

const TIME_ZONES = ['Asia/Kolkata', 'Asia/Dubai', 'Asia/Singapore', 'Asia/Kathmandu', 'Asia/Dhaka', 'Asia/Colombo', 'Europe/London', 'Europe/Berlin', 'America/New_York', 'America/Los_Angeles', 'Australia/Sydney', 'UTC'];

/** OTP and hotspot session defaults for an organization (OrganizationSettings). */
export function SettingsPanel({ organizationId, canEdit }: { organizationId: string; canEdit: boolean }) {
  const toast = useToast();
  const { data, loading, error, reload } = useApi((s) => organizationsApi.settings(organizationId, s), [organizationId]);
  const [form, setForm] = useState<Form | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (data) setForm({
      otpExpirySeconds: String(data.otpExpirySeconds), otpResendCooldownSeconds: String(data.otpResendCooldownSeconds), otpMaxAttempts: String(data.otpMaxAttempts),
      sessionValidityHours: String(+(data.sessionValiditySeconds / 3600).toFixed(2)), defaultMaxDevices: String(data.defaultMaxDevices),
      defaultDataLimitMb: data.defaultDataLimitMb == null ? '' : String(data.defaultDataLimitMb), timeZoneId: data.timeZoneId,
    });
  }, [data]);

  if (error) return <Card><ErrorState message={error} onRetry={reload} /></Card>;
  if (loading || !form) return <Card><LoadingBlock /></Card>;

  const set = (k: keyof Form, v: string) => setForm({ ...form, [k]: v });
  const intIn = (v: string, min: number, max: number) => /^\d+$/.test(v) && +v >= min && +v <= max;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const errs: Record<string, string> = {};
    if (!intIn(form.otpExpirySeconds, 30, 3600)) errs.otpExpirySeconds = '30–3600 seconds.';
    if (!intIn(form.otpResendCooldownSeconds, 0, 3600)) errs.otpResendCooldownSeconds = '0–3600 seconds.';
    if (!intIn(form.otpMaxAttempts, 1, 20)) errs.otpMaxAttempts = '1–20 attempts.';
    const hours = Number(form.sessionValidityHours);
    if (!Number.isFinite(hours) || hours * 3600 < 60 || hours * 3600 > 31_536_000) errs.sessionValidityHours = 'Between 1 minute and 365 days.';
    if (!intIn(form.defaultMaxDevices, 1, 50)) errs.defaultMaxDevices = '1–50 devices.';
    if (form.defaultDataLimitMb && !intIn(form.defaultDataLimitMb, 1, 10_000_000)) errs.defaultDataLimitMb = 'Whole number of MB, or leave empty for unlimited.';
    setErrors(errs);
    if (Object.keys(errs).length) return;
    setSaving(true);
    try {
      await organizationsApi.updateSettings(organizationId, {
        otpExpirySeconds: +form.otpExpirySeconds, otpResendCooldownSeconds: +form.otpResendCooldownSeconds, otpMaxAttempts: +form.otpMaxAttempts,
        sessionValiditySeconds: Math.round(hours * 3600), defaultMaxDevices: +form.defaultMaxDevices,
        defaultDataLimitMb: form.defaultDataLimitMb ? +form.defaultDataLimitMb : null, timeZoneId: form.timeZoneId,
      });
      toast.success('Settings saved.');
      reload();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const num = (k: keyof Form, label: string, hint: string, suffix: string) => (
    <Field label={label} error={errors[k]} hint={hint} className="col-12 col-sm-6 col-lg-4">
      {(id, inv) => (
        <div className="input-group">
          <input id={id} inputMode="decimal" className={`form-control ${inv ? 'is-invalid' : ''}`} value={form[k]} onChange={(e) => set(k, e.target.value)} />
          <span className="input-group-text">{suffix}</span>
        </div>
      )}
    </Field>
  );

  return (
    <Card>
      <CardHeader title="Authentication & session settings" subtitle="OTP rules and default hotspot session limits for this organization." />
      <form onSubmit={submit} noValidate>
        <CardBody>
          <fieldset disabled={!canEdit || saving}>
            <div className="nxt-form-section">
              <div className="nxt-form-section-title">One-time passwords</div>
              <div className="nxt-form-section-desc">Applies to Mobile OTP, Student OTP and Guest OTP sign-in.</div>
              <div className="row g-3">
                {num('otpExpirySeconds', 'OTP validity', 'How long a code stays valid.', 'sec')}
                {num('otpResendCooldownSeconds', 'Resend cooldown', 'Minimum wait before another SMS.', 'sec')}
                {num('otpMaxAttempts', 'Max attempts', 'Wrong entries before the code is void.', 'tries')}
              </div>
            </div>
            <div className="nxt-form-section">
              <div className="nxt-form-section-title">Session defaults</div>
              <div className="nxt-form-section-desc">Used when a subscriber's package does not set its own limits.</div>
              <div className="row g-3">
                {num('sessionValidityHours', 'Session validity', 'Access duration after sign-in.', 'hours')}
                {num('defaultMaxDevices', 'Devices per user', 'Simultaneous devices.', 'devices')}
                {num('defaultDataLimitMb', 'Data limit', 'Empty = unlimited.', 'MB')}
                <Field label="Time zone" className="col-12 col-sm-6 col-lg-4">
                  {(id) => (
                    <select id={id} className="form-select" value={form.timeZoneId} onChange={(e) => set('timeZoneId', e.target.value)}>
                      {[...new Set([form.timeZoneId, ...TIME_ZONES])].map((z) => <option key={z} value={z}>{z}</option>)}
                    </select>
                  )}
                </Field>
              </div>
            </div>
          </fieldset>
        </CardBody>
        {canEdit && (
          <div className="nxt-card-footer d-flex justify-content-end">
            <button type="submit" className="btn btn-primary" disabled={saving}>{saving ? 'Saving…' : 'Save settings'}</button>
          </div>
        )}
      </form>
    </Card>
  );
}
