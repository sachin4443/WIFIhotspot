import { apiClient } from './client';
import type {
  AdminUser, AuditLog, CreateAdminUserInput, LoginMethod, OrganizationBranding, OrganizationDetail, OrganizationProfile,
  OrganizationSettings, OrganizationStatistics, OrganizationStatus, OrganizationSummary, OrganizationType, OrganizationTypeDetail,
  PagedResult, PermissionInfo, PlatformDashboard, RegisterOrganizationInput, Role, Site, SiteListItem, SmsConfig,
  SubscriptionPlan, SubscriptionPlanDetail, UpsertLoginMethodInput, UpsertPlanInput, UpsertSmsInput, Wallet, WalletOverview,
  WalletTransaction,
} from '../types/models';

type Sig = AbortSignal | undefined;
const get = async <T>(url: string, signal?: Sig, params?: object) => (await apiClient.get<T>(url, { signal, params })).data;
const put = async <T>(url: string, body?: unknown) => (await apiClient.put<T>(url, body)).data;
const post = async <T>(url: string, body?: unknown, headers?: Record<string, string>) => (await apiClient.post<T>(url, body, { headers })).data;
const del = async (url: string) => { await apiClient.delete(url); };

/** Random key so a retried money operation is applied once (backend honours Idempotency-Key). */
export const newIdempotencyKey = (): string => {
  if (typeof crypto.randomUUID === 'function') return crypto.randomUUID();
  const b = crypto.getRandomValues(new Uint8Array(16));
  return Array.from(b, (x) => x.toString(16).padStart(2, '0')).join('');
};

export const dashboardApi = {
  platform: (signal?: Sig) => get<PlatformDashboard>('/api/dashboard/platform', signal),
  organization: (orgId: string, signal?: Sig) => get<OrganizationStatistics>(`/api/dashboard/organizations/${orgId}`, signal),
};

export interface OrganizationListQuery { page: number; pageSize: number; search?: string; status?: OrganizationStatus | ''; organizationTypeId?: number | '' }

export const organizationsApi = {
  list: (q: OrganizationListQuery, signal?: Sig) =>
    get<PagedResult<OrganizationSummary>>('/api/organizations', signal, {
      page: q.page, pageSize: q.pageSize, search: q.search || undefined, status: q.status || undefined, organizationTypeId: q.organizationTypeId || undefined,
    }),
  get: (id: string, signal?: Sig) => get<OrganizationDetail>(`/api/organizations/${id}`, signal),
  register: (input: RegisterOrganizationInput) => post<OrganizationDetail>('/api/organizations', input),
  setStatus: (id: string, status: OrganizationStatus) => put<void>(`/api/organizations/${id}/status`, { status }),
  assignPlan: (id: string, subscriptionPlanId: string | null) => put<void>(`/api/organizations/${id}/subscription-plan`, { subscriptionPlanId }),
  profile: (id: string, signal?: Sig) => get<OrganizationProfile>(`/api/organizations/${id}/profile`, signal),
  updateProfile: (id: string, body: Omit<OrganizationProfile, 'id'>) => put<OrganizationProfile>(`/api/organizations/${id}/profile`, body),
  settings: (id: string, signal?: Sig) => get<OrganizationSettings>(`/api/organizations/${id}/settings`, signal),
  updateSettings: (id: string, body: Omit<OrganizationSettings, 'organizationId'>) => put<OrganizationSettings>(`/api/organizations/${id}/settings`, body),
  branding: (id: string, signal?: Sig) => get<OrganizationBranding>(`/api/organizations/${id}/branding`, signal),
  updateBranding: (id: string, body: Omit<OrganizationBranding, 'organizationId'>) => put<OrganizationBranding>(`/api/organizations/${id}/branding`, body),
  loginMethods: (id: string, signal?: Sig) => get<LoginMethod[]>(`/api/organizations/${id}/login-methods`, signal),
  upsertLoginMethod: (id: string, body: UpsertLoginMethodInput) => put<LoginMethod[]>(`/api/organizations/${id}/login-methods`, body),
  deleteLoginMethod: (id: string, methodId: string) => del(`/api/organizations/${id}/login-methods/${methodId}`),
  siteLoginMethods: (id: string, siteId: string, signal?: Sig) => get<LoginMethod[]>(`/api/organizations/${id}/sites/${siteId}/login-methods`, signal),
  upsertSiteLoginMethod: (id: string, siteId: string, body: UpsertLoginMethodInput) => put<LoginMethod[]>(`/api/organizations/${id}/sites/${siteId}/login-methods`, body),
  sms: (id: string, signal?: Sig) => get<SmsConfig[]>(`/api/organizations/${id}/sms`, signal),
  upsertSms: (id: string, body: UpsertSmsInput) => put<SmsConfig>(`/api/organizations/${id}/sms`, body),
  deleteSms: (id: string) => del(`/api/organizations/${id}/sms`),
  upsertSiteSms: (id: string, siteId: string, body: UpsertSmsInput) => put<SmsConfig>(`/api/organizations/${id}/sites/${siteId}/sms`, body),
  deleteSiteSms: (id: string, siteId: string) => del(`/api/organizations/${id}/sites/${siteId}/sms`),
};

export const referenceApi = {
  organizationTypes: (includeInactive = false, signal?: Sig) => get<OrganizationType[]>('/api/organization-types', signal, { includeInactive }),
  subscriptionPlans: (signal?: Sig) => get<SubscriptionPlan[]>('/api/subscription-plans', signal),
};

export const platformApi = {
  organizationType: (id: number, signal?: Sig) => get<OrganizationTypeDetail>(`/api/platform/organization-types/${id}`, signal),
  createOrganizationType: (body: Omit<OrganizationTypeDetail, 'organizationTypeId'>) => post<OrganizationTypeDetail>('/api/platform/organization-types', body),
  updateOrganizationType: (id: number, body: Omit<OrganizationTypeDetail, 'organizationTypeId'>) => put<OrganizationTypeDetail>(`/api/platform/organization-types/${id}`, body),
  plans: (signal?: Sig) => get<SubscriptionPlanDetail[]>('/api/platform/subscription-plans', signal),
  createPlan: (body: UpsertPlanInput) => post<SubscriptionPlanDetail>('/api/platform/subscription-plans', body),
  updatePlan: (id: string, body: UpsertPlanInput) => put<SubscriptionPlanDetail>(`/api/platform/subscription-plans/${id}`, body),
  sms: (signal?: Sig) => get<SmsConfig>('/api/platform/sms', signal),
  upsertSms: (body: UpsertSmsInput) => put<SmsConfig>('/api/platform/sms', body),
};

export const sitesApi = {
  directory: (q: { organizationId?: string; search?: string; isActive?: boolean | ''; page: number; pageSize: number }, signal?: Sig) =>
    get<PagedResult<SiteListItem>>('/api/sites', signal, {
      organizationId: q.organizationId || undefined, search: q.search || undefined, isActive: q.isActive === '' ? undefined : q.isActive, page: q.page, pageSize: q.pageSize,
    }),
  list: (orgId: string, signal?: Sig) => get<Site[]>(`/api/organizations/${orgId}/sites`, signal),
  create: (orgId: string, body: { name: string; code: string; address?: string | null; city?: string | null }) => post<Site>(`/api/organizations/${orgId}/sites`, body),
  update: (siteId: string, body: { name: string; address?: string | null; city?: string | null; isActive: boolean }) => put<Site>(`/api/sites/${siteId}`, body),
};

export const adminUsersApi = {
  list: (organizationId?: string | null, signal?: Sig) => get<AdminUser[]>('/api/admin-users', signal, { organizationId: organizationId || undefined }),
  create: (body: CreateAdminUserInput) => post<AdminUser>('/api/admin-users', body),
  setRoles: (id: string, roleIds: string[]) => put<AdminUser>(`/api/admin-users/${id}/roles`, { roleIds }),
  setSites: (id: string, hasAllSitesAccess: boolean, siteIds: string[]) => put<AdminUser>(`/api/admin-users/${id}/sites`, { hasAllSitesAccess, siteIds }),
  setActive: (id: string, isActive: boolean) => put<void>(`/api/admin-users/${id}/active`, { isActive }),
};

export const rolesApi = {
  list: (signal?: Sig) => get<Role[]>('/api/roles', signal),
  permissions: (signal?: Sig) => get<PermissionInfo[]>('/api/permissions', signal),
  create: (body: { organizationId?: string | null; name: string; description?: string | null; permissionCodes: string[] }) => post<Role>('/api/roles', body),
  setPermissions: (id: string, permissionCodes: string[]) => put<Role>(`/api/roles/${id}/permissions`, { permissionCodes }),
  remove: (id: string) => del(`/api/roles/${id}`),
};

export const walletsApi = {
  overview: (q: { organizationId?: string; search?: string; page: number; pageSize: number }, signal?: Sig) =>
    get<PagedResult<WalletOverview>>('/api/wallets', signal, { organizationId: q.organizationId || undefined, search: q.search || undefined, page: q.page, pageSize: q.pageSize }),
  get: (siteId: string, signal?: Sig) => get<Wallet>(`/api/sites/${siteId}/wallet`, signal),
  transactions: (siteId: string, page: number, pageSize: number, signal?: Sig) =>
    get<PagedResult<WalletTransaction>>(`/api/sites/${siteId}/wallet/transactions`, signal, { page, pageSize }),
  credit: (siteId: string, body: { amount: number; reference?: string | null; description?: string | null }, key: string) =>
    post<WalletTransaction>(`/api/sites/${siteId}/wallet/credit`, body, { 'Idempotency-Key': key }),
  debit: (siteId: string, body: { amount: number; reference: string; description?: string | null }, key: string) =>
    post<WalletTransaction>(`/api/sites/${siteId}/wallet/debit`, body, { 'Idempotency-Key': key }),
};

export const auditApi = {
  list: (q: { organizationId?: string; action?: string; from?: string; to?: string; page: number; pageSize: number }, signal?: Sig) =>
    get<PagedResult<AuditLog>>('/api/audit-logs', signal, {
      organizationId: q.organizationId || undefined, action: q.action || undefined, from: q.from || undefined, to: q.to || undefined, page: q.page, pageSize: q.pageSize,
    }),
};
