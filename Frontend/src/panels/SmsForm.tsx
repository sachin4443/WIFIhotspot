import { Field, Switch } from '../components/ui/Field';
import type { SmsConfig, UpsertSmsInput } from '../types/models';
import { isHttpsUrl } from '../utils/validation';

export interface SmsFormState { providerName: string; apiEndpoint: string; apiKey: string; senderId: string; messageTemplate: string; isActive: boolean }

export const DEFAULT_TEMPLATE = 'Your WiFi OTP is {OTP}. Valid for {EXPIRY} minutes. Do not share it with anyone.';
export const emptySmsForm = (): SmsFormState => ({ providerName: '2Factor', apiEndpoint: 'https://2factor.in/API/R1/', apiKey: '', senderId: '', messageTemplate: DEFAULT_TEMPLATE, isActive: true });
export const smsFormFrom = (c: SmsConfig): SmsFormState => ({ providerName: c.providerName, apiEndpoint: c.apiEndpoint, apiKey: '', senderId: c.senderId, messageTemplate: c.messageTemplate, isActive: c.isActive });

export function validateSmsForm(f: SmsFormState, keyRequired: boolean): Record<string, string> {
  const e: Record<string, string> = {};
  if (f.apiEndpoint && !isHttpsUrl(f.apiEndpoint)) e.apiEndpoint = 'Must be an https URL on the allowed provider list.';
  if (keyRequired && f.apiKey.trim().length < 8) e.apiKey = 'Enter the provider API key (at least 8 characters).';
  if (!keyRequired && f.apiKey && f.apiKey.trim().length < 8) e.apiKey = 'API keys are at least 8 characters.';
  if (f.senderId.trim().length < 3 || f.senderId.trim().length > 20) e.senderId = '3–20 characters, as registered with the provider.';
  if (f.messageTemplate && !f.messageTemplate.includes('{OTP}')) e.messageTemplate = 'The template must contain {OTP}.';
  return e;
}

export const toSmsInput = (f: SmsFormState): UpsertSmsInput => ({
  providerName: f.providerName.trim() || null,
  apiEndpoint: f.apiEndpoint.trim() || null,
  apiKey: f.apiKey.trim() || null,
  senderId: f.senderId.trim(),
  messageTemplate: f.messageTemplate.trim() || null,
  isActive: f.isActive,
});

interface Props { value: SmsFormState; onChange: (v: SmsFormState) => void; errors: Record<string, string>; hasStoredKey: boolean }

export function SmsForm({ value: f, onChange, errors, hasStoredKey }: Props) {
  const set = (k: keyof SmsFormState, v: string | boolean) => onChange({ ...f, [k]: v });
  return (
    <div className="row g-3">
      <Field label="Provider" className="col-12 col-md-4">
        {(id) => <input id={id} className="form-control" value={f.providerName} maxLength={50} onChange={(e) => set('providerName', e.target.value)} />}
      </Field>
      <Field label="API endpoint" error={errors.apiEndpoint} hint="Only hosts on the platform allow-list are accepted." className="col-12 col-md-8">
        {(id, inv) => <input id={id} type="url" className={`form-control ${inv ? 'is-invalid' : ''}`} value={f.apiEndpoint} maxLength={500} onChange={(e) => set('apiEndpoint', e.target.value)} />}
      </Field>
      <Field label="API key" required={!hasStoredKey} error={errors.apiKey} hint={hasStoredKey ? 'A key is stored. Leave blank to keep it; type a new one to replace it.' : 'Stored encrypted (AES-GCM) and never shown again.'} className="col-12 col-md-8">
        {(id, inv) => <input id={id} type="password" autoComplete="new-password" className={`form-control ${inv ? 'is-invalid' : ''}`} value={f.apiKey} maxLength={200} placeholder={hasStoredKey ? '•••••••• (unchanged)' : ''} onChange={(e) => set('apiKey', e.target.value)} />}
      </Field>
      <Field label="Sender ID" required error={errors.senderId} hint="DLT-registered header (India)." className="col-12 col-md-4">
        {(id, inv) => <input id={id} className={`form-control text-uppercase ${inv ? 'is-invalid' : ''}`} value={f.senderId} maxLength={20} onChange={(e) => set('senderId', e.target.value)} />}
      </Field>
      <Field label="Message template" error={errors.messageTemplate} hint="Placeholders: {OTP}, {EXPIRY}. Must match the registered DLT template." className="col-12">
        {(id, inv) => <textarea id={id} rows={2} className={`form-control ${inv ? 'is-invalid' : ''}`} value={f.messageTemplate} maxLength={500} onChange={(e) => set('messageTemplate', e.target.value)} />}
      </Field>
      <div className="col-12"><Switch label="Provider is active" checked={f.isActive} onChange={(v) => set('isActive', v)} /></div>
    </div>
  );
}
