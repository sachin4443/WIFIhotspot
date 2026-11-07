import { sitesApi } from '../../api/platform';
import { useApi } from '../../hooks/useApi';

interface Props {
  organizationId: string;
  value: string;
  onChange: (id: string) => void;
  allLabel?: string | null;
  id?: string;
  className?: string;
  invalid?: boolean;
}

/** Sites of one organization the caller can access (the API filters by site access). */
export function SiteSelect({ organizationId, value, onChange, allLabel = 'All sites', id, className = 'form-select', invalid }: Props) {
  const { data, loading } = useApi((s) => sitesApi.list(organizationId, s), [organizationId]);
  return (
    <select id={id} className={`${className} ${invalid ? 'is-invalid' : ''}`} value={value} onChange={(e) => onChange(e.target.value)} disabled={loading} aria-label={id ? undefined : "Site"}>
      {allLabel !== null ? <option value="">{allLabel}</option> : <option value="" disabled>Select site…</option>}
      {data?.map((s) => <option key={s.id} value={s.id}>{s.name}{!s.isActive ? ' (inactive)' : ''}</option>)}
    </select>
  );
}
