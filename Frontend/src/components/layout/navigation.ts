import type { IconType } from 'react-icons';
import { FiActivity, FiBriefcase, FiCreditCard, FiDroplet, FiGrid, FiKey, FiLayers, FiMapPin, FiMessageSquare, FiPackage, FiRadio, FiShield, FiTag, FiUserCheck, FiUsers, FiWifi, FiDatabase, FiBarChart2, FiServer } from 'react-icons/fi';
import { Permissions as P } from '../../types/permissions';

export type NavScope = 'platform' | 'organization' | 'all';

export interface NavItem {
  label: string;
  path: string;
  icon: IconType;
  /** Shown only when the admin holds at least one of these permissions. Empty = everyone. */
  anyOf: readonly string[];
  scope: NavScope;
}

export interface NavSection {
  title: string;
  items: NavItem[];
}

/** Single source of truth for the menu. Items are filtered by scope and permissions at runtime. */
export const NAV_SECTIONS: NavSection[] = [
  {
    title: 'Overview',
    items: [{ label: 'Dashboard', path: '/', icon: FiGrid, anyOf: [], scope: 'all' }],
  },
  {
    title: 'Tenants',
    items: [
      { label: 'Organizations', path: '/organizations', icon: FiBriefcase, anyOf: [P.OrganizationsRead], scope: 'platform' },
      { label: 'Sites', path: '/all-sites', icon: FiMapPin, anyOf: [P.SitesRead], scope: 'platform' },
      { label: 'Wallets', path: '/wallets', icon: FiCreditCard, anyOf: [P.WalletRead], scope: 'platform' },
    ],
  },
  {
    title: 'Network',
    items: [
      { label: 'Sites', path: '/sites', icon: FiMapPin, anyOf: [P.SitesRead], scope: 'organization' },
      { label: 'NAS devices', path: '/nas', icon: FiRadio, anyOf: [P.NasRead], scope: 'organization' },
    ],
  },
  {
    title: 'Hotspot users',
    items: [
      { label: 'Subscribers', path: '/subscribers', icon: FiUserCheck, anyOf: [P.SubscribersRead], scope: 'organization' },
      { label: 'Groups & policies', path: '/groups', icon: FiLayers, anyOf: [P.SubscribersRead], scope: 'organization' },
      { label: 'Internet packages', path: '/packages', icon: FiPackage, anyOf: [P.PackagesRead], scope: 'organization' },
    ],
  },
  {
    title: 'Sessions & usage',
    items: [
      { label: 'Active sessions', path: '/active-sessions', icon: FiWifi, anyOf: [P.SessionsRead], scope: 'all' },
      { label: 'Accounting', path: '/accounting', icon: FiDatabase, anyOf: [P.SessionsRead], scope: 'all' },
      { label: 'Usage reports', path: '/reports', icon: FiBarChart2, anyOf: [P.ReportsRead], scope: 'all' },
      { label: 'RADIUS monitor', path: '/radius', icon: FiServer, anyOf: [P.SessionsRead], scope: 'all' },
    ],
  },
  {
    title: 'Configuration',
    items: [
      { label: 'Authentication', path: '/authentication', icon: FiKey, anyOf: [P.SettingsRead], scope: 'organization' },
      { label: 'SMS provider', path: '/sms', icon: FiMessageSquare, anyOf: [P.SettingsRead], scope: 'organization' },
      { label: 'Branding', path: '/branding', icon: FiDroplet, anyOf: [P.SettingsRead], scope: 'organization' },
      { label: 'Wallet', path: '/wallets', icon: FiCreditCard, anyOf: [P.WalletRead], scope: 'organization' },
    ],
  },
  {
    title: 'Access control',
    items: [
      { label: 'Administrators', path: '/administrators', icon: FiUsers, anyOf: [P.AdminUsersRead], scope: 'all' },
      { label: 'Roles & permissions', path: '/roles', icon: FiShield, anyOf: [P.RolesRead], scope: 'all' },
    ],
  },
  {
    title: 'Platform',
    items: [
      { label: 'Organization types', path: '/organization-types', icon: FiTag, anyOf: [P.OrganizationTypesRead], scope: 'platform' },
      { label: 'Subscription plans', path: '/subscription-plans', icon: FiLayers, anyOf: [P.SubscriptionPlansRead], scope: 'platform' },
      { label: 'Platform SMS', path: '/platform-sms', icon: FiMessageSquare, anyOf: [P.PlatformSmsWrite], scope: 'platform' },
      { label: 'Audit log', path: '/audit-logs', icon: FiActivity, anyOf: [P.AuditRead], scope: 'all' },
    ],
  },
];

export function visibleSections(isPlatformAdmin: boolean, hasAnyPermission: (codes: readonly string[]) => boolean): NavSection[] {
  return NAV_SECTIONS.map((section) => ({
    ...section,
    items: section.items.filter(
      (item) =>
        (item.scope === 'all' || (item.scope === 'platform') === isPlatformAdmin) &&
        (item.anyOf.length === 0 || hasAnyPermission(item.anyOf)),
    ),
  })).filter((section) => section.items.length > 0);
}

/** Page title for the top bar: longest matching menu path. */
export function titleForPath(pathname: string): string {
  let best: NavItem | undefined;
  for (const item of NAV_SECTIONS.flatMap((s) => s.items)) {
    const matches = item.path === '/' ? pathname === '/' : pathname === item.path || pathname.startsWith(`${item.path}/`);
    if (matches && (!best || item.path.length > best.path.length)) best = item;
  }
  return best?.label ?? 'NXT Hotspot';
}
