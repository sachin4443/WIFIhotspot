# NXT Hotspot – Windows setup and run (Phase 3)

Supersedes `Windows-Setup-Phase2.md` for day-to-day use. Commands are PowerShell, run from the repository root.

## 1. Prerequisites

| Component | Version | Note |
|---|---|---|
| .NET SDK | 8.0 (keep your 10.0 SDK too) | `winget install Microsoft.DotNet.SDK.8` – the projects target .NET 8 |
| Node.js | 20.19+ or 22.12+ | `node -v` |
| PostgreSQL | 18 (16+ works) | database `nxthotspot`, owner `nxt_app` (see Phase 2 guide) |
| EF Core tool | 8.x | `dotnet tool install --global dotnet-ef --version 8.*` |

## 2. Update your working copy

```powershell
git checkout main
git pull
```

(After the Phase 3 pull request is merged. To try it before merging: `git fetch` then `git checkout feature/complete-saas-platform`.)

## 3. Apply the new migration

The `HotspotRadiusPortal` migration only adds tables and indexes. Review it, then apply it to `nxthotspot`:

```powershell
$env:NXT_DESIGN_CONNECTION = "Host=localhost;Port=5432;Database=nxthotspot;Username=nxt_app;Password=YOUR_DB_PASSWORD"
.\Database\Scripts\Update-Database.ps1
Remove-Item Env:NXT_DESIGN_CONNECTION
```

(With `ASPNETCORE_ENVIRONMENT=Development` the API also migrates on start-up, because `appsettings.Development.json`
sets `Database:MigrateOnStartup=true`.)

## 4. API configuration (user secrets, unchanged from Phase 2) and new optional settings

Your existing secrets (`ConnectionStrings:NxtHotspot`, `Jwt:Key`, `Security:EncryptionKey`) keep working. Optional:

```powershell
cd Backend\NxtHotspot.API
# Address your MikroTik routers use to reach the RADIUS server (fills in generated router config)
dotnet user-secrets set "Radius:PublicAddress" "192.168.1.10"
# Public URL of the captive portal (the frontend), used in portal links
dotnet user-secrets set "Radius:PortalBaseUrl" "http://192.168.1.10:5173"
# Local testing only: write OTP codes to the API log instead of sending SMS (refused outside Development)
dotnet user-secrets set "Sms:DeliveryMode" "DevelopmentLog"
cd ..\..
```

Run the API (Visual Studio profile `NxtHotspot.API`, or):

```powershell
dotnet run --project Backend\NxtHotspot.API --launch-profile NxtHotspot.API
```

## 5. RADIUS server

The RADIUS server must use the **same database and the same `Security:EncryptionKey`** as the API (NAS shared
secrets are encrypted with it).

```powershell
cd RadiusServer\NxtHotspot.RadiusServer
dotnet user-secrets set "ConnectionStrings:NxtHotspot" "Host=localhost;Port=5432;Database=nxthotspot;Username=nxt_app;Password=YOUR_DB_PASSWORD"
dotnet user-secrets set "Security:EncryptionKey" "<the same value as the API>"
cd ..\..
$env:DOTNET_ENVIRONMENT = "Development"   # user secrets load only in Development
dotnet run --project RadiusServer\NxtHotspot.RadiusServer
```

You should see `NXT RADIUS server listening: authentication 0.0.0.0:1812/udp, accounting 0.0.0.0:1813/udp`.
The **RADIUS monitor** page then shows the server as Online within 15 seconds.

As a Windows Service (elevated PowerShell):

```powershell
.\RadiusServer\Install-RadiusService.ps1 -PublishDir C:\NxtHotspot\Radius
[Environment]::SetEnvironmentVariable('ConnectionStrings__NxtHotspot', 'Host=localhost;Database=nxthotspot;Username=nxt_app;Password=YOUR_DB_PASSWORD', 'Machine')
[Environment]::SetEnvironmentVariable('Security__EncryptionKey', '<same key as the API>', 'Machine')
Start-Service NxtHotspotRadius
```

Allow UDP 1812/1813 inbound **only from your routers' IPs** (the script creates a rule you should narrow).

## 6. Frontend (admin console and captive portal)

```powershell
cd Frontend
npm install
npm run dev
```

Open http://localhost:5173. To let phones on the hotspot reach the portal, run `npm run dev -- --host` (or build
with `npm run build` and serve `Frontend\dist` from IIS/nginx, setting `VITE_API_BASE_URL` to the API URL and adding
the portal origin to the API's `Cors:AllowedOrigins`).

## 7. First end-to-end setup

1. Sign in as Super Admin → **Organizations → Register organization** (e.g. type Hotel).
2. Sign in as that organization's admin.
3. **NAS devices → Add NAS**: the router's source IP and a shared secret (12+ characters).
4. **Config** on the NAS row: copy the RouterOS commands, replace `<SHARED_SECRET>`, run them on the router.
5. **Portal key** on the NAS row: copy the portal URL (shown once). On the router, replace `hotspot/login.html`
   with a page that redirects to `PORTAL_URL?mac=$(mac)&ip=$(ip)&link-login-only=$(link-login-only)&dst=$(link-orig-esc)`.
6. **Internet packages**: create e.g. "Guest 24 h · 10 Mbps".
7. **Subscribers → Room guests → Check in guest** (or add students/employees, or import from Excel).
8. Connect a phone to the hotspot → the portal opens → sign in → the router asks NXT RADIUS → online.
9. Watch **Active sessions**, **Accounting**, **Usage reports** and **RADIUS monitor**.

## 8. Testing RADIUS without a router (optional)

From WSL or any Linux machine with `freeradius-utils` (client tools only):

```bash
printf 'User-Name = "student1"\nUser-Password = "their-password"\n' | radclient -x 192.168.1.10:1812 auth YOUR_NAS_SECRET
```

The NAS IP registered in NXT must equal the source IP of that machine.

## 9. Build and test

```powershell
dotnet build NxtHotspot.sln
dotnet test Tests\NxtHotspot.Tests
cd Frontend; npm run build
```

Expected: 0 errors, 0 warnings, 128 tests passing, frontend build clean.
