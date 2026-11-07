import { useMemo, useState } from 'react';
import { FiPlus, FiShield } from 'react-icons/fi';
import { rolesApi } from '../../api/platform';
import { getErrorMessage } from '../../api/errors';
import { useAuth } from '../../auth/useAuth';
import { PageHeader } from '../../components/common/PageHeader';
import { Badge } from '../../components/ui/Badge';
import { Card } from '../../components/ui/Card';
import { DataTable } from '../../components/ui/DataTable';
import { Field } from '../../components/ui/Field';
import { Modal } from '../../components/ui/Modal';
import { Notice } from '../../components/ui/States';
import { useConfirm } from '../../components/ui/useConfirm';
import { useApi } from '../../hooks/useApi';
import { useToast } from '../../toast/useToast';
import type { PermissionInfo, Role } from '../../types/models';
import { Permissions } from '../../types/permissions';
import { useWorkspace } from '../../workspace/useWorkspace';

const GROUP_LABELS: Record<string, string> = {
  organizations: 'Organizations', sites: 'Sites', adminusers: 'Administrators', roles: 'Roles', wallet: 'Wallet', organizationtypes: 'Organization types',
  subscriptionplans: 'Subscription plans', settings: 'Settings & branding', audit: 'Audit log', platform: 'Platform', nas: 'NAS devices', packages: 'Internet packages',
  subscribers: 'Subscribers', sessions: 'Sessions', reports: 'Reports', radius: 'RADIUS',
};

function groupOf(code: string) { return code.split('.')[0]; }

export function RolesPage() {
  const { user, hasPermission } = useAuth();
  const { organization } = useWorkspace();
  const toast = useToast();
  const confirm = useConfirm();
  const canWrite = hasPermission(Permissions.RolesWrite);
  const roles = useApi((s) => rolesApi.list(s), []);
  const perms = useApi((s) => rolesApi.permissions(s), []);
  const [editing, setEditing] = useState<Role | 'new' | null>(null);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [codes, setCodes] = useState<string[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  const myPerms = useMemo(() => new Set(user?.permissions ?? []), [user]);
  const grantable = useMemo(() => (perms.data ?? []).filter((p) => (user?.isPlatformAdmin || !p.isPlatformOnly) && myPerms.has(p.code)), [perms.data, user, myPerms]);
  const groups = useMemo(() => {
    const map = new Map<string, PermissionInfo[]>();
    for (const p of grantable) map.set(groupOf(p.code), [...(map.get(groupOf(p.code)) ?? []), p]);
    return [...map.entries()];
  }, [grantable]);

  const readOnly = editing !== null && editing !== 'new' && (editing.isSystem && !user?.isPlatformAdmin);

  const open = (r: Role | 'new') => {
    setEditing(r);
    setErrors({});
    setName(r === 'new' ? '' : r.name);
    setDescription(r === 'new' ? '' : r.description ?? '');
    setCodes(r === 'new' ? [] : r.permissionCodes);
  };

  const save = async () => {
    if (!editing) return;
    const errs: Record<string, string> = {};
    if (editing === 'new' && name.trim().length < 2) errs.name = 'Enter a role name.';
    if (codes.length === 0) errs.codes = 'Select at least one permission.';
    setErrors(errs);
    if (Object.keys(errs).length) return;
    setSaving(true);
    try {
      if (editing === 'new') await rolesApi.create({ organizationId: user?.isPlatformAdmin ? null : organization?.id ?? null, name: name.trim(), description: description.trim() || null, permissionCodes: codes });
      else await rolesApi.setPermissions(editing.id, codes);
      toast.success('Role saved. Users get the new permissions at their next sign-in.');
      setEditing(null);
      roles.reload();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (r: Role) => {
    if (!(await confirm({ title: 'Delete role', message: `Delete the role “${r.name}”? This fails if any administrator still has it.`, confirmLabel: 'Delete' }))) return;
    try {
      await rolesApi.remove(r.id);
      toast.success('Role deleted.');
      roles.reload();
    } catch (err) {
      toast.error(getErrorMessage(err));
    }
  };

  const toggle = (c: string) => setCodes((cur) => (cur.includes(c) ? cur.filter((x) => x !== c) : [...cur, c]));
  const toggleGroup = (items: PermissionInfo[]) => {
    const all = items.every((p) => codes.includes(p.code));
    setCodes((cur) => (all ? cur.filter((c) => !items.some((p) => p.code === c)) : [...new Set([...cur, ...items.map((p) => p.code)])]));
  };

  return (
    <>
      <PageHeader title="Roles & permissions" subtitle="Roles bundle permissions. Menus and actions appear only for the permissions a user holds." actions={canWrite && <button type="button" className="btn btn-primary btn-icon" onClick={() => open('new')}><FiPlus /> New role</button>} />
      <Card>
        <DataTable
          rows={roles.data}
          loading={roles.loading}
          error={roles.error}
          onRetry={roles.reload}
          rowKey={(r) => r.id}
          onRowClick={open}
          empty={{ title: 'No roles', icon: <FiShield /> }}
          columns={[
            { key: 'name', header: 'Role', render: (r) => <div><div className="nxt-cell-primary">{r.name}</div><div className="nxt-cell-secondary">{r.description ?? ''}</div></div> },
            { key: 'kind', header: 'Kind', render: (r) => r.isPlatformRole ? <Badge tone="info">Platform</Badge> : r.isSystem ? <Badge>System</Badge> : <Badge tone="success">Custom</Badge> },
            { key: 'perms', header: 'Permissions', render: (r) => r.permissionCodes.length },
          ]}
          actions={canWrite ? (r) => (
            <>
              <button type="button" className="btn btn-light btn-sm" onClick={() => open(r)}>{r.isSystem && !user?.isPlatformAdmin ? 'View' : 'Edit'}</button>
              {!r.isSystem && <button type="button" className="btn btn-soft-danger btn-sm" onClick={() => remove(r)}>Delete</button>}
            </>
          ) : undefined}
        />
      </Card>
      <Modal
        open={editing !== null}
        size="lg"
        title={editing === 'new' ? 'New role' : editing ? editing.name : ''}
        subtitle={editing && editing !== 'new' && editing.isSystem ? 'System role' : undefined}
        onClose={() => setEditing(null)}
        busy={saving}
        footer={<><button type="button" className="btn btn-light" onClick={() => setEditing(null)} disabled={saving}>{readOnly || !canWrite ? 'Close' : 'Cancel'}</button>{canWrite && !readOnly && <button type="button" className="btn btn-primary" onClick={save} disabled={saving}>{saving ? 'Saving…' : 'Save role'}</button>}</>}
      >
        {editing === 'new' && (
          <div className="row g-3 mb-3">
            <Field label="Role name" required error={errors.name} className="col-12 col-md-6">
              {(id, inv) => <input id={id} className={`form-control ${inv ? 'is-invalid' : ''}`} value={name} maxLength={80} onChange={(e) => setName(e.target.value)} />}
            </Field>
            <Field label="Description" className="col-12 col-md-6">
              {(id) => <input id={id} className="form-control" value={description} maxLength={300} onChange={(e) => setDescription(e.target.value)} />}
            </Field>
          </div>
        )}
        {readOnly && <Notice className="mb-3">System roles are managed by the platform. Create a custom role to tailor permissions.</Notice>}
        {errors.codes && <div className="text-danger fs-sm mb-2">{errors.codes}</div>}
        <div className="d-grid gap-3">
          {groups.map(([group, items]) => (
            <div key={group} className="border rounded-3 p-3">
              <div className="d-flex align-items-center justify-content-between gap-2 mb-2">
                <div className="fw-semibold fs-sm">{GROUP_LABELS[group] ?? group}</div>
                {!readOnly && canWrite && <button type="button" className="btn btn-link btn-sm p-0" onClick={() => toggleGroup(items)}>{items.every((p) => codes.includes(p.code)) ? 'Clear' : 'Select all'}</button>}
              </div>
              <div className="row g-2">
                {items.map((p) => (
                  <div key={p.code} className="col-12 col-md-6">
                    <label className="d-flex gap-2 align-items-start fs-sm">
                      <input type="checkbox" className="form-check-input mt-1 flex-shrink-0" checked={codes.includes(p.code)} disabled={readOnly || !canWrite} onChange={() => toggle(p.code)} />
                      <span><span className="font-monospace fs-xs d-block">{p.code}</span><span className="text-muted-2 fs-xs">{p.description}</span></span>
                    </label>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      </Modal>
    </>
  );
}
