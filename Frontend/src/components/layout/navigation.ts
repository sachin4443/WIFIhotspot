import type { IconType } from 'react-icons';
import { FiActivity, FiBriefcase, FiCreditCard, FiGrid, FiLayers, FiMapPin, FiMessageSquare, FiShield, FiTag, FiUsers } from 'react-icons/fi';
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
