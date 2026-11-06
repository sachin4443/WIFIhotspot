# NXT Hotspot – Architecture

Multi-tenant, multi-industry WiFi hotspot management SaaS with a custom C# RADIUS server. Windows Server only, PostgreSQL only, no FreeRADIUS.

**Where the details live**

- Tenancy, organization types, login methods, identity model, SMS, captive-portal tenant resolution, MikroTik hand-off, RBAC, registration workflow, entity list: `Documentation/MultiTenancy-and-Database.md`. That document is the source of truth for the data model and the Phase 2 design.
- This document covers the system layout and the custom RADIUS server design.

**Status.** Phase 1 (scaffold) and Phase 2 (organizations, sites, admin auth, RBAC, wallet) are written but have **not been compiled or run** in the session that last touched them: the .NET SDK was not available there. Nothing is production-ready, and nothing has been tested on MikroTik hardware.

## 1. Constraints

- No FreeRADIUS and no Linux dependency anywhere.
- The RADIUS server is a .NET 8 Worker Service hosted as a Windows Service.
- The Web API never opens UDP 1812/1813/3799. API and RADIUS server are separate processes sharing the PostgreSQL database.
- The NAS (MikroTik) enforces speed and access. The RADIUS server only returns attributes and never claims to enforce a policy the NAS does not support.
- One platform. Industry behaviour comes from data (organization types, login methods, profile tables), not from per-industry code paths.

## 2. Solution layout

```
NxtHotspot/
  Backend/
    NxtHotspot.Domain/          entities, enums
    NxtHotspot.Application/     DTOs, service interfaces, permissions, exceptions, password policy
    NxtHotspot.Infrastructure/  EF Core (Npgsql), services, security (hashing, AES-GCM), seeding
    NxtHotspot.API/             ASP.NET Core Web API: JWT, permission policies, controllers
  RadiusServer/
    NxtHotspot.RadiusProtocol/      protocol library only (no DB, no sockets)
    NxtHotspot.Radius.Application/  auth/accounting/CoA handlers and policy evaluation
    NxtHotspot.RadiusServer/        Worker Service and UDP listeners
  Tests/NxtHotspot.Tests/       xunit tests
  Frontend/                     React admin panels (Super Admin portal, Client Admin panel)
  CaptivePortal/                React portal
  Database/                     migrations and scripts
  Documentation/
```

Dependencies: Domain ← Application ← Infrastructure ← API. `RadiusProtocol` depends on nothing. `Radius.Application` uses Domain, Application and Infrastructure data access.

## 3. Custom RADIUS server (Phases 5–7)

**Listeners.** UDP sockets for authentication (1812), accounting (1813) and CoA/Disconnect (3799), ports configurable. Receive loops push datagrams into a bounded channel consumed by worker tasks, so a slow database call never blocks a socket. The NAS is identified by source IP; an unknown source is dropped silently (rate-limited log line). The shared secret is per NAS.

**Protocol engine (`RadiusProtocol`).**

- RFC 2865 header and attribute parsing with strict length checks; dictionary-driven attributes (standard plus MikroTik vendor 14988).
- Response Authenticator = MD5(Code + ID + Length + RequestAuthenticator + Attributes + Secret). Accounting-Request authenticator verification (RFC 2866).
- Message-Authenticator (HMAC-MD5) validated when present and required when the request carries EAP.
- PAP decoding (RFC 2865 §5.2) and CHAP verification (§5.3). MS-CHAP/MS-CHAPv2 stays disabled until verified against real interop vectors.
- Disconnect/CoA packet construction with correct authenticators (RFC 5176).

**Credential storage constraint.** PAP can be checked against a salted hash because the server receives the cleartext. CHAP and MS-CHAP cannot: the server needs reversible credential material to recompute the response. Where those methods are enabled the credential is stored encrypted (AES-GCM) in a separate column, only for the users or per-session credentials that need it. Default is PAP.

**Authentication pipeline.** Validate NAS and authenticator → identify user → verify credential → check status, plan, expiry, data quota, simultaneous-use limit → reply Accept (Session-Timeout, Idle-Timeout, Mikrotik-Rate-Limit, ...) or Reject → write `RadiusRequestLogs` (never passwords or OTPs).

**Accounting.** Idempotency key (NAS, Acct-Session-Id, Acct-Status-Type, Acct-Session-Time, Acct-Delay-Time) with a unique index, so duplicates are acknowledged but not re-applied. Gigaword counters combine into 64-bit totals. Interim updates upsert the session; Stop closes it.

**CoA / Disconnect.** Started from the API through a command table. The RADIUS worker sends the packet, waits for ACK/NAK with retry and timeout, and records the result. Disconnect is expected to work on MikroTik; CoA support varies by RouterOS version and configuration, so a NAK or timeout is reported as such.

**Windows Service.** `UseWindowsService()`, automatic start, recovery actions configured by the install script, Serilog (files + Event Log), health checks for DB and listeners, counters exposed through a status table the API reads.

## 4. Phase plan

| Phase | Scope | Exit criteria |
|---|---|---|
| 1 | Architecture and scaffold | written |
| 2 | Tenancy: org types, registration, sites, admin auth, RBAC, wallet | `dotnet build` and `dotnet test` green; EF migration generated and applied to PostgreSQL; Super Admin bootstrap verified |
| 3 | NAS, WiFi profiles/SSIDs, login-method and SMS admin APIs | tenant-isolation tests |
| 4 | Plans, bandwidth policies, identity model, student/staff import | import and policy tests |
| 5 | RADIUS protocol engine and Windows Worker Service | RFC test vectors pass; service listens |
| 6 | Authentication, accounting, sessions | PostgreSQL integration tests |
| 7 | MikroTik adapters (RouterOS v6/v7), CoA/Disconnect | tested on RouterOS CHR or hardware |
| 8 | Room/booking, OTP (2Factor), vouchers, portal hand-off | end to end |
| 9 | Captive portal and dashboards | UI against live API |
| 10 | Security review, deployment, documentation | clean-server install rehearsal |

"Production-ready" is not claimed before interoperability testing on real MikroTik hardware.
