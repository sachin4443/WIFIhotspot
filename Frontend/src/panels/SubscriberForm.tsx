import { useState, type FormEvent } from 'react';
import { subscribersApi } from '../api/hotspot';
import { getErrorMessage } from '../api/errors';
import { SiteSelect } from '../components/common/SiteSelect';
import { Field } from '../components/ui/Field';
import { Modal } from '../components/ui/Modal';
import { Notice } from '../components/ui/States';
import { useToast } from '../toast/useToast';
import { typeInfo, type InternetPackage, type Subscriber, type SubscriberGroup, type SubscriberInput, type SubscriberType } from '../types/hotspot';
import { fromLocalInput, toLocalInput } from '../utils/format';
import { isEmail, isMobile } from '../utils/validation';

interface Props {
  organizationId: string;
  type: SubscriberType;
  editing: Subscriber | null;
  open: boolean;
  onClose: () => void;
  onSaved: () => void;
  groups: SubscriberGroup[];
  packages: InternetPackage[];
}

interface Form {
  identifier: string; fullName: string; mobileNumber: string; email: string; password: string; clearPassword: boolean; siteId: string; groupId: string;
  packageId: string; roomNumber: string; checkInAt: string; checkOutAt: string; validUntil: string; maxDevices: string; notes: string;
}

const PASSWORD_TYPES: SubscriberType[] = ['Student', 'Teacher', 'Staff', 'Employee', 'Customer'];

function initial(s: Subscriber | null, type: SubscriberType): Form {
  if (!s) {
    const now = new Date();
    const out = new Date(now.getTime() + 24 * 3600_000);
    out.setHours(11, 0, 0, 0);
    return { identifier: '', fullName: '', mobileNumber: '', email: '', password: '', clearPassword: false, siteId: '', groupId: '', packageId: '', roomNumber: '',
      checkInAt: type === 'RoomGuest' ? toLocalInput(now.toISOString()) : '', checkOutAt: type === 'RoomGuest' ? toLocalInput(out.toISOString()) : '', validUntil: '', maxDevices: '', notes: '' };
  }
  return { identifier: s.identifier, fullName: s.fullName, mobileNumber: s.mobileNumber ?? '', email: s.email ?? '', password: '', clearPassword: false,
    siteId: s.siteId ?? '', groupId: s.groupId ?? '', packageId: s.packageId ?? '', roomNumber: s.roomNumber ?? '', checkInAt: toLocalInput(s.checkInAt),
    checkOutAt: toLocalInput(s.checkOutAt), validUntil: toLocalInput(s.validUntil), maxDevices: s.maxDevices?.toString() ?? '', notes: s.notes ?? '' };
}

/** Create / edit form whose fields follow the subscriber type (room guest, guest, student, employee, …). */
export function SubscriberFormModal({ organizationId, type, editing, open, onClose, onSaved, groups, packages }: Props) {
  const toast = useToast();
  const [form, setForm] = useState<Form>(() => initial(editing, type));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);
  const info = typeInfo(type);
  const isRoom = type === 'RoomGuest';
  const isGuest = type === 'Guest';
  const usesPassword = PASSWORD_TYPES.includes(type);
  const set = (k: keyof Form, v: string | boolean) => setForm((f) => ({ ...f, [k]: v }));
  const sitePackages = packages.filter((p) => p.isActive && (!p.siteId || p.siteId === form.siteId));

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const errs: Record<string, string> = {};
    if (!form.fullName.trim()) errs.fullName = 'Enter the full name.';
    if (!editing && !isRoom && !isGuest && !form.identifier.trim()) errs.identifier = `Enter the ${info.idLabel.toLowerCase()}.`;
    if (form.mobileNumber && !isMobile(form.mobileNumber)) errs.mobileNumber = '10–15 digits, optionally starting with +.';
    if ((isRoom || isGuest) && !form.mobileNumber) errs.mobileNumber = 'A mobile number is required.';
    if (form.email && !isEmail(form.email)) errs.email = 'Enter a valid email.';
    if (form.password && (form.password.length < 6 || form.password.length > 128)) errs.password = '6–128 characters.';
    if (isRoom) {
      if (!form.siteId) errs.siteId = 'Room numbers are per site: choose the property.';
      if (!form.roomNumber.trim()) errs.roomNumber = 'Enter the room number.';
      if (!form.checkOutAt) errs.checkOutAt = 'Enter the check-out time.';
      else if (form.checkInAt && new Date(form.checkOutAt) <= new Date(form.checkInAt)) errs.checkOutAt = 'Check-out must be after check-in.';
    }
    if (form.maxDevices && (!/^\d+$/.test(form.maxDevices) || +form.maxDevices < 1 || +form.maxDevices > 50)) errs.maxDevices = '1–50, or empty to use the package.';
    setErrors(errs);
    if (Object.keys(errs).length) return;

    const body: SubscriberInput = {
      fullName: form.fullName.trim(), mobileNumber: form.mobileNumber.trim() || null, email: form.email.trim() || null,
      siteId: form.siteId || null, groupId: form.groupId || null, packageId: form.packageId || null, roomNumber: form.roomNumber.trim() || null,
      checkInAt: fromLocalInput(form.checkInAt), checkOutAt: fromLocalInput(form.checkOutAt), validUntil: fromLocalInput(form.validUntil),
      maxDevices: form.maxDevices ? +form.maxDevices : null, notes: form.notes.trim() || null,
    };
    setSaving(true);
    try {
      if (editing) {
        await subscribersApi.update(organizationId, editing.id, { ...body, newPassword: form.password || null, clearPassword: form.clearPassword });
        toast.success('Subscriber updated.');
      } else {
        const created = await subscribersApi.create(organizationId, { ...body, subscriberType: type, identifier: form.identifier.trim() || null, password: form.password || null });
        toast.success(isRoom ? `Guest checked in to room ${created.roomNumber}.` : `${info.label} ${created.identifier} created.`);
      }
      onSaved();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const input = (k: keyof Form, label: string, opts: { required?: boolean; type?: string; col?: string; hint?: string; disabled?: boolean; max?: number } = {}) => (
    <Field label={label} required={opts.required} error={errors[k]} hint={opts.hint} className={opts.col ?? 'col-12 col-md-6'}>
      {(id, inv) => <input id={id} type={opts.type ?? 'text'} disabled={opts.disabled} maxLength={opts.max} className={`form-control ${inv ? 'is-invalid' : ''}`} value={form[k] as string} onChange={(e) => set(k, e.target.value)} />}
    </Field>
  );

  return (
    <Modal open={open} size="lg" title={editing ? `Edit ${info.label.toLowerCase()}` : isRoom ? 'Check in guest' : `Add ${info.label.toLowerCase()}`} subtitle={editing?.identifier} onClose={onClose} busy={saving}
      footer={<><button type="button" className="btn btn-light" onClick={onClose} disabled={saving}>Cancel</button><button type="submit" form="sub-form" className="btn btn-primary" disabled={saving}>{saving ? 'Saving…' : isRoom && !editing ? 'Check in' : 'Save'}</button></>}>
      <form id="sub-form" onSubmit={submit} noValidate className="row g-3">
        {isRoom && (
          <>
            <Field label="Property / site" required error={errors.siteId} className="col-12 col-md-6">
              {(id, inv) => <SiteSelect id={id} organizationId={organizationId} value={form.siteId} onChange={(v) => set('siteId', v)} allLabel={null} invalid={inv} />}
            </Field>
            {input('roomNumber', 'Room number', { required: true, col: 'col-12 col-md-6', max: 20 })}
          </>
        )}
        {!isRoom && !isGuest && input('identifier', info.idLabel, { required: !editing, disabled: !!editing, hint: editing ? 'Identifiers cannot be changed.' : 'Used to sign in on the captive portal. Unique in your organization.', max: 100 })}
        {input('fullName', 'Full name', { required: true, max: 150 })}
        {input('mobileNumber', isRoom ? 'Registered mobile' : 'Mobile number', { required: isRoom || isGuest, type: 'tel', hint: isRoom ? 'The guest signs in with room number + this mobile.' : undefined })}
        {input('email', 'Email', { type: 'email', max: 254 })}
        {isRoom && (
          <>
            {input('checkInAt', 'Check-in', { type: 'datetime-local' })}
            {input('checkOutAt', 'Check-out', { required: true, type: 'datetime-local', hint: 'Access ends automatically at check-out.' })}
          </>
        )}
        {usesPassword && (
          <Field label={editing ? 'New password' : 'Password'} error={errors.password} hint={editing ? (editing.hasPassword ? 'Leave empty to keep the current password.' : 'No password set yet.') : 'Optional: needed for password-based sign-in. At least 6 characters.'} className="col-12 col-md-6">
            {(id, inv) => <input id={id} type="password" autoComplete="new-password" className={`form-control ${inv ? 'is-invalid' : ''}`} value={form.password} maxLength={128} onChange={(e) => set('password', e.target.value)} />}
          </Field>
        )}
        {usesPassword && editing?.hasPassword && (
          <div className="col-12 col-md-6 d-flex align-items-end"><label className="form-check"><input type="checkbox" className="form-check-input" checked={form.clearPassword} onChange={(e) => set('clearPassword', e.target.checked)} /> <span className="form-check-label fs-sm">Remove password (OTP-only sign-in)</span></label></div>
        )}
        {!isRoom && (
          <Field label="Site" hint="Empty = can sign in at every site." className="col-12 col-md-6">
            {(id) => <SiteSelect id={id} organizationId={organizationId} value={form.siteId} onChange={(v) => set('siteId', v)} allLabel="All sites" />}
          </Field>
        )}
        {!isGuest && !isRoom && (
          <Field label={type === 'Student' ? 'Class / course / batch' : 'Department'} className="col-12 col-md-6">
            {(id) => <select id={id} className="form-select" value={form.groupId} onChange={(e) => set('groupId', e.target.value)}><option value="">None</option>{groups.filter((g) => g.isActive).map((g) => <option key={g.id} value={g.id}>{g.name} ({g.groupType})</option>)}</select>}
          </Field>
        )}
        <Field label="Internet package" hint={form.groupId && !form.packageId ? 'The group’s default package is used when empty.' : undefined} className="col-12 col-md-6">
          {(id) => <select id={id} className="form-select" value={form.packageId} onChange={(e) => set('packageId', e.target.value)}><option value="">Organization defaults</option>{sitePackages.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}</select>}
        </Field>
        {!isRoom && input('validUntil', 'Access valid until', { type: 'datetime-local', hint: 'Empty = no end date.' })}
        {input('maxDevices', 'Device limit override', { col: 'col-6 col-md-3', hint: 'Empty = package limit.' })}
        <Field label="Notes" className="col-12">
          {(id) => <input id={id} className="form-control" value={form.notes} maxLength={500} onChange={(e) => set('notes', e.target.value)} />}
        </Field>
        {isRoom && !editing && <div className="col-12"><Notice>Room numbers are only unique within a property, so the guest is linked to the selected site.</Notice></div>}
      </form>
    </Modal>
  );
}
