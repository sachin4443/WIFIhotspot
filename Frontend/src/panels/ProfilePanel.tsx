import { useEffect, useState, type FormEvent } from 'react';
import { organizationsApi } from '../api/platform';
import { getErrorMessage } from '../api/errors';
import { Card, CardBody, CardHeader } from '../components/ui/Card';
import { Field } from '../components/ui/Field';
import { ErrorState, LoadingBlock } from '../components/ui/States';
import { useApi } from '../hooks/useApi';
import { useToast } from '../toast/useToast';
import type { OrganizationProfile } from '../types/models';
import { isEmail, isMobile } from '../utils/validation';

export function ProfilePanel({ organizationId, canEdit, onSaved }: { organizationId: string; canEdit: boolean; onSaved?: () => void }) {
  const toast = useToast();
  const { data, loading, error, reload } = useApi((s) => organizationsApi.profile(organizationId, s), [organizationId]);
  const [form, setForm] = useState<Omit<OrganizationProfile, 'id'> | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  useEffect(() => { if (data) setForm({ name: data.name, contactPerson: data.contactPerson, mobileNumber: data.mobileNumber, email: data.email, address: data.address }); }, [data]);

  if (error) return <Card><ErrorState message={error} onRetry={reload} /></Card>;
  if (loading || !form) return <Card><LoadingBlock /></Card>;

  const set = (k: keyof typeof form, v: string) => setForm({ ...form, [k]: v });
  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const errs: Record<string, string> = {};
    if (form.name.trim().length < 2) errs.name = 'Enter the organization name.';
    if (!form.contactPerson.trim()) errs.contactPerson = 'Enter a contact person.';
    if (!isMobile(form.mobileNumber)) errs.mobileNumber = 'Enter 10–15 digits, optionally starting with +.';
    if (!isEmail(form.email)) errs.email = 'Enter a valid email address.';
    setErrors(errs);
    if (Object.keys(errs).length) return;
    setSaving(true);
    try {
      await organizationsApi.updateProfile(organizationId, { ...form, address: form.address?.trim() || null });
      toast.success('Organization profile saved.');
      onSaved?.();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card>
      <CardHeader title="Organization profile" subtitle="Legal name and primary contact." />
      <form onSubmit={submit} noValidate>
        <CardBody>
          <fieldset disabled={!canEdit || saving} className="row g-3">
            <Field label="Organization name" required error={errors.name} className="col-12 col-md-6">
              {(id, inv) => <input id={id} className={`form-control ${inv ? 'is-invalid' : ''}`} value={form.name} maxLength={200} onChange={(e) => set('name', e.target.value)} />}
            </Field>
            <Field label="Contact person" required error={errors.contactPerson} className="col-12 col-md-6">
              {(id, inv) => <input id={id} className={`form-control ${inv ? 'is-invalid' : ''}`} value={form.contactPerson} maxLength={150} onChange={(e) => set('contactPerson', e.target.value)} />}
            </Field>
            <Field label="Mobile number" required error={errors.mobileNumber} className="col-12 col-md-6">
              {(id, inv) => <input id={id} type="tel" inputMode="tel" className={`form-control ${inv ? 'is-invalid' : ''}`} value={form.mobileNumber} onChange={(e) => set('mobileNumber', e.target.value)} />}
            </Field>
            <Field label="Email" required error={errors.email} className="col-12 col-md-6">
              {(id, inv) => <input id={id} type="email" className={`form-control ${inv ? 'is-invalid' : ''}`} value={form.email} maxLength={254} onChange={(e) => set('email', e.target.value)} />}
            </Field>
            <Field label="Address" className="col-12">
              {(id) => <textarea id={id} className="form-control" rows={2} maxLength={500} value={form.address ?? ''} onChange={(e) => set('address', e.target.value)} />}
            </Field>
          </fieldset>
        </CardBody>
        {canEdit && (
          <div className="nxt-card-footer d-flex justify-content-end">
            <button type="submit" className="btn btn-primary" disabled={saving}>{saving ? 'Saving…' : 'Save profile'}</button>
          </div>
        )}
      </form>
    </Card>
  );
}
