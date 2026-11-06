# NXT Hotspot

Multi-tenant WiFi hotspot management SaaS with a custom C# RADIUS server. Windows Server only; PostgreSQL; no FreeRADIUS.

See `Documentation/Windows-Setup-Phase2.md` for exact Windows build, migration and run commands.

**Current status:** Phase 1 (scaffold) and Phase 2 (organizations, sites, admin auth, RBAC, wallet) are written but **not yet compiled or run** (no .NET SDK was available where they were written). The RADIUS server does not listen on any port yet. Nothing here is production-ready.

- `Documentation/Architecture.md`: layout, RADIUS server design, phase plan
- `Documentation/MultiTenancy-and-Database.md`: tenancy, login methods, identity model, SMS, portal hand-off, data model

## First run (Windows or any machine with the .NET 8 SDK and PostgreSQL)

```powershell
dotnet build NxtHotspot.sln
dotnet test Tests/NxtHotspot.Tests
```

Then generate the initial migration (none exists yet) and apply it:

```powershell
dotnet ef migrations add Initial --project Backend/NxtHotspot.Infrastructure --startup-project Backend/NxtHotspot.API
dotnet ef database update --project Backend/NxtHotspot.Infrastructure --startup-project Backend/NxtHotspot.API
```

Secrets (connection string, JWT key, encryption key, bootstrap Super Admin) come from environment variables or user-secrets, never from source control.
