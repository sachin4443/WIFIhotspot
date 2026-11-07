import { apiClient } from './client';
import { fileNameFromDisposition, saveBlob } from '../utils/download';
import type { PagedResult } from '../types/models';
import type { DisconnectCommand, RadiusOverview, RadiusRequestLog, RadiusResult, RadiusSessionRow, SessionQuery, UsageReport } from '../types/sessions';

type Sig = AbortSignal | undefined;
const clean = (q: object) => Object.fromEntries(Object.entries(q).filter(([, v]) => v !== '' && v !== undefined && v !== null));

export const sessionsApi = {
  list: async (q: SessionQuery, signal?: Sig) => (await apiClient.get<PagedResult<RadiusSessionRow>>('/api/sessions', { signal, params: clean(q) })).data,
  disconnect: async (id: string) => (await apiClient.post<DisconnectCommand>(`/api/sessions/${id}/disconnect`)).data,
  disconnects: async (organizationId?: string, signal?: Sig) => (await apiClient.get<DisconnectCommand[]>('/api/sessions/disconnects', { signal, params: clean({ organizationId }) })).data,
  exportXlsx: async (q: Omit<SessionQuery, 'page' | 'pageSize'>) => {
    const res = await apiClient.get<Blob>('/api/sessions/export', { params: clean(q), responseType: 'blob', timeout: 120_000 });
    saveBlob(res.data, fileNameFromDisposition(res.headers['content-disposition'] as string | undefined, 'accounting.xlsx'));
  },
};

export const reportsApi = {
  usage: async (q: { organizationId?: string; siteId?: string; from: string; to: string }, signal?: Sig) =>
    (await apiClient.get<UsageReport>('/api/reports/usage', { signal, params: clean(q) })).data,
};

export const radiusApi = {
  overview: async (organizationId?: string, signal?: Sig) => (await apiClient.get<RadiusOverview>('/api/radius/overview', { signal, params: clean({ organizationId }) })).data,
  requests: async (q: { organizationId?: string; result?: RadiusResult | ''; search?: string; page: number; pageSize: number }, signal?: Sig) =>
    (await apiClient.get<PagedResult<RadiusRequestLog>>('/api/radius/requests', { signal, params: clean(q) })).data,
};
