import { useMemo, useState, type FormEvent } from 'react';
import { FiPlus, FiUsers } from 'react-icons/fi';
import { adminUsersApi, rolesApi, sitesApi } from '../api/platform';
import { getErrorMessage } from '../api/errors';
import { useAuth } from '../auth/useAuth';
import { ActiveBadge, Badge } from '../components/ui/Badge';
import { Card, CardHeader } from '../components/ui/Card';
import { DataTable } from '../components/ui/DataTable';
import { Field, Switch } from '../components/ui/Field';
import { Modal } from '../components/ui/Modal';
import { SearchInput } from '../components/ui/SearchInput';
import { Notice } from '../components/ui/States';
import { useConfirm } from '../components/ui/useConfirm';
import { useApi } from '../hooks/useApi';
import { useToast } from '../toast/useToast';
import type { AdminUser, Role } from '../types/models';
import { formatRelative } from '../utils/format';
import { isEmail, isMobile, isUsername, passwordError } from '../utils/validation';

interface Props {
  /** Organization whose admins are managed; null = platform users (Super Admin only). */
  organizationId: string | null;
  canWrite: boolean;
  /** Platform view listing every user, with an organization column. */
  showAll?: boolean;
  organizationNames?: Map<string, string>;
}

interface Form { username: string; password: string; fullName: string; email: string; mobileNumber: string; hasAllSitesAccess: boolean; roleIds: string[]; siteIds: string[] }
const EMPTY: Form = { username: '', password: '', fullName: '', email: '', mobileNumber: '', hasAllSitesAccess: false, roleIds: [], siteIds: [] };

export function AdminUsersPanel({ organizationId, canWrite, showAll, organizationNames }: Props) {
  const { user: me } = useAuth();
  const toast = useToast();
  const confirm = useConfirm();
  const users = useApi((s) => adminUsersApi.list(showAll ? null : organizationId, s), [organizationId, showAll]);
  const roles = useApi((s) => rolesApi.list(s), []);
  const sites = useApi((s) => (organizationId ? sitesApi.list(organizationId, s) : Promise.resolve([])), [organizationId]);
  const [search, setSearch] = useState('');
  const [mode, setMode] = useState<{ kind: 'create' } | { kind: 'access'; user: AdminUser } | null>(null);
  const [form, setForm] = useState<Form>(EMPTY);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  const isPlatformScope = organizationId === null;
  const assignableRoles = useMemo<Role[]>(() => (roles.data ?? []).filter((r) => (isPlatformScope ? r.isPlatformRole : !r.isPlatformRole && (r.organizationId === null || r.organizationId === organizationId))), [roles.data, isPlatformScope, organizationId]);
  const roleIdByName = useMemo(() => new Map(assignableRoles.map((r) => [r.name, r.id])), [assignableRoles]);

  const rows = useMemo(() => {
    let list = users.data ?? [];
    if (!showAll) list = list.filter((u) => u.organizationId === organizationId);
    const q = search.trim().toLowerCase();
    return q ? list.filter((u) => `${u.username} ${u.fullName} ${u.email}`.toLowerCase().includes(q)) : list;
  }, [users.data, search, showAll, organizationId]);

  const openCreate = () => { setForm(EMPTY); setErrors({}); setMode({ kind: 'create' }); };
  const openAccess = (u: AdminUser) => {
    setForm({ ...EMPTY, hasAllSitesAccess: u.hasAllSitesAccess, siteIds: u.siteIds, roleIds: u.roles.map((n) => roleIdByName.get(n)).filter((x): x is string => !!x) });
    setErrors({});
    setMode({ kind: 'access', user: u });
  };

  const toggle = (list: string[], id: string) => (list.includes(id) ? list.filter((x) => x !== id) : [...list, id]);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!mode) return;
    const errs: Record<string, string> = {};
    if (mode.kind === 'create') {
      if (!isUsername(form.username)) errs.username = '3–50 letters, digits, . _ @ or -.';
      const pe = passwordError(form.password);
      if (pe) errs.password = pe;
      if (!form.fullName.trim()) errs.fullName = 'Enter the full name.';
      if (!isEmail(form.email)) errs.email = 'Enter a valid email address.';
      if (form.mobileNumber && !isMobile(form.mobileNumber)) errs.mobileNumber = '10–15 digits.';
    }
    if (form.roleIds.length === 0) errs.roles = 'Assign at least one role.';
    if (!isPlatformScope && !form.hasAllSitesAccess && form.siteIds.length === 0) errs.sites = 'Grant access to all sites or pick at least one.';
    setErrors(errs);
    if (Object.keys(errs).length) return;
    setSaving(true);
    try {
      if (mode.kind === 'create') {
        await adminUsersApi.create({
          organizationId, username: form.username.trim(), password: form.password, fullName: form.fullName.trim(), email: form.email.trim(),
          mobileNumber: form.mobileNumber.trim() || null, hasAllSitesAccess: isPlatformScope ? false : form.hasAllSitesAccess,
          roleIds: form.roleIds, siteIds: form.hasAllSitesAccess ? [] : form.siteIds,
        });
        toast.success('Administrator created.');
      } else {
        await adminUsersApi.setRoles(mode.user.id, form.roleIds);
        if (!isPlatformScope) await adminUsersApi.setSites(mode.user.id, form.hasAllSitesAccess, form.hasAllSitesAccess ? [] : form.siteIds);
        toast.success('Access updated. Changes apply at the user’s next sign-in.');
      }
      setMode(null);
      users.reload();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const setActive = async (u: AdminUser) => {
    const activate = !u.isActive;
    if (!(await confirm({ title: activate ? 'Activate administrator' : 'Deactivate administrator', message: activate ? `${u.username} will be able to sign in again.` : `${u.username} will be signed out at token expiry and cannot sign in.`, confirmLabel: activate ? 'Activate' : 'Deactivate', tone: activate ? 'primary' : 'danger' }))) return;
    try {
      await adminUsersApi.setActive(u.id, activate);
      toast.success(activate ? 'Administrator activated.' : 'Administrator deactivated.');
      users.reload();
    } catch (err) {
      toast.error(getErrorMessage(err));
    }
  };

  const siteName = new Map((sites.data ?? []).map((s) => [s.id, s.name]));

  return (
    <Card>
      <CardHeader
        title="Administrators"
        subtitle={isPlatformScope ? 'Platform and organization administrator accounts.' : 'People who can sign in to this organization’s console.'}
        icon={<FiUsers />}
        actions={canWrite && (!showAll || isPlatformScope) && <button type="button" className="btn btn-primary btn-sm btn-icon" onClick={openCreate}><FiPlus /> {isPlatformScope ? 'Add platform admin' : 'Add administrator'}</button>}
      />
      <div className="nxt-toolbar"><SearchInput value={search} onChange={setSearch} placeholder="Search name, username or email" /></div>
      <DataTable
        rows={rows}
        loading={users.loading}
        error={users.error}
        onRetry={users.reload}
        rowKey={(u) => u.id}
        empty={{ title: search ? 'No matching administrators' : 'No administrators yet', icon: <FiUsers /> }}
        mobileBadge={(u) => <ActiveBadge active={u.isActive} />}
        columns={[
          { key: 'user', header: 'Administrator', render: (u) => <div><div className="nxt-cell-primary">{u.fullName}</div><div className="nxt-cell-secondary">@{u.username} · {u.email}</div></div> },
          ...(showAll ? [{ key: 'org', header: 'Organization', render: (u: AdminUser) => (u.organizationId ? organizationNames?.get(u.organizationId) ?? 'Organization' : <Badge tone="info">Platform</Badge>) }] : []),
          { key: 'roles', header: 'Roles', render: (u) => <div className="d-flex flex-wrap gap-1">{u.roles.length ? u.roles.map((r) => <Badge key={r}>{r}</Badge>) : <span className="text-muted-2">None</span>}</div> },
          ...(!isPlatformScope ? [{ key: 'sites', header: 'Site access', render: (u: AdminUser) => (u.hasAllSitesAccess ? 'All sites' : u.siteIds.length ? u.siteIds.map((id) => siteName.get(id) ?? 'Site').join(', ') : <span className="text-danger">No sites</span>) }] : []),
          { key: 'last', header: 'Last sign-in', render: (u) => formatRelative(u.lastLoginAt) },
          { key: 'status', header: 'Status', hideOnMobile: true, render: (u) => <ActiveBadge active={u.isActive} /> },
        ]}
        actions={canWrite ? (u) => (u.id === me?.adminUserId ? <span className="fs-xs text-muted-2">You</span> : (
          <>
            {(!showAll || u.organizationId === organizationId) && <button type="button" className="btn btn-light btn-sm" onClick={() => openAccess(u)}>Access</button>}
            <button type="button" className={`btn btn-sm ${u.isActive ? 'btn-soft-danger' : 'btn-outline-primary'}`} onClick={() => setActive(u)}>{u.isActive ? 'Deactivate' : 'Activate'}</button>
          </>
        )) : undefined}
      />
      <Modal
        open={mode !== null}
        size="lg"
        title={mode?.kind === 'access' ? `Access · ${mode.user.fullName}` : isPlatformScope ? 'New platform administrator' : 'New administrator'}
        onClose={() => setMode(null)}
        busy={saving}
        footer={<><button type="button" className="btn btn-light" onClick={() => setMode(null)} disabled={saving}>Cancel</button><button type="submit" form="admin-form" className="btn btn-primary" disabled={saving}>{saving ? 'Saving…' : 'Save'}</button></>}
      >
        <form id="admin-form" onSubmit={submit} noValidate>
          {mode?.kind === 'create' && (
            <div className="row g-3 mb-3">
              <Field label="Full name" required error={errors.fullName} className="col-12 col-md-6">
                {(id, inv) => <input id={id} className={`form-control ${inv ? 'is-invalid' : ''}`} value={form.fullName} maxLength={150} onChange={(e) => setForm({ ...form, fullName: e.target.value })} />}
              </Field>
              <Field label="Username" required error={errors.username} className="col-12 col-md-6">
                {(id, inv) => <input id={id} autoComplete="off" className={`form-control ${inv ? 'is-invalid' : ''}`} value={form.username} maxLength={50} onChange={(e) => setForm({ ...form, username: e.target.value })} />}
              </Field>
              <Field label="Email" required error={errors.email} className="col-12 col-md-6">
                {(id, inv) => <input id={id} type="email" className={`form-control ${inv ? 'is-invalid' : ''}`} value={form.email} maxLength={254} onChange={(e) => setForm({ ...form, email: e.target.value })} />}
              </Field>
              <Field label="Mobile" error={errors.mobileNumber} className="col-12 col-md-6">
                {(id, inv) => <input id={id} type="tel" className={`form-control ${inv ? 'is-invalid' : ''}`} value={form.mobileNumber} onChange={(e) => setForm({ ...form, mobileNumber: e.target.value })} />}
              </Field>
              <Field label="Temporary password" required error={errors.password} hint="10+ characters with upper, lower and a digit. Share it securely." className="col-12 col-md-6">
                {(id, inv) => <input id={id} type="password" autoComplete="new-password" className={`form-control ${inv ? 'is-invalid' : ''}`} value={form.password} maxLength={128} onChange={(e) => setForm({ ...form, password: e.target.value })} />}
              </Field>
            </div>
          )}
          <div className="nxt-form-section">
            <div className="nxt-form-section-title">Roles</div>
            <div className="nxt-form-section-desc">You can only assign roles whose permissions you hold yourself.</div>
            {errors.roles && <div className="text-danger fs-sm mb-2">{errors.roles}</div>}
            <div className="row g-2">
              {assignableRoles.map((r) => (
                <div key={r.id} className="col-12 col-md-6">
                  <button type="button" className={`nxt-choice ${form.roleIds.includes(r.id) ? 'is-selected' : ''}`} onClick={() => setForm({ ...form, roleIds: toggle(form.roleIds, r.id) })} aria-pressed={form.roleIds.includes(r.id)}>
                    <input type="checkbox" className="form-check-input mt-1 flex-shrink-0" readOnly checked={form.roleIds.includes(r.id)} tabIndex={-1} />
                    <span className="min-w-0"><span className="nxt-choice-title d-block">{r.name} {r.isSystem && <Badge>System</Badge>}</span><span className="nxt-choice-desc d-block">{r.description ?? `${r.permissionCodes.length} permissions`}</span></span>
                  </button>
                </div>
              ))}
            </div>
          </div>
          {!isPlatformScope && (
            <div className="nxt-form-section">
              <div className="nxt-form-section-title">Site access</div>
              <div className="nxt-form-section-desc">Site-level administrators only see data for the sites selected here.</div>
              <Switch label="Access to all sites" checked={form.hasAllSitesAccess} onChange={(v) => setForm({ ...form, hasAllSitesAccess: v })} />
              {!form.hasAllSitesAccess && (
                <div className="row g-2 mt-1">
                  {errors.sites && <div className="col-12 text-danger fs-sm">{errors.sites}</div>}
                  {(sites.data ?? []).map((s) => (
                    <div key={s.id} className="col-12 col-md-6">
                      <label className={`nxt-choice ${form.siteIds.includes(s.id) ? 'is-selected' : ''}`}>
                        <input type="checkbox" className="form-check-input mt-1" checked={form.siteIds.includes(s.id)} onChange={() => setForm({ ...form, siteIds: toggle(form.siteIds, s.id) })} />
                        <span><span className="nxt-choice-title d-block">{s.name}</span><span className="nxt-choice-desc">{s.code}{s.city ? ` · ${s.city}` : ''}</span></span>
                      </label>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}
          {mode?.kind === 'access' && <Notice className="mt-3">Role and site changes take effect the next time this administrator signs in.</Notice>}
        </form>
      </Modal>
    </Card>
  );
}
