import { useMemo, useState } from 'react';
import { organizationsApi } from '../../api/platform';
import { useAuth } from '../../auth/useAuth';
import { OrganizationSelect } from '../../components/common/OrganizationSelect';
import { PageHeader } from '../../components/common/PageHeader';
import { useApi } from '../../hooks/useApi';
import { AdminUsersPanel } from '../../panels/AdminUsersPanel';
import { Permissions } from '../../types/permissions';
import { useWorkspace } from '../../workspace/useWorkspace';

/** Platform admins pick "platform" or an organization; organization admins manage their own. */
export function AdminUsersPage() {
  const { user, hasPermission } = useAuth();
  const { organization } = useWorkspace();
  const [scope, setScope] = useState<string>('platform');
  const orgs = useApi((s) => organizationsApi.list({ page: 1, pageSize: 100 }, s), [], !!user?.isPlatformAdmin);
  const names = useMemo(() => new Map((orgs.data?.items ?? []).map((o) => [o.id, o.name])), [orgs.data]);
  const canWrite = hasPermission(Permissions.AdminUsersWrite);

  if (!user?.isPlatformAdmin) {
    return (
      <>
        <PageHeader title="Administrators" subtitle="Manage who can sign in to your organization console and which sites they can access." />
        {organization && <AdminUsersPanel organizationId={organization.id} canWrite={canWrite} />}
      </>
    );
  }

  return (
    <>
      <PageHeader
        title="Administrators"
        subtitle="Platform administrators and every organization’s console users."
        actions={
          <div style={{ minWidth: 260 }}>
            <select className="form-select" value={scope === 'platform' || scope === 'all' ? scope : 'org'} onChange={(e) => setScope(e.target.value === 'org' ? '' : e.target.value)} aria-label="Scope">
              <option value="platform">Platform administrators</option>
              <option value="all">All administrators</option>
              <option value="org">One organization…</option>
            </select>
          </div>
        }
      />
      {scope !== 'platform' && scope !== 'all' && (
        <div className="mb-3" style={{ maxWidth: 360 }}>
          <OrganizationSelect value={scope} onChange={setScope} includeAll={false} />
        </div>
      )}
      {scope === 'platform' && <AdminUsersPanel organizationId={null} canWrite={canWrite} />}
      {scope === 'all' && <AdminUsersPanel organizationId={null} canWrite={canWrite} showAll organizationNames={names} />}
      {scope !== 'platform' && scope !== 'all' && scope && <AdminUsersPanel key={scope} organizationId={scope} canWrite={canWrite} />}
    </>
  );
}
