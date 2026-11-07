/* Mirrors of the hotspot DTOs (NAS, packages, groups, subscribers). */

export type NasVendor = 'MikroTik' | 'Other';
export type SubscriberType = 'Guest' | 'RoomGuest' | 'Student' | 'Teacher' | 'Staff' | 'Employee' | 'Customer';
export type SubscriberStatus = 'Active' | 'Suspended' | 'Expired' | 'CheckedOut';
export type SubscriberGroupType = 'Department' | 'Class' | 'Course' | 'Batch' | 'Floor' | 'Other';

export const SUBSCRIBER_TYPES: { value: SubscriberType; label: string; plural: string; idLabel: string }[] = [
  { value: 'RoomGuest', label: 'Room guest', plural: 'Room guests', idLabel: 'Guest reference' },
  { value: 'Guest', label: 'Guest', plural: 'Guests', idLabel: 'Mobile / ID' },
  { value: 'Student', label: 'Student', plural: 'Students', idLabel: 'Student ID / enrollment no.' },
  { value: 'Teacher', label: 'Teacher', plural: 'Teachers', idLabel: 'Staff ID' },
  { value: 'Staff', label: 'Staff', plural: 'Staff', idLabel: 'Staff ID' },
  { value: 'Employee', label: 'Employee', plural: 'Employees', idLabel: 'Employee ID' },
  { value: 'Customer', label: 'Customer', plural: 'Customers', idLabel: 'Username' },
];
export const typeInfo = (t: SubscriberType) => SUBSCRIBER_TYPES.find((x) => x.value === t)!;
export const GROUP_TYPES: SubscriberGroupType[] = ['Department', 'Class', 'Course', 'Batch', 'Floor', 'Other'];

/** Subscriber workflows offered per organization type (by type name). */
export function subscriberTypesFor(orgTypeName: string | undefined): SubscriberType[] {
  const n = (orgTypeName ?? '').toLowerCase();
  if (/hotel|resort/.test(n)) return ['RoomGuest', 'Guest', 'Staff'];
  if (/school/.test(n)) return ['Student', 'Teacher', 'Staff', 'Guest'];
  if (/college|university/.test(n)) return ['Student', 'Teacher', 'Staff', 'Guest'];
  if (/office|corporate|enterprise/.test(n)) return ['Employee', 'Guest'];
  if (/isp|hostel/.test(n)) return ['Customer', 'Guest'];
  if (/hospital/.test(n)) return ['Guest', 'Staff'];
  return ['Guest', 'Customer'];
}

export interface Nas {
  id: string; organizationId: string; siteId: string; siteName: string; name: string; ipAddress: string; vendor: NasVendor;
  routerOsVersion: string | null; authPort: number; acctPort: number; coaPort: number; description: string | null; isActive: boolean;
  hasSharedSecret: boolean; hasPortalKey: boolean; lastSeenAt: string | null; activeSessions: number; createdAt: string;
}
export interface UpsertNasInput {
  siteId: string; name: string; ipAddress: string; vendor: NasVendor; routerOsVersion: string | null; sharedSecret: string | null;
  authPort: number; acctPort: number; coaPort: number; description: string | null; isActive: boolean;
}
export interface PortalKey { nasId: string; portalKey: string; portalUrl: string }
export interface RouterConfig { nasId: string; routerOsVersion: string; script: string; notes: string[] }

export interface InternetPackage {
  id: string; organizationId: string; siteId: string | null; siteName: string | null; name: string; description: string | null;
  downloadKbps: number; uploadKbps: number; dataLimitMb: number | null; validityMinutes: number | null; sessionTimeoutMinutes: number | null;
  idleTimeoutMinutes: number | null; maxDevices: number; price: number; isActive: boolean; subscriberCount: number; createdAt: string;
}
export type UpsertPackageInput = Omit<InternetPackage, 'id' | 'organizationId' | 'siteName' | 'subscriberCount' | 'createdAt'>;

export interface SubscriberGroup {
  id: string; organizationId: string; name: string; groupType: SubscriberGroupType; description: string | null;
  defaultPackageId: string | null; defaultPackageName: string | null; isActive: boolean; subscriberCount: number;
}
export interface UpsertGroupInput { name: string; groupType: SubscriberGroupType; description: string | null; defaultPackageId: string | null; isActive: boolean }

export interface Subscriber {
  id: string; organizationId: string; siteId: string | null; siteName: string | null; subscriberType: SubscriberType; identifier: string;
  fullName: string; mobileNumber: string | null; email: string | null; hasPassword: boolean; groupId: string | null; groupName: string | null;
  packageId: string | null; packageName: string | null; roomNumber: string | null; checkInAt: string | null; checkOutAt: string | null;
  validUntil: string | null; status: SubscriberStatus; maxDevices: number | null; notes: string | null; lastLoginAt: string | null;
  activeSessions: number; createdAt: string;
}

export interface SubscriberQuery { type?: SubscriberType | ''; status?: SubscriberStatus | ''; siteId?: string; groupId?: string; search?: string; page: number; pageSize: number }

export interface SubscriberInput {
  subscriberType?: SubscriberType; identifier?: string | null; fullName: string; mobileNumber: string | null; email: string | null;
  password?: string | null; newPassword?: string | null; clearPassword?: boolean; siteId: string | null; groupId: string | null; packageId: string | null;
  roomNumber: string | null; checkInAt: string | null; checkOutAt: string | null; validUntil: string | null; maxDevices: number | null; notes: string | null;
}

export interface ImportResult { created: number; updated: number; skipped: number; errors: { row: number; message: string }[] }
