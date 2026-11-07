import type { IconType } from 'react-icons';
import {
  FiActivity,
  FiBriefcase,
  FiCreditCard,
  FiGrid,
  FiLayers,
  FiMapPin,
  FiMessageSquare,
  FiSettings,
  FiShield,
  FiUsers,
} from 'react-icons/fi';
import { Permissions } from '../../types/permissions';

export interface NavItem {
  label: string;
  path: string;
  icon: IconType;
  /** Shown only when the admin holds at least one of these permissions. Empty = everyone. */
  anyOf: readonly string[];
  /** Phase 3 module that delivers the page. Items whose module is not built yet are shown disabled. */
  module: number;
}

export interface NavSection {
  title: string;
  items: NavItem[];
}

/** Highest Phase 3 module whose pages exist. Raise as modules are delivered. */
export const BUILT_MODULE = 1;

export const NAV_SECTIONS: NavSection[] = [
  {
    title: 'Overview',
    items: [{ label: 'Dashboard', path: '/', icon: FiGrid, anyOf: [], module: 1 }],
  },
  {
    title: 'Network',
    items: [
      { label: 'Organizations', path: '/organizations', icon: FiBriefcase, anyOf: [Permissions.OrganizationsRead], module: 3 },
      { label: 'Sites', path: '/sites', icon: FiMapPin, anyOf: [Permissions.SitesRead], module: 4 },
    ],
  },
  {
    title: 'Access control',
    items: [
      { label: 'Admin users', path: '/admin-users', icon: FiUsers, anyOf: [Permissions.AdminUsersRead], module: 5 },
      { label: 'Roles', path: '/roles', icon: FiShield, anyOf: [Permissions.RolesRead], module: 5 },
    ],
  },
  {
    title: 'Billing',
    items: [
      { label: 'Wallets', path: '/wallets', icon: FiCreditCard, anyOf: [Permissions.WalletRead], module: 6 },
      { label: 'Subscription plans', path: '/subscription-plans', icon: FiLayers, anyOf: [Permissions.SubscriptionPlansRead], module: 6 },
    ],
  },
  {
    title: 'Configuration',
    items: [
      { label: 'SMS & login', path: '/authentication', icon: FiMessageSquare, anyOf: [Permissions.SettingsRead, Permissions.PlatformSmsWrite], module: 7 },
      { label: 'Audit logs', path: '/audit-logs', icon: FiActivity, anyOf: [Permissions.AuditRead], module: 8 },
      { label: 'Platform settings', path: '/platform', icon: FiSettings, anyOf: [Permissions.OrganizationTypesWrite], module: 8 },
    ],
  },
];
