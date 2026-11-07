import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router';
import { FiArrowLeft, FiArrowRight, FiCheck } from 'react-icons/fi';
import { organizationsApi, platformApi, referenceApi } from '../../api/platform';
import { getErrorMessage } from '../../api/errors';
import { PageHeader } from '../../components/common/PageHeader';
import { Badge } from '../../components/ui/Badge';
import { Card, CardBody } from '../../components/ui/Card';
import { Field, Switch } from '../../components/ui/Field';
import { ErrorState, LoadingBlock, Notice } from '../../components/ui/States';
import { useApi } from '../../hooks/useApi';
import { useToast } from '../../toast/useToast';
import { AUTH_TYPES, authLabel, type AuthenticationType, type OrganizationStatus } from '../../types/models';
import { formatMoney } from '../../utils/format';
import { isEmail, isMobile, isUsername, isUrl, passwordError } from '../../utils/validation';
import { SmsForm, emptySmsForm, toSmsInput, validateSmsForm, type SmsFormState } from '../../panels/SmsForm';

const STEPS = ['Organization type', 'Details', 'Administrator', 'Plan & wallet', 'Login methods', 'SMS provider', 'Review'] as const;

interface Form {
  organizationTypeId: number | null;
  organizationName: string; contactPerson: string; mobileNumber: string; email: string; address: string; primarySiteName: string; logoUrl: string;
  adminUsername: string; adminPassword: string; adminPasswordConfirm: string;
  subscriptionPlanId: string; initialWalletBalance: string; status: OrganizationStatus;
  loginMethods: AuthenticationType[];
  configureSms: boolean; sms: SmsFormState;
}

const INITIAL: Form = {
  organizationTypeId: null, organizationName: '', contactPerson: '', mobileNumber: '', email: '', address: '', primarySiteName: '', logoUrl: '',
  adminUsername: '', adminPassword: '', adminPasswordConfirm: '', subscriptionPlanId: '', initialWalletBalance: '0', status: 'Active',
  loginMethods: [], configureSms: false, sms: emptySmsForm(),
};

export function RegisterOrganizationPage() {
  const navigate = useNavigate();
  const toast = useToast();
  const [step, setStep] = useState(0);
  const [form, setForm] = useState<Form>(INITIAL);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);
  const types = useApi((s) => referenceApi.organizationTypes(false, s), []);
  const plans = useApi((s) => referenceApi.subscriptionPlans(s), []);
  const typeDetail = useApi((s) => platformApi.organizationType(form.organizationTypeId ?? 0, s), [form.organizationTypeId], form.organizationTypeId !== null);

  // Pre-select the type's default login methods whenever the type changes.
  useEffect(() => {
    if (typeDetail.data) setForm((f) => ({ ...f, loginMethods: typeDetail.data!.defaultLoginMethods.map((m) => m.authenticationType) }));
  }, [typeDetail.data]);

  const set = <K extends keyof Form>(k: K, v: Form[K]) => setForm((f) => ({ ...f, [k]: v }));
  const typeName = types.data?.find((t) => t.organizationTypeId === form.organizationTypeId)?.typeName;
  const plan = plans.data?.find((p) => p.id === form.subscriptionPlanId);

  const validate = (s: number): Record<string, string> => {
    const e: Record<string, string> = {};
    if (s === 0 && form.organizationTypeId === null) e.type = 'Choose the type of organization.';
    if (s === 1) {
      if (form.organizationName.trim().length < 2) e.organizationName = 'Enter the organization name.';
      if (!form.contactPerson.trim()) e.contactPerson = 'Enter a contact person.';
      if (!isMobile(form.mobileNumber)) e.mobileNumber = '10–15 digits, optionally starting with +.';
      if (!isEmail(form.email)) e.email = 'Enter a valid email address.';
      if (form.logoUrl && !isUrl(form.logoUrl)) e.logoUrl = 'Enter a full http(s) URL.';
    }
    if (s === 2) {
      if (!isUsername(form.adminUsername)) e.adminUsername = '3–50 letters, digits, . _ @ or -.';
      const pe = passwordError(form.adminPassword);
      if (pe) e.adminPassword = pe;
      else if (form.adminPassword !== form.adminPasswordConfirm) e.adminPasswordConfirm = 'Passwords do not match.';
    }
    if (s === 3) {
      const b = Number(form.initialWalletBalance || '0');
      if (!Number.isFinite(b) || b < 0 || b > 10_000_000) e.initialWalletBalance = 'Between 0 and 10,000,000.';
    }
    if (s === 4 && form.loginMethods.length === 0) e.loginMethods = 'Select at least one login method.';
    if (s === 5 && form.configureSms) Object.assign(e, validateSmsForm(form.sms, true));
    return e;
  };

  const next = () => {
    const e = validate(step);
    setErrors(e);
    if (Object.keys(e).length === 0) setStep((s) => Math.min(s + 1, STEPS.length - 1));
  };

  const submit = async () => {
    for (let s = 0; s < STEPS.length - 1; s++) {
      const e = validate(s);
      if (Object.keys(e).length) { setErrors(e); setStep(s); return; }
    }
    setSubmitting(true);
    try {
      const created = await organizationsApi.register({
        organizationName: form.organizationName.trim(), organizationTypeId: form.organizationTypeId!, contactPerson: form.contactPerson.trim(),
        mobileNumber: form.mobileNumber.trim(), email: form.email.trim(), address: form.address.trim() || null, primarySiteName: form.primarySiteName.trim() || null,
        adminUsername: form.adminUsername.trim(), adminPassword: form.adminPassword, subscriptionPlanId: form.subscriptionPlanId || null,
        initialWalletBalance: Number(form.initialWalletBalance || '0'), defaultLoginMethods: form.loginMethods,
        sms: form.configureSms ? { ...toSmsInput(form.sms), apiKey: form.sms.apiKey.trim(), providerName: form.sms.providerName || '2Factor', senderId: form.sms.senderId.trim() } : null,
        logoUrl: form.logoUrl.trim() || null, status: form.status,
      });
      toast.success(`${created.name} registered. Administrator: ${created.adminUsername}.`, { title: 'Organization created' });
      navigate(`/organizations/${created.id}`, { replace: true });
    } catch (err) {
      toast.error(getErrorMessage(err), { title: 'Registration failed' });
    } finally {
      setSubmitting(false);
    }
  };

  const toggleMethod = (m: AuthenticationType) => set('loginMethods', form.loginMethods.includes(m) ? form.loginMethods.filter((x) => x !== m) : [...form.loginMethods, m]);
  const typeDefaults = useMemo(() => new Set(typeDetail.data?.defaultLoginMethods.map((m) => m.authenticationType) ?? []), [typeDetail.data]);

  const input = (k: keyof Form, label: string, opts: { required?: boolean; type?: string; hint?: string; col?: string; max?: number; auto?: string } = {}) => (
    <Field label={label} required={opts.required} error={errors[k as string]} hint={opts.hint} className={opts.col ?? 'col-12 col-md-6'}>
      {(id, inv) => <input id={id} type={opts.type ?? 'text'} autoComplete={opts.auto} className={`form-control ${inv ? 'is-invalid' : ''}`} maxLength={opts.max} value={form[k] as string} onChange={(e) => set(k, e.target.value as never)} />}
    </Field>
  );

  return (
    <>
      <PageHeader title="Register organization" subtitle="Creates the organization, its primary site and wallet, the first administrator, login methods and SMS provider in one step." breadcrumbs={[{ label: 'Organizations', to: '/organizations' }, { label: 'Register' }]} />
      <div className="nxt-steps" aria-label="Progress">
        {STEPS.map((s, i) => (
          <button key={s} type="button" className={`nxt-step ${i === step ? 'is-active' : i < step ? 'is-done' : ''}`} onClick={() => i < step && setStep(i)} disabled={i > step} aria-current={i === step ? 'step' : undefined}>
            <span className="nxt-step-num">{i < step ? <FiCheck /> : i + 1}</span>{s}
          </button>
        ))}
      </div>

      <Card>
        <CardBody>
          {step === 0 && (
            types.error ? <ErrorState message={types.error} onRetry={types.reload} /> : !types.data ? <LoadingBlock /> : (
              <>
                <h2 className="h6 fw-semibold mb-1">What kind of organization is this?</h2>
                <p className="text-muted-2 fs-sm mb-3">The type decides the default hotspot login methods and subscriber workflow.</p>
                {errors.type && <div className="text-danger fs-sm mb-2">{errors.type}</div>}
                <div className="row g-2">
                  {types.data.map((t) => (
                    <div key={t.organizationTypeId} className="col-12 col-sm-6 col-lg-4 col-xxl-3">
                      <button type="button" className={`nxt-choice ${form.organizationTypeId === t.organizationTypeId ? 'is-selected' : ''}`} onClick={() => set('organizationTypeId', t.organizationTypeId)} aria-pressed={form.organizationTypeId === t.organizationTypeId}>
                        <span><span className="nxt-choice-title d-block">{t.typeName}</span><span className="nxt-choice-desc">{t.description}</span></span>
                      </button>
                    </div>
                  ))}
                </div>
              </>
            )
          )}

          {step === 1 && (
            <div className="row g-3">
              {input('organizationName', 'Organization name', { required: true, max: 200 })}
              {input('contactPerson', 'Contact person', { required: true, max: 150 })}
              {input('mobileNumber', 'Mobile number', { required: true, type: 'tel' })}
              {input('email', 'Email', { required: true, type: 'email', max: 254 })}
              {input('primarySiteName', 'Primary site name', { hint: 'Defaults to "Main".', max: 150 })}
              {input('logoUrl', 'Logo URL', { type: 'url', max: 500 })}
              <Field label="Address" className="col-12">
                {(id) => <textarea id={id} rows={2} className="form-control" maxLength={500} value={form.address} onChange={(e) => set('address', e.target.value)} />}
              </Field>
            </div>
          )}

          {step === 2 && (
            <div className="row g-3">
              <div className="col-12"><Notice>The first administrator gets the Org Admin role with access to all sites. They can add more admins from their own portal.</Notice></div>
              {input('adminUsername', 'Admin username', { required: true, max: 50, auto: 'off' })}
              <div className="col-12 col-md-6" />
              {input('adminPassword', 'Password', { required: true, type: 'password', hint: '10+ characters with upper, lower and a digit.', max: 128, auto: 'new-password' })}
              {input('adminPasswordConfirm', 'Confirm password', { required: true, type: 'password', max: 128, auto: 'new-password' })}
            </div>
          )}

          {step === 3 && (
            <div className="row g-3">
              <Field label="Subscription plan" hint="Limits the number of sites, NAS devices and users." className="col-12 col-md-6">
                {(id) => (
                  <select id={id} className="form-select" value={form.subscriptionPlanId} onChange={(e) => set('subscriptionPlanId', e.target.value)}>
                    <option value="">No plan (unlimited)</option>
                    {plans.data?.map((p) => <option key={p.id} value={p.id}>{p.name} · {formatMoney(p.monthlyPrice)}/month</option>)}
                  </select>
                )}
              </Field>
              {input('initialWalletBalance', 'Opening wallet balance (INR)', { hint: 'Credited to the primary site wallet.' })}
              <Field label="Initial status" className="col-12 col-md-6">
                {(id) => (
                  <select id={id} className="form-select" value={form.status} onChange={(e) => set('status', e.target.value as OrganizationStatus)}>
                    <option value="Active">Active – admins can sign in now</option>
                    <option value="Pending">Pending – activate later</option>
                  </select>
                )}
              </Field>
              {plan && (
                <div className="col-12">
                  <div className="d-flex flex-wrap gap-2">
                    <Badge tone="info">{plan.maxSites ?? '∞'} sites</Badge>
                    <Badge tone="info">{plan.maxNasDevices ?? '∞'} NAS devices</Badge>
                    <Badge tone="info">{plan.maxUsers?.toLocaleString() ?? '∞'} users</Badge>
                  </div>
                </div>
              )}
            </div>
          )}

          {step === 4 && (
            <>
              <h2 className="h6 fw-semibold mb-1">Hotspot login methods</h2>
              <p className="text-muted-2 fs-sm mb-3">Pre-selected from the {typeName} defaults. The organization admin can refine these later, including per site.</p>
              {errors.loginMethods && <div className="text-danger fs-sm mb-2">{errors.loginMethods}</div>}
              <div className="row g-2">
                {AUTH_TYPES.map((t) => (
                  <div key={t.value} className="col-12 col-md-6 col-xl-4">
                    <label className={`nxt-choice ${form.loginMethods.includes(t.value) ? 'is-selected' : ''}`}>
                      <input type="checkbox" className="form-check-input mt-1" checked={form.loginMethods.includes(t.value)} onChange={() => toggleMethod(t.value)} />
                      <span><span className="nxt-choice-title d-block">{t.label} {typeDefaults.has(t.value) && <Badge>Type default</Badge>}</span><span className="nxt-choice-desc">{t.description}</span></span>
                    </label>
                  </div>
                ))}
              </div>
            </>
          )}

          {step === 5 && (
            <>
              <Switch label="Configure the organization's own SMS provider now" description="Otherwise OTP messages use the platform default provider (if configured). API keys are encrypted and never shown again." checked={form.configureSms} onChange={(v) => set('configureSms', v)} />
              {form.configureSms && <div className="mt-3"><SmsForm value={form.sms} onChange={(v) => set('sms', v)} errors={errors} hasStoredKey={false} /></div>}
            </>
          )}

          {step === 6 && (
            <div className="row g-3">
              <div className="col-12 col-lg-6">
                <h3 className="h6 fw-semibold">Organization</h3>
                <dl className="nxt-dl">
                  <dt>Name</dt><dd>{form.organizationName}</dd>
                  <dt>Type</dt><dd>{typeName}</dd>
                  <dt>Contact</dt><dd>{form.contactPerson} · {form.mobileNumber} · {form.email}</dd>
                  <dt>Primary site</dt><dd>{form.primarySiteName || 'Main'}</dd>
                  <dt>Status</dt><dd>{form.status}</dd>
                </dl>
              </div>
              <div className="col-12 col-lg-6">
                <h3 className="h6 fw-semibold">Access &amp; billing</h3>
                <dl className="nxt-dl">
                  <dt>Administrator</dt><dd>{form.adminUsername}</dd>
                  <dt>Plan</dt><dd>{plan?.name ?? 'No plan'}</dd>
                  <dt>Opening balance</dt><dd>{formatMoney(Number(form.initialWalletBalance || 0))}</dd>
                  <dt>Login methods</dt><dd>{form.loginMethods.map(authLabel).join(', ')}</dd>
                  <dt>SMS</dt><dd>{form.configureSms ? `${form.sms.providerName} · ${form.sms.senderId}` : 'Platform default'}</dd>
                </dl>
              </div>
            </div>
          )}
        </CardBody>
        <div className="nxt-card-footer d-flex flex-wrap justify-content-between gap-2">
          <button type="button" className="btn btn-light btn-icon" onClick={() => (step === 0 ? navigate('/organizations') : setStep(step - 1))} disabled={submitting}><FiArrowLeft /> {step === 0 ? 'Cancel' : 'Back'}</button>
          {step < STEPS.length - 1
            ? <button type="button" className="btn btn-primary btn-icon" onClick={next}>Continue <FiArrowRight /></button>
            : <button type="button" className="btn btn-primary btn-icon" onClick={submit} disabled={submitting}>{submitting ? 'Registering…' : <><FiCheck /> Register organization</>}</button>}
        </div>
      </Card>
    </>
  );
}
