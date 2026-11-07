import { organizationsApi } from '../../api/platform';
import { useApi } from '../../hooks/useApi';

interface Props {
  value: string;
  onChange: (id: string) => void;
  includeAll?: boolean;
  allLabel?: string;
  id?: string;
  className?: string;
  required?: boolean;
}

/** Organization dropdown for platform administrators (loads up to 100, alphabetical). */
export function OrganizationSelect({ value, onChange, includeAll = true, allLabel = 'All organizations', id, className = 'form-select', required }: Props) {
  const { data, loading } = useApi((s) => organizationsApi.list({ page: 1, pageSize: 100 }, s), []);
  return (
    <select id={id} className={className} value={value} onChange={(e) => onChange(e.target.value)} disabled={loading} required={required} aria-label="Organization">
      {includeAll ? <option value="">{allLabel}</option> : <option value="" disabled>Select organization…</option>}
      {data?.items.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
    </select>
  );
}
