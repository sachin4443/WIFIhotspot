/* TypeScript mirrors of the backend DTOs (System.Text.Json camelCase, enums as strings). */

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export type OrganizationStatus = 'Pending' | 'Active' | 'Suspended' | 'Inactive';
export const ORGANIZATION_STATUSES: OrganizationStatus[] = ['Active', 'Pending', 'Suspended', 'Inactive'];

export type AuthenticationType =
  | 'MobileOTP' | 'RoomMobile' | 'StudentPassword' | 'StudentOTP' | 'StaffPassword'
  | 'EmployeePassword' | 'Voucher' | 'UsernamePassword' | 'GuestOTP';

export const AUTH_TYPES: { value: AuthenticationType; label: string; description: string }[] = [
  { value: 'MobileOTP', label: 'Mobile OTP', description: 'Visitor enters a mobile number and verifies an SMS one-time password.' },
  { value: 'RoomMobile', label: 'Room + mobile', description: 'Hotel guest enters room number and registered mobile number.' },
  { value: 'StudentPassword', label: 'Student ID + password', description: 'Student signs in with enrollment number and password.' },
  { value: 'StudentOTP', label: 'Student ID + OTP', description: 'Student enrollment number verified with an SMS OTP.' },
  { value: 'StaffPassword', label: 'Staff / teacher password', description: 'Teachers and staff sign in with staff ID and password.' },
  { value: 'EmployeePassword', label: 'Employee ID + password', description: 'Employees sign in with employee ID and password.' },
  { value: 'Voucher', label: 'Voucher code', description: 'Pre-generated access code.' },
  { value: 'UsernamePassword', label: 'Username + password', description: 'Subscriber account credentials (ISP / hostel).' },
  { value: 'GuestOTP', label: 'Guest OTP', description: 'Visitors in education/enterprise venues verify by SMS OTP.' },
];

export const authLabel = (t: AuthenticationType) => AUTH_TYPES.find((a) => a.value === t)?.label ?? t;

export interface OrganizationSummary {
  id: string;
  name: string;
  organizationTypeName: string;
  status: OrganizationStatus;
  siteCount: number;
  createdAt: string;
}

export interface LoginMethod {
  loginMethodId: string;
  siteId: string | null;
  authenticationType: AuthenticationType;
  isEnabled: boolean;
  isDefault: boolean;
  requireOTP: boolean;
  requireBookingValidation: boolean;
  allowMultipleDevices: boolean;
  maxDevices: number;
}

export interface SmsSetting {
  isConfigured: boolean;
  providerName: string | null;
  apiEndpoint: string | null;
  senderId: string | null;
  hasApiKey: boolean;
  usesPlatformDefault: boolean;
}

export interface OrganizationDetail {
  id: string;
  name: string;
  organizationTypeId: number;
  organizationTypeName: string;
  status: OrganizationStatus;
  contactPerson: string;
  mobileNumber: string;
  email: string;
  address: string | null;
  subscriptionPlanId: string | null;
  primarySiteId: string;
  adminUserId: string | null;
  adminUsername: string | null;
  walletId: string;
  walletBalance: number;
  loginMethods: LoginMethod[];
  sms: SmsSetting;
  createdAt: string;
}

export interface SmsProviderInput {
  providerName: string;
  apiEndpoint?: string | null;
  apiKey: string;
  senderId: string;
  messageTemplate?: string | null;
}

export interface RegisterOrganizationInput {
  organizationName: string;
  organizationTypeId: number;
  contactPerson: string;
  mobileNumber: string;
  email: string;
  address?: string | null;
  primarySiteName?: string | null;
  adminUsername: string;
  adminPassword: string;
  subscriptionPlanId?: string | null;
  initialWalletBalance: number;
  sms?: SmsProviderInput | null;
  defaultLoginMethods?: AuthenticationType[] | null;
  logoUrl?: string | null;
  status?: OrganizationStatus | null;
}

export interface OrganizationType {
  organizationTypeId: number;
  typeName: string;
  description: string | null;
  isActive: boolean;
}

export interface OrganizationTypeLoginMethod {
  authenticationType: AuthenticationType;
  isDefault: boolean;
  requireOTP: boolean;
  requireBookingValidation: boolean;
  allowMultipleDevices: boolean;
  maxDevices: number;
}

export interface OrganizationTypeDetail extends OrganizationType {
  defaultLoginMethods: OrganizationTypeLoginMethod[];
}

export interface SubscriptionPlan {
  id: string;
  name: string;
  monthlyPrice: number;
  maxSites: number | null;
  maxNasDevices: number | null;
  maxUsers: number | null;
  isActive: boolean;
}

export interface SubscriptionPlanDetail extends SubscriptionPlan {
  description: string | null;
  organizationCount: number;
  createdAt: string;
}

export interface UpsertPlanInput {
  name: string;
  description?: string | null;
  monthlyPrice: number;
  maxSites?: number | null;
  maxNasDevices?: number | null;
  maxUsers?: number | null;
  isActive: boolean;
}

export interface Site {
  id: string;
  organizationId: string;
  name: string;
  code: string;
  address: string | null;
  city: string | null;
  isPrimary: boolean;
  isActive: boolean;
}

export interface SiteListItem {
  id: string;
  organizationId: string;
  organizationName: string;
  name: string;
  code: string;
  city: string | null;
  isPrimary: boolean;
  isActive: boolean;
  walletBalance: number;
  createdAt: string;
}

export interface OrganizationProfile {
  id: string;
  name: string;
  contactPerson: string;
  mobileNumber: string;
  email: string;
  address: string | null;
}

export interface OrganizationSettings {
  organizationId: string;
  otpExpirySeconds: number;
  otpResendCooldownSeconds: number;
  otpMaxAttempts: number;
  sessionValiditySeconds: number;
  defaultMaxDevices: number;
  defaultDataLimitMb: number | null;
  timeZoneId: string;
}

export interface OrganizationBranding {
  organizationId: string;
  logoUrl: string | null;
  welcomeMessage: string | null;
  themeColor: string;
  backgroundUrl: string | null;
  termsAndConditions: string | null;
  privacyPolicy: string | null;
}

export interface SmsConfig {
  smsSettingId: string;
  organizationId: string | null;
  siteId: string | null;
  providerName: string;
  apiEndpoint: string;
  senderId: string;
  messageTemplate: string;
  isActive: boolean;
  hasApiKey: boolean;
}

export interface UpsertSmsInput {
  providerName?: string | null;
  apiEndpoint?: string | null;
  apiKey?: string | null;
  senderId: string;
  messageTemplate?: string | null;
  isActive: boolean;
}

export interface UpsertLoginMethodInput {
  authenticationType: AuthenticationType;
  isEnabled: boolean;
  isDefault: boolean;
  requireOTP: boolean;
  requireBookingValidation: boolean;
  allowMultipleDevices: boolean;
  maxDevices: number;
}

export interface AdminUser {
  id: string;
  organizationId: string | null;
  username: string;
  fullName: string;
  email: string;
  mobileNumber: string | null;
  isActive: boolean;
  hasAllSitesAccess: boolean;
  roles: string[];
  siteIds: string[];
  lastLoginAt: string | null;
}

export interface CreateAdminUserInput {
  organizationId?: string | null;
  username: string;
  password: string;
  fullName: string;
  email: string;
  mobileNumber?: string | null;
  hasAllSitesAccess: boolean;
  roleIds: string[];
  siteIds: string[];
}

export interface Role {
  id: string;
  organizationId: string | null;
  name: string;
  description: string | null;
  isSystem: boolean;
  isPlatformRole: boolean;
  permissionCodes: string[];
}

export interface PermissionInfo {
  id: string;
  code: string;
  description: string | null;
  isPlatformOnly: boolean;
}

export interface Wallet {
  id: string;
  siteId: string;
  organizationId: string;
  balance: number;
  currency: string;
  isActive: boolean;
}

export interface WalletTransaction {
  id: string;
  siteWalletId: string;
  type: 'Credit' | 'Debit';
  amount: number;
  balanceAfter: number;
  reference: string | null;
  description: string | null;
  createdAt: string;
}

export interface WalletOverview {
  walletId: string;
  siteId: string;
  siteName: string;
  siteCode: string;
  organizationId: string;
  organizationName: string;
  balance: number;
  currency: string;
  isActive: boolean;
  updatedAt: string;
}

export interface AuditLog {
  id: string;
  organizationId: string | null;
  adminUserId: string | null;
  action: string;
  entityType: string;
  entityId: string | null;
  details: string | null;
  ipAddress: string | null;
  createdAt: string;
}

export interface LabelCount { label: string; count: number }
export interface MonthCount { month: string; count: number }
export interface CountPair { total: number; active: number }

export interface WalletSummary {
  totalBalance: number;
  currency: string;
  walletCount: number;
  creditsLast30Days: number;
  debitsLast30Days: number;
  lowBalanceWallets: number;
}

export interface HotspotSummary {
  nasDevices: number;
  activeNasDevices: number;
  subscribers: number;
  activeSubscribers: number;
  packages: number;
  activeSessions: number;
  dataUsedBytesLast30Days: number;
  sessionsLast30Days: number;
}

export interface PlatformDashboard {
  totalOrganizations: number;
  organizationsByStatus: LabelCount[];
  organizationsByType: LabelCount[];
  sites: CountPair;
  adminUsers: CountPair;
  subscriptionPlans: CountPair;
  organizationsByPlan: LabelCount[];
  wallet: WalletSummary;
  hotspot: HotspotSummary;
  registrationsByMonth: MonthCount[];
  recentOrganizations: OrganizationSummary[];
  recentActivity: AuditLog[];
}

export interface SiteStatistics {
  siteId: string;
  name: string;
  code: string;
  isActive: boolean;
  walletBalance: number;
  nasDevices: number;
  subscribers: number;
  activeSessions: number;
}

export interface OrganizationStatistics {
  organizationId: string;
  name: string;
  organizationTypeName: string;
  status: OrganizationStatus;
  subscriptionPlanName: string | null;
  maxSites: number | null;
  maxNasDevices: number | null;
  maxUsers: number | null;
  sites: CountPair;
  adminUsers: CountPair;
  wallet: WalletSummary;
  hotspot: HotspotSummary;
  siteBreakdown: SiteStatistics[];
  recentActivity: AuditLog[];
}
