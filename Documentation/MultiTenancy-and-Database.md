# NXT Hotspot – Multi-Tenant, Multi-Industry Design and Database

Status: design. Entities marked **P2** are implemented in code in Phase 2 (unbuilt, see README). Others are implemented in the phase shown.

## 1. Principles

1. One platform, no per-industry apps. Industry behaviour comes from **data**: `OrganizationTypes`, per-type default login methods, per-org settings.
2. Tenant = `Organization`. Branch = `Site`. NAS, SSIDs, plans, users, wallets hang off a Site; everything carries `OrganizationId` for isolation.
3. The tenant is **never** taken from a client-supplied `OrganizationId`. Admin APIs derive it from the JWT; the captive portal derives it from an opaque portal key mapped to a NAS/Site (section 5).
4. Secrets (SMS API keys, NAS shared secrets, CHAP credentials) are encrypted at rest (AES-GCM) and never returned by any API or written to logs.
5. Money is a ledger: wallet balance changes only through append-only transactions.

## 2. Entity groups

### Tenancy and admin (P2)
| Table | Purpose / key constraints |
|---|---|
| OrganizationTypes | `OrganizationTypeId` (int PK), `TypeName` unique, `Description`, `IsActive`. Seeded with the 14 types; editable data, not an enum. |
| OrganizationTypeDefaultLoginMethods | Default login methods and flags per type, copied into an org at registration. |
| SubscriptionPlans | Name, price, limits (`MaxSites`, `MaxNasDevices`, `MaxUsers`; null = unlimited). |
| Organizations | Name, type FK, plan FK, contact, status (`Pending/Active/Suspended/Inactive`). |
| OrganizationSettings | 1:1. OTP expiry, resend cooldown, max attempts, session validity, default max devices, time zone. |
| OrganizationBranding | 1:1. Logo URL, welcome message, theme colour, background, T&C, privacy policy. |
| Sites | Branch. Unique `(OrganizationId, Code)`. Exactly one primary site per org (filtered unique index). |
| OrganizationLoginMethods | Per org, optional per-site override. Fields exactly as specified (`LoginMethodId`, `AuthenticationType`, `IsEnabled`, `IsDefault`, `RequireOTP`, `RequireBookingValidation`, `AllowMultipleDevices`, `MaxDevices`). Unique per `(Org, AuthenticationType)` at org level and per `(Site, AuthenticationType)` at site level. |
| OrganizationSmsSettings | Fields as specified. `OrganizationId` is **nullable**: null = platform default provider (Super Admin). Resolution order: site override, then org, then platform default. `ApiKeyEncrypted` only. |
| AdminUsers | `OrganizationId` null = platform (Super Admin side). Unique normalized username, lockout counters, `HasAllSitesAccess`. |
| Roles / Permissions / RolePermissions / AdminUserRoles / AdminUserSites | RBAC. System roles (`SuperAdmin`, `OrgAdmin`, `SiteAdmin`, `ReadOnly`) plus org-defined roles. Platform-only permissions can never be put in org roles. |
| SiteWallets / SiteWalletTransactions | One wallet per site. Balance `numeric(18,4)`, optimistic `Version`. Ledger rows are insert-only, with optional idempotency key. |
| AuditLogs | Who did what, in which tenant. |

### Identity and users (Phase 4)
One common identity, no duplicate user rows per industry:

- `HotspotUsers` (one row per human/account per org) with `HotspotUserTypeId` (Student, Staff, Faculty, Employee, HotelGuest, Guest, Subscriber...).
- `HotspotUserTypes`, `HotspotUserGroups` (class/section/department/batch/employee group; plans attach to groups).
- `StudentProfiles`, `StaffProfiles`, `EmployeeProfiles`: 1:1 extension tables keyed by `HotspotUserId`.
- `HotspotIdentityMappings`: `(OrganizationId, IdentityKind, NormalizedValue)` unique, pointing to a `HotspotUserId`. Kinds: StudentId, EnrollmentNumber, StaffId, EmployeeId, Mobile, Username, RoomNumber, VoucherCode. One user can have several identifiers (student ID and mobile) without a second user row.
- `HotspotExternalUsers`: identities that originate outside admin-managed rosters (OTP guests, future SSO). Always anchored to a `HotspotUsers` row.
- `Departments`, `Courses`, `StudentBatches` (college/university).
- Bulk import (CSV/XLSX) for students, staff, employees; validated, tenant-scoped, idempotent on identity mapping.

### NAS / WiFi (Phase 3, 7)
`HotspotNas` (RouterOS version v6/v7, router IP, RADIUS secret encrypted, ports), `NasApiCredentials` (encrypted), `NasStatusLogs`, `SiteWifiProfiles`, `NasWifiProfileMappings`, `HotspotWifiProfiles`, bandwidth policies, access schedules. SSID-to-login-method and profile-to-plan mappings are explicit tables. Router configuration through RouterOS API is separate from external AP/controller integration; do not assume the router manages SSIDs on third-party APs.

### Plans, hotel, auth, RADIUS, system (Phases 4, 6, 8)
As in the original brief: plans/policies, `HotelRooms`, `HotelGuestBookings` (room + registered mobile + check-in/out), `HotspotRoomCredentials`, `HotspotOTPRequests` (OTP stored **hashed**), `HotspotSMSLogs`, vouchers and batches, `RadiusNasClients`, `RadiusUsers`, check/reply attributes, `RadiusAccounting`, `RadiusSessions` (`HotspotSessions` maps here), `RadiusRequestLogs`, payments, system settings, job logs.

## 3. Login methods

`AuthenticationType`: MobileOTP, RoomMobile, StudentPassword, StudentOTP, StaffPassword, EmployeePassword, Voucher, UsernamePassword, GuestOTP.

Defaults seeded per type (editable by Super Admin, copied at registration, then owned by the org):

| Type | Enabled (first = default) |
|---|---|
| Hotel, Resort | RoomMobile, MobileOTP, Voucher |
| School | StudentPassword, StaffPassword, GuestOTP |
| College, University | StudentPassword, StudentOTP, StaffPassword, GuestOTP |
| Hospital | MobileOTP |
| Hostel | UsernamePassword, MobileOTP |
| Cafe, Restaurant | MobileOTP, Voucher |
| Office, Corporate | EmployeePassword, MobileOTP, Voucher |
| Public WiFi | MobileOTP, Voucher |
| ISP | UsernamePassword, Voucher, MobileOTP |
| Other | MobileOTP, UsernamePassword, Voucher |

Hotel rule: **room number alone is never a credential.** RoomMobile requires an active booking for that room, matching registered mobile, within check-in/out; optional OTP on top. Access is revoked automatically at checkout (background job + CoA/Disconnect).

## 4. SMS (2Factor.in) per tenant

- Provider config lives in `OrganizationSmsSettings`; the key is encrypted and only a `hasApiKey` flag is ever returned.
- Endpoint is stored per setting but must be `https` and its host must be in an allowlist (default `2factor.in`) to prevent the server being used for SSRF.
- Default URL shape follows what you gave: `https://2factor.in/API/R1/?module=TRANS_SMS&apikey={key}&to={mobile}&from={sender}&msg={message}`. I have not verified the current 2Factor documentation; the endpoint stays configurable and the "test SMS" feature (Phase 8) is how each tenant verifies it. In India, transactional SMS normally need a DLT-registered sender ID and template, so the message template must match what is registered.
- The API key travels in a query string, so HTTP client logging must redact it (done in Phase 8 when the client is written).
- Abuse controls (each OTP send costs wallet money): per-mobile resend cooldown, max attempts, per-IP/MAC rate limits, per-site daily SMS cap, wallet balance check before send. Every send writes `HotspotSMSLogs` and a wallet debit.

## 5. Captive portal: tenant resolution and router handoff

**Tenant identification.** The router's hotspot login page redirects to `https://portal/p/{portalKey}`. `portalKey` is a random 128-bit value stored (hashed) against a NAS/Site, configured once on the router. The portal resolves NAS, Site, Org from it, then loads that org's branding and **only the login methods enabled for that site**. MikroTik query parameters (`mac`, `ip`, `link-login`, `link-orig`, `identity`) are treated as untrusted data and never choose the tenant. Because forging a key is infeasible, an attacker cannot make another tenant pay for SMS.

**OTP success is not a router session.** After the portal-side validation (OTP, room+mobile, student ID+password, voucher) succeeds, the backend:
1. Resolves/creates the `HotspotUser` and assigns the plan, checking device limits.
2. Issues a short-lived, single-use RADIUS credential bound to that user and the device MAC (stored hashed for PAP).
3. Returns a form that auto-submits to the router's `link-login` (`http-pap`; prefer HTTPS login on the router).
4. The router sends Access-Request to the custom RADIUS server, which validates the credential, plan, expiry, quota, simultaneous-use, and replies with Accept plus Session-Timeout/Rate-Limit.
5. Accounting from the router creates the session record.

A MAC-authentication pre-authorization flow is a possible later alternative. Both must be verified on real RouterOS (v6 and v7) before anything is called working.

## 6. RBAC and tenant isolation (P2)

- JWT carries admin id, org id (absent for platform admins), `all_sites` flag, role names, permission codes.
- Endpoints use permission policies (`[Authorize(Policy = "sites.write")]`).
- Org checks: non-platform admins can only touch their own `OrganizationId`.
- Site checks: an admin without `HasAllSitesAccess` can only touch sites listed in `AdminUserSites`; default for new admins is **no sites** (safe by default).
- Privilege-escalation guards: only platform admins assign platform roles; nobody can grant a role containing permissions they do not hold; org roles cannot contain platform-only permissions.
- Platform-only permissions: `organizations.write`, `wallet.credit`, `subscriptionplans.manage`, `organizationtypes.manage`.
- Login: PBKDF2 hashing (ASP.NET Core `PasswordHasher`), generic error messages, lockout after 5 failures for 15 minutes, rate-limited endpoint.

## 7. Registration workflow (Super Admin, P2)

Single database save (atomic):
1. Validate type is active, plan exists, admin username unique, SMS endpoint allowed, password policy.
2. Create Organization (type, plan, contact, status).
3. Create primary Site.
4. Create OrganizationSettings and OrganizationBranding.
5. Create admin user with `OrgAdmin` role and all-sites access.
6. Create SMS setting from supplied provider values (key encrypted), or leave unset to fall back to the platform default.
7. Create login methods from supplied list, or the type's defaults.
8. Create primary-site wallet; if initial balance > 0 write a Credit ledger entry.
9. Status `Active` unless the request says otherwise. Audit log written.

## 8. Defaults chosen (no need to ask)

- Super Admin is bootstrapped once from configuration (`Bootstrap:SuperAdmin:*`, supplied via environment or user-secrets); there is no default password in the repo.
- Currency INR; time zone Asia/Kolkata.
- OTP: 6 digits, 5 min expiry, 60 s resend cooldown, 5 attempts.
- Logo is stored as a URL in Phase 2; file upload arrives with the captive portal (Phase 9).
- Refresh tokens are deferred; Phase 2 issues 30-minute access tokens.
- Payment gateway integration (wallet top-ups by clients) is deferred; Super Admin credits wallets manually for now.

## 9. Phase 2 scope (this change)

Implemented: org types, subscription plans, organizations, settings, branding, sites, login methods, SMS settings (+ resolver), admin users, roles, permissions, wallets and ledger, audit logs, seeding, auth, registration, site/role/admin-user/wallet APIs, tests.

Deferred to later phases: remaining entity groups above, refresh tokens, org-type/plan CRUD screens (read endpoints only in Phase 2), organization profile edit, client-admin UI.
