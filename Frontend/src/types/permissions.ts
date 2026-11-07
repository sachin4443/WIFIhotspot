/** Permission codes from NxtHotspot.Application.Common.Permissions. Keep in sync with the backend. */
export const Permissions = {
  OrganizationsRead: 'organizations.read',
  OrganizationsWrite: 'organizations.write',
  SitesRead: 'sites.read',
  SitesWrite: 'sites.write',
  AdminUsersRead: 'adminusers.read',
  AdminUsersWrite: 'adminusers.write',
  RolesRead: 'roles.read',
  RolesWrite: 'roles.write',
  WalletRead: 'wallet.read',
  WalletCredit: 'wallet.credit',
  WalletDebit: 'wallet.debit',
  OrganizationTypesRead: 'organizationtypes.read',
  OrganizationTypesWrite: 'organizationtypes.write',
  SubscriptionPlansRead: 'subscriptionplans.read',
  SettingsRead: 'settings.read',
  SettingsWrite: 'settings.write',
  AuditRead: 'audit.read',
  PlatformSmsWrite: 'platform.sms.write',
} as const;

export type PermissionCode = (typeof Permissions)[keyof typeof Permissions];
