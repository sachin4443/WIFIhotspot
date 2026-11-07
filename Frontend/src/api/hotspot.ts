import { apiClient } from './client';
import { fileNameFromDisposition, saveBlob } from '../utils/download';
import type { PagedResult } from '../types/models';
import type {
  ImportResult, InternetPackage, Nas, PortalKey, RouterConfig, Subscriber, SubscriberGroup, SubscriberInput, SubscriberQuery,
  SubscriberStatus, SubscriberType, UpsertGroupInput, UpsertNasInput, UpsertPackageInput,
} from '../types/hotspot';

type Sig = AbortSignal | undefined;
const base = (orgId: string) => `/api/organizations/${orgId}`;
const get = async <T>(url: string, signal?: Sig, params?: object) => (await apiClient.get<T>(url, { signal, params })).data;
const put = async <T>(url: string, body?: unknown) => (await apiClient.put<T>(url, body)).data;
const post = async <T>(url: string, body?: unknown) => (await apiClient.post<T>(url, body)).data;
const del = async (url: string) => { await apiClient.delete(url); };

async function download(url: string, params: object | undefined, fallback: string) {
  const res = await apiClient.get<Blob>(url, { params, responseType: 'blob' });
  saveBlob(res.data, fileNameFromDisposition(res.headers['content-disposition'] as string | undefined, fallback));
}

const clean = (q: object) => Object.fromEntries(Object.entries(q).filter(([, v]) => v !== '' && v !== undefined && v !== null));

export const nasApi = {
  list: (orgId: string, siteId?: string, signal?: Sig) => get<Nas[]>(`${base(orgId)}/nas`, signal, clean({ siteId })),
  create: (orgId: string, body: UpsertNasInput) => post<Nas>(`${base(orgId)}/nas`, body),
  update: (orgId: string, id: string, body: UpsertNasInput) => put<Nas>(`${base(orgId)}/nas/${id}`, body),
  remove: (orgId: string, id: string) => del(`${base(orgId)}/nas/${id}`),
  portalKey: (orgId: string, id: string) => post<PortalKey>(`${base(orgId)}/nas/${id}/portal-key`),
  routerConfig: (orgId: string, id: string, signal?: Sig) => get<RouterConfig>(`${base(orgId)}/nas/${id}/router-config`, signal),
};

export const packagesApi = {
  list: (orgId: string, siteId?: string, signal?: Sig) => get<InternetPackage[]>(`${base(orgId)}/packages`, signal, clean({ siteId })),
  create: (orgId: string, body: UpsertPackageInput) => post<InternetPackage>(`${base(orgId)}/packages`, body),
  update: (orgId: string, id: string, body: UpsertPackageInput) => put<InternetPackage>(`${base(orgId)}/packages/${id}`, body),
  remove: (orgId: string, id: string) => del(`${base(orgId)}/packages/${id}`),
};

export const groupsApi = {
  list: (orgId: string, signal?: Sig) => get<SubscriberGroup[]>(`${base(orgId)}/subscriber-groups`, signal),
  create: (orgId: string, body: UpsertGroupInput) => post<SubscriberGroup>(`${base(orgId)}/subscriber-groups`, body),
  update: (orgId: string, id: string, body: UpsertGroupInput) => put<SubscriberGroup>(`${base(orgId)}/subscriber-groups/${id}`, body),
  remove: (orgId: string, id: string) => del(`${base(orgId)}/subscriber-groups/${id}`),
};

export const subscribersApi = {
  list: (orgId: string, q: SubscriberQuery, signal?: Sig) => get<PagedResult<Subscriber>>(`${base(orgId)}/subscribers`, signal, clean(q)),
  get: (orgId: string, id: string, signal?: Sig) => get<Subscriber>(`${base(orgId)}/subscribers/${id}`, signal),
  create: (orgId: string, body: SubscriberInput) => post<Subscriber>(`${base(orgId)}/subscribers`, body),
  update: (orgId: string, id: string, body: SubscriberInput) => put<Subscriber>(`${base(orgId)}/subscribers/${id}`, body),
  setStatus: (orgId: string, id: string, status: SubscriberStatus) => put<Subscriber>(`${base(orgId)}/subscribers/${id}/status`, { status }),
  checkOut: (orgId: string, id: string) => post<Subscriber>(`${base(orgId)}/subscribers/${id}/check-out`),
  remove: (orgId: string, id: string) => del(`${base(orgId)}/subscribers/${id}`),
  downloadTemplate: (orgId: string, type: SubscriberType) => download(`${base(orgId)}/subscribers/import-template`, { type }, `${type}-template.xlsx`),
  exportXlsx: (orgId: string, q: Omit<SubscriberQuery, 'page' | 'pageSize'>) => download(`${base(orgId)}/subscribers/export`, clean(q), 'subscribers.xlsx'),
  importXlsx: async (orgId: string, type: SubscriberType, file: File, updateExisting: boolean) => {
    const form = new FormData();
    form.append('file', file);
    const res = await apiClient.post<ImportResult>(`${base(orgId)}/subscribers/import`, form, { params: { type, updateExisting }, timeout: 120_000 });
    return res.data;
  },
};
