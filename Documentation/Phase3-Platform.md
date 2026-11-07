# NXT Hotspot – Phase 3: Complete SaaS platform

This document describes what Phase 3 delivered: the light-theme admin console for every role, the hotspot
domain (NAS, packages, subscribers), the custom C# RADIUS server, the captive-portal sign-in workflows,
sessions/accounting/reports and RADIUS monitoring. Windows setup is in [Windows-Setup.md](Windows-Setup.md).

## 1. Modules

| # | Module | Backend | Frontend | Verified |
|---|---|---|---|---|
| 1 | Light SaaS design system, responsive shell, UI kit | – | sidebar/drawer, top bar, tables → mobile cards, modals → bottom sheets, toasts, confirm dialogs, charts | screenshots at 8 viewports |
| 2 | Dashboards, account context, plans, site/wallet directories | `AccountService`, `DashboardService`, `PlanService`, `SiteDirectoryService` | platform dashboard, organization dashboard | unit tests + browser |
| 3 | Super Admin: organizations (7-step registration), types, plans, sites, wallets, admins, roles, platform SMS, audit log | existing Phase 2 services + new endpoints | all Super Admin pages | Playwright CRUD run |
| 4 | Hotspot domain: NAS (encrypted secrets, portal key, RouterOS config), internet packages, groups, subscribers by type, hotel check-in/out, Excel import/export | `NasService`, `PackageService`, `SubscriberGroupService`, `SubscriberService` | NAS, packages, groups, subscribers pages | unit tests + PostgreSQL smoke test |
| 5 | Organization and site admin portal | site scoping in every service | org menu, org pages, workflow tabs per organization type | Playwright org-admin run, site-admin scoping |
| 6 | Custom RADIUS server | `RadiusProtocol`, `Radius.Application`, `RadiusServer` (UDP 1812/1813, disconnect worker, heartbeat) | – | RFC 2865 vectors, handler tests, `radclient` interop |
| 7 | Hotspot sign-in workflows (captive portal) | `PortalService`, `SmsSender`, shared `AccessPolicy` | public portal page `/p/{key}` | portal → credential → RADIUS Access-Accept |
| 8 | Sessions, accounting, usage reports, RADIUS monitor | `SessionService`, `UsageReportService`, `RadiusMonitorService` | active sessions (disconnect), accounting (export), reports (charts), RADIUS monitor | browser run against the running RADIUS server |

### Organization types and workflows

Seeded types: Hotel, Resort, School, College, University, Hospital, Hostel, Cafe, Restaurant, Office, Corporate,
Public WiFi, ISP, **Enterprise**, **Shopping Mall**, **Railway / Transport** (new), Other. A Super Admin can add more.

| Venue | Default login methods | Subscriber tabs in the console |
|---|---|---|
| Hotel / Resort | Room + mobile, Mobile OTP, Voucher | Room guests, Guests, Staff |
| School | Student password, Staff password, Guest OTP | Students, Teachers, Staff, Guests |
| College / University | Student password, Student OTP, Staff password, Guest OTP | Students, Teachers, Staff, Guests |
| Office / Corporate / Enterprise | Employee password, Mobile OTP, Voucher | Employees, Guests |
| ISP / Hostel | Username + password, Voucher, Mobile OTP | Customers, Guests |
| Public WiFi, Cafe, Restaurant, Mall, Transport | Mobile OTP, Voucher | Guests, Customers |

Every method can be enabled, made default, given "require OTP", device limits and (for rooms) booking validation,
per organization and per site, by the organization admin.

### How a hotspot sign-in works

```
Phone ──► MikroTik hotspot ──redirect──► NXT portal /p/{portal key}?mac=…&link-login-only=…
                                          │  room+mobile / OTP / ID+password (+OTP)
                                          ▼
                              NXT API issues a one-time RADIUS credential
                              (single use, 2 minutes, bound to the device MAC)
                                          │  portal posts it to link-login-only
                                          ▼
MikroTik ──Access-Request (PAP)──► NXT RADIUS server ──► AccessPolicy ──► Access-Accept
         ◄── Session-Timeout, Idle-Timeout, Mikrotik-Rate-Limit, Mikrotik-Total-Limit, Class
MikroTik ──Accounting Start / Interim / Stop──► RadiusSessions (active sessions, reports)
Admin ──Disconnect──► DisconnectCommands ──► RADIUS server ──Disconnect-Request (RFC 5176)──► MikroTik
```

The same `AccessPolicy` runs in the portal (before issuing a credential) and in the RADIUS server (on every
Access-Request): organization/site/subscriber status, site scope, hotel stay window, validity, package expiry,
data quota and simultaneous-device limit.

## 2. Database changes and migrations

| Migration | Content | Destructive? |
|---|---|---|
| `20261107100215_InitialCreate` | Phase 1/2 schema (generated on your Windows machine) | – |
| `20261107113317_HotspotRadiusPortal` | 12 new tables, 31 indexes: `NasDevices`, `InternetPackages`, `SubscriberGroups`, `Subscribers`, `RadiusCredentials`, `OtpChallenges`, `SmsLogs`, `RadiusSessions`, `RadiusAccountingEvents`, `RadiusRequestLogs`, `DisconnectCommands`, `RadiusServerStatuses` | **No.** Only `CreateTable`/`CreateIndex`; existing tables are untouched. |

Notable constraints: NAS IP unique platform-wide (RADIUS identifies routers by source IP); subscriber identifier
unique per organization (case-insensitive); package name unique per organization; group name unique per
organization and type; `(NasId, AcctSessionId)` unique for sessions; accounting idempotency key unique.

Seed changes (idempotent, applied at API start-up): three new organization types and 11 new permissions
(`subscriptionplans.write`, `nas.*`, `packages.*`, `subscribers.*`, `sessions.read`, `sessions.disconnect`,
`reports.read`, `radius.monitor`). Newly added permissions are granted to existing system roles whose defaults
include them; permissions an administrator removed earlier are not re-added.

## 3. API endpoints (91 operations)

All endpoints except `/api/auth/login`, `/api/portal/*` and `/health` need a bearer token. Organization and site
scope always comes from the token; another tenant's data returns 403/404.

| Area | Endpoints | Permission |
|---|---|---|
| Auth & account | `POST /api/auth/login` · `GET /api/auth/me` · `GET /api/account/context` | anonymous / signed in |
| Dashboards | `GET /api/dashboard/platform` · `GET /api/dashboard/organizations/{id}` | organizations.read (platform) / signed in |
| Organizations | `GET/POST /api/organizations` · `GET /api/organizations/{id}` · `PUT …/{id}/status` · `PUT …/{id}/subscription-plan` | organizations.read / .write |
| Organization config | `GET/PUT …/{orgId}/profile` · `…/settings` · `…/branding` · `GET/PUT …/login-methods` · `DELETE …/login-methods/{id}` · `GET/PUT …/sites/{siteId}/login-methods` · `GET/PUT/DELETE …/sms` · `PUT/DELETE …/sites/{siteId}/sms` | settings.read / .write |
| Reference & platform | `GET /api/organization-types` · `GET /api/subscription-plans` · `GET/POST/PUT /api/platform/organization-types[/{id}]` · `GET/POST/PUT /api/platform/subscription-plans[/{id}]` · `GET/PUT /api/platform/sms` | organizationtypes.*, subscriptionplans.*, platform.sms.write |
| Sites | `GET /api/sites` · `GET/POST /api/organizations/{orgId}/sites` · `PUT /api/sites/{siteId}` | sites.read / .write |
| Wallets | `GET /api/wallets` · `GET /api/sites/{siteId}/wallet` · `GET …/wallet/transactions` · `POST …/wallet/credit` · `POST …/wallet/debit` (Idempotency-Key header) | wallet.read / .credit / .debit |
| Admins & roles | `GET/POST /api/admin-users` · `PUT …/{id}/roles` · `PUT …/{id}/sites` · `PUT …/{id}/active` · `GET /api/roles` · `GET /api/permissions` · `POST /api/roles` · `PUT /api/roles/{id}/permissions` · `DELETE /api/roles/{id}` | adminusers.*, roles.* |
| NAS | `GET/POST …/{orgId}/nas` · `PUT/DELETE …/nas/{nasId}` · `POST …/nas/{nasId}/portal-key` · `GET …/nas/{nasId}/router-config` | nas.read / .write |
| Packages | `GET/POST …/{orgId}/packages` · `PUT/DELETE …/packages/{id}` | packages.read / .write |
| Groups | `GET/POST …/{orgId}/subscriber-groups` · `PUT/DELETE …/subscriber-groups/{id}` | subscribers.read / .write |
| Subscribers | `GET/POST …/{orgId}/subscribers` · `GET/PUT/DELETE …/subscribers/{id}` · `PUT …/{id}/status` · `POST …/{id}/check-out` · `GET …/subscribers/import-template` · `POST …/subscribers/import` (multipart .xlsx) · `GET …/subscribers/export` | subscribers.read / .write |
| Sessions & accounting | `GET /api/sessions` · `GET /api/sessions/export` · `POST /api/sessions/{id}/disconnect` · `GET /api/sessions/disconnects` | sessions.read / .disconnect |
| Reports | `GET /api/reports/usage?from&to&siteId&organizationId` | reports.read |
| RADIUS monitor | `GET /api/radius/overview` · `GET /api/radius/requests` | sessions.read (platform sees all) |
| Captive portal | `GET /api/portal/{key}/config` · `POST /api/portal/{key}/otp` · `POST /api/portal/{key}/login` | anonymous, 30 req/min per IP |
| Audit | `GET /api/audit-logs` | audit.read |
| Health | `GET /health` | anonymous |

Secrets are write-only everywhere: SMS API keys and NAS shared secrets are AES-GCM encrypted and only a
`hasApiKey`/`hasSharedSecret` flag is returned; portal keys are stored as SHA-256 and shown once; OTP codes are
stored as HMAC-SHA256; subscriber and admin passwords are PBKDF2 hashes.

## 4. Frontend routes

| Route | Who | Page |
|---|---|---|
| `/login` | everyone | sign-in |
| `/` | all | platform dashboard (Super Admin) or organization dashboard |
| `/organizations`, `/organizations/new`, `/organizations/:id?tab=…` | platform | list, registration wizard, detail (overview, profile, sites, NAS, packages, subscribers, admins, login methods, SMS, OTP & sessions, branding, wallet) |
| `/organization-types`, `/subscription-plans`, `/platform-sms`, `/all-sites` | platform | platform configuration |
| `/sites`, `/nas`, `/subscribers`, `/groups`, `/packages` | organization / site admins | network and hotspot users |
| `/authentication`, `/sms`, `/branding` | organization admins | configuration |
| `/wallets`, `/wallets/:siteId` | wallet.read | balances and ledgers |
| `/active-sessions`, `/accounting`, `/reports`, `/radius` | sessions.read / reports.read | sessions, accounting, usage, RADIUS monitor |
| `/administrators`, `/roles`, `/audit-logs` | permission-based | access control and audit |
| `/p/:key` | public (hotspot visitors) | captive portal |

Menus show only items the user's scope and permissions allow; every route is also guarded, and the API enforces
the same rules.

## 5. Build and test results

| Check | Result |
|---|---|
| `dotnet build NxtHotspot.sln` | 0 errors, 0 warnings (the former MSB3277 EF Core version split is fixed) |
| `dotnet test` | 128 tests passing (85 from Phase 2 + 43 new) |
| RADIUS protocol | RFC 2865 §7.1 Access-Request and Access-Accept reproduced byte-for-byte; PAP, CHAP, Message-Authenticator, accounting/disconnect authenticators, malformed-packet rejection |
| RADIUS interop | `radclient` (FreeRADIUS client tool, used only as a test client) against the running NXT server: Access-Accept with `Mikrotik-Rate-Limit` decoded by radclient's own dictionary, Access-Reject, wrong secret dropped, accounting Start/Interim/Stop |
| Disconnect | API → RADIUS server → simulated RFC 5176 NAS: Disconnect-Request authenticator verified by the NAS, Disconnect-ACK verified by NXT, session closed |
| Captive portal | browser sign-in (hotel room + mobile, mobile OTP) → issued credential accepted by RADIUS; replay rejected |
| Frontend | `npm run build` (tsc + vite) clean; Playwright runs for Super Admin (18 checks), org admin (17), portal (10), sessions/monitor (9) |
| Responsive | every page as Super Admin (19 pages), organization admin (17), site admin and the portal at 1920×1080, 1366×768, 1024×768, 768×1024, 430×932, 390×844, 375×812, 320×640: no horizontal overflow, no page errors |

## 6. Remaining limitations (honest list)

1. **No real MikroTik hardware or RouterOS CHR was available.** RADIUS was verified with `radclient` and a
   simulated NAS. The generated RouterOS configuration follows the RouterOS documentation but must be tested on
   your routers (v6 and v7) before production. Real-world CoA/Disconnect behaviour depends on the RouterOS build.
2. **SMS delivery was not exercised against a live provider** (no API key, outbound access to the provider was not
   available here). The sender follows the documented 2Factor URL shape; send one test OTP with your key first.
   `Sms:DeliveryMode=DevelopmentLog` exists only for local testing and is refused outside Development.
3. **PAP only for portal credentials and hashed passwords.** CHAP/MS-CHAP cannot be verified against hashed
   passwords; the hotspot must allow `http-pap` (the generated config does). EAP/802.1X is not supported.
4. **Vouchers** are listed as a login method but voucher generation/redemption is not implemented; the portal says so.
5. **Wallet charging for SMS/usage is not automated.** Wallets support manual credit/debit with a ledger; per-SMS or
   per-session charging rules were not specified.
6. **Usage reports attribute a session's traffic to the day it started** (UTC). Long sessions crossing midnight are
   not split per day.
7. **Single RADIUS instance tested.** Several instances can share the database (heartbeats are per instance), but
   load balancing/failover was not tested.
8. The captive portal is served by the admin frontend at `/p/{key}`; the separate `CaptivePortal/` folder is still a
   placeholder. Hosting the portal on its own domain is a deployment choice.
9. No automated browser tests are committed to the repository yet (they ran from a local scratch folder).
