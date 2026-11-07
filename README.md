# NXT Hotspot

Multi-tenant Wi-Fi hotspot management SaaS with a custom C# RADIUS server (no FreeRADIUS). Windows Server, PostgreSQL, ASP.NET Core 8, React 19.

**Status (Phase 3):** admin console for Super Admins, organization admins and site admins; hotspot domain (NAS devices,
internet packages, subscriber groups, subscribers by venue type, hotel check-in/out, Excel import/export); custom RADIUS
server (authentication, accounting, disconnect, monitoring); captive-portal sign-in (mobile OTP, hotel room + mobile,
student/staff/employee ID + password with optional OTP); sessions, accounting and usage reports.
Verified with unit tests, `radclient` interoperability and browser tests, **not yet on real MikroTik hardware** —
see the limitations in [Documentation/Phase3-Platform.md](Documentation/Phase3-Platform.md#6-remaining-limitations-honest-list).

## Repository layout

| Folder | Content |
|---|---|
| `Backend/` | `NxtHotspot.Domain`, `.Application`, `.Infrastructure` (EF Core, services), `.API` (REST + Swagger) |
| `RadiusServer/` | `NxtHotspot.RadiusProtocol` (RFC 2865/2866/5176 engine), `NxtHotspot.Radius.Application` (auth/accounting/disconnect), `NxtHotspot.RadiusServer` (UDP listeners, Windows Service) |
| `Frontend/` | React + Vite + TypeScript admin console and the public captive portal (`/p/{key}`) |
| `Database/` | migration and update scripts |
| `Tests/` | xUnit tests (128) |
| `Documentation/` | architecture, data model, Phase 3 platform, Windows setup |

## Quick start (Windows)

See [Documentation/Windows-Setup.md](Documentation/Windows-Setup.md). In short:

```powershell
dotnet build NxtHotspot.sln
dotnet test Tests/NxtHotspot.Tests
dotnet run --project Backend/NxtHotspot.API --launch-profile NxtHotspot.API
dotnet run --project RadiusServer/NxtHotspot.RadiusServer
cd Frontend; npm install; npm run dev
```

Secrets (connection string, JWT key, encryption key, bootstrap Super Admin) come from environment variables or
user-secrets, never from source control. SMS API keys and NAS shared secrets are stored AES-GCM encrypted and are
never returned by the API.

## Documentation

- `Documentation/Phase3-Platform.md` – modules, database changes, API endpoints, routes, test results, limitations
- `Documentation/Windows-Setup.md` – install, configure and run everything on Windows
- `Documentation/Architecture.md` – layout, RADIUS server design, phase plan
- `Documentation/MultiTenancy-and-Database.md` – tenancy, login methods, identity model, portal hand-off
