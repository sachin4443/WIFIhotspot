import { useEffect, useState, type FormEvent } from 'react';
import { FiWifi } from 'react-icons/fi';
import { organizationsApi } from '../api/platform';
import { getErrorMessage } from '../api/errors';
import { Card, CardBody, CardHeader } from '../components/ui/Card';
import { Field } from '../components/ui/Field';
import { ErrorState, LoadingBlock } from '../components/ui/States';
import { useApi } from '../hooks/useApi';
import { useToast } from '../toast/useToast';
import type { OrganizationBranding } from '../types/models';
import { isHexColor, isUrl } from '../utils/validation';

type Form = Omit<OrganizationBranding, 'organizationId'>;

/** Captive-portal branding: logo, colours, welcome text, terms and privacy policy, with a live preview. */
export function BrandingPanel({ organizationId, organizationName, canEdit }: { organizationId: string; organizationName: string; canEdit: boolean }) {
  const toast = useToast();
  const { data, loading, error, reload } = useApi((s) => organizationsApi.branding(organizationId, s), [organizationId]);
  const [form, setForm] = useState<Form | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (data) {
      const { organizationId: _ignored, ...rest } = data;
      void _ignored;
      setForm(rest);
    }
  }, [data]);

  if (error) return <Card><ErrorState message={error} onRetry={reload} /></Card>;
  if (loading || !form) return <Card><LoadingBlock /></Card>;

  const set = (k: keyof Form, v: string) => setForm({ ...form, [k]: v });
  const clean = (v: string | null) => (v && v.trim() ? v.trim() : null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const errs: Record<string, string> = {};
    if (!isHexColor(form.themeColor)) errs.themeColor = 'Use a hex colour like #4f46e5.';
    if (form.logoUrl && !isUrl(form.logoUrl)) errs.logoUrl = 'Enter a full http(s) URL.';
    if (form.backgroundUrl && !isUrl(form.backgroundUrl)) errs.backgroundUrl = 'Enter a full http(s) URL.';
    setErrors(errs);
    if (Object.keys(errs).length) return;
    setSaving(true);
    try {
      await organizationsApi.updateBranding(organizationId, {
        logoUrl: clean(form.logoUrl), welcomeMessage: clean(form.welcomeMessage), themeColor: form.themeColor.trim(),
        backgroundUrl: clean(form.backgroundUrl), termsAndConditions: clean(form.termsAndConditions), privacyPolicy: clean(form.privacyPolicy),
      });
      toast.success('Branding saved.');
      reload();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const color = isHexColor(form.themeColor) ? form.themeColor : '#4f46e5';

  return (
    <div className="row g-3">
      <div className="col-12 col-xl-7">
        <Card>
          <CardHeader title="Captive portal branding" subtitle="Shown to visitors on the hotspot sign-in page." />
          <form onSubmit={submit} noValidate>
            <CardBody>
              <fieldset disabled={!canEdit || saving} className="row g-3">
                <Field label="Logo URL" error={errors.logoUrl} hint="PNG or SVG, ideally square." className="col-12 col-md-8">
                  {(id, inv) => <input id={id} type="url" className={`form-control ${inv ? 'is-invalid' : ''}`} value={form.logoUrl ?? ''} maxLength={500} onChange={(e) => set('logoUrl', e.target.value)} placeholder="https://…" />}
                </Field>
                <Field label="Theme colour" error={errors.themeColor} className="col-12 col-md-4">
                  {(id, inv) => (
                    <div className="input-group">
                      <input type="color" className="form-control form-control-color" style={{ maxWidth: 52 }} value={color} onChange={(e) => set('themeColor', e.target.value)} aria-label="Pick colour" />
                      <input id={id} className={`form-control ${inv ? 'is-invalid' : ''}`} value={form.themeColor} maxLength={7} onChange={(e) => set('themeColor', e.target.value)} />
                    </div>
                  )}
                </Field>
                <Field label="Background image URL" error={errors.backgroundUrl} className="col-12">
                  {(id, inv) => <input id={id} type="url" className={`form-control ${inv ? 'is-invalid' : ''}`} value={form.backgroundUrl ?? ''} maxLength={500} onChange={(e) => set('backgroundUrl', e.target.value)} placeholder="https://…" />}
                </Field>
                <Field label="Welcome message" className="col-12">
                  {(id) => <input id={id} className="form-control" value={form.welcomeMessage ?? ''} maxLength={500} onChange={(e) => set('welcomeMessage', e.target.value)} placeholder="Welcome! Sign in to get online." />}
                </Field>
                <Field label="Terms and conditions" className="col-12">
                  {(id) => <textarea id={id} className="form-control" rows={4} maxLength={20000} value={form.termsAndConditions ?? ''} onChange={(e) => set('termsAndConditions', e.target.value)} />}
                </Field>
                <Field label="Privacy policy" className="col-12">
                  {(id) => <textarea id={id} className="form-control" rows={4} maxLength={20000} value={form.privacyPolicy ?? ''} onChange={(e) => set('privacyPolicy', e.target.value)} />}
                </Field>
              </fieldset>
            </CardBody>
            {canEdit && (
              <div className="nxt-card-footer d-flex justify-content-end">
                <button type="submit" className="btn btn-primary" disabled={saving}>{saving ? 'Saving…' : 'Save branding'}</button>
              </div>
            )}
          </form>
        </Card>
      </div>
      <div className="col-12 col-xl-5">
        <Card>
          <CardHeader title="Preview" subtitle="Approximate sign-in page appearance." />
          <CardBody>
            <div className="border rounded-3 overflow-hidden mx-auto" style={{ maxWidth: 320 }}>
              <div style={{ background: color, height: 96 }} className="d-flex align-items-center justify-content-center">
                {form.logoUrl && isUrl(form.logoUrl)
                  ? <img src={form.logoUrl} alt="" style={{ maxHeight: 56, maxWidth: 160, objectFit: 'contain', background: '#fff', borderRadius: 8, padding: 6 }} />
                  : <span className="d-inline-flex align-items-center justify-content-center bg-white rounded-3" style={{ width: 52, height: 52, color }}><FiWifi size={26} /></span>}
              </div>
              <div className="p-3 text-center">
                <div className="fw-semibold">{organizationName}</div>
                <div className="fs-sm text-muted-2 mb-3">{form.welcomeMessage || 'Welcome! Sign in to get online.'}</div>
                <div className="form-control mb-2 text-start text-muted-2 fs-sm">Mobile number</div>
                <div className="btn w-100 text-white" style={{ background: color }}>Get OTP</div>
                <div className="fs-xs text-muted-2 mt-2">By continuing you accept the terms of use.</div>
              </div>
            </div>
          </CardBody>
        </Card>
      </div>
    </div>
  );
}
