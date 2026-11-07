import { useAuth } from '../../auth/useAuth';
import { OrganizationSelect } from '../../components/common/OrganizationSelect';
import { SiteSelect } from '../../components/common/SiteSelect';
import { useWorkspace } from '../../workspace/useWorkspace';

/** Organization filter for platform admins; site filter for organization admins. */
export function ScopeFilters({ orgId, onOrgChange, siteId, onSiteChange }: { orgId: string; onOrgChange: (v: string) => void; siteId: string; onSiteChange: (v: string) => void }) {
  const { user } = useAuth();
  const { organization } = useWorkspace();
  if (user?.isPlatformAdmin) {
    return (
      <>
        <div style={{ minWidth: 200 }} className="flex-grow-1 flex-md-grow-0"><OrganizationSelect value={orgId} onChange={(v) => { onOrgChange(v); onSiteChange(''); }} /></div>
        {orgId && <div style={{ minWidth: 160 }} className="flex-grow-1 flex-md-grow-0"><SiteSelect organizationId={orgId} value={siteId} onChange={onSiteChange} /></div>}
      </>
    );
  }
  return organization ? <div style={{ minWidth: 160 }} className="flex-grow-1 flex-md-grow-0"><SiteSelect organizationId={organization.id} value={siteId} onChange={onSiteChange} /></div> : null;
}

/** Default date range helpers (local dates for <input type="date">). */
export const isoDate = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
export const daysAgo = (n: number) => { const d = new Date(); d.setDate(d.getDate() - n); return isoDate(d); };
export const startOfDay = (v: string) => (v ? new Date(`${v}T00:00:00`).toISOString() : undefined);
export const endOfDay = (v: string) => (v ? new Date(`${v}T23:59:59.999`).toISOString() : undefined);
