# Phase 2 – Windows build, migrate and run

> **Status honesty:** this code was written in an environment with no .NET SDK. It has **not been compiled and its tests have not been run**.
> No EF Core migration exists yet; you generate it in step 4 (it needs the SDK). Expect to fix a few compile errors on the first build.

## Prerequisites
- .NET 8 SDK, PostgreSQL 16 (Windows installer), PowerShell, Git.

## 1. Create the database
```powershell
psql -U postgres -c "CREATE DATABASE nxthotspot;"
```

## 2. Configure secrets (user-secrets, never commit)
```powershell
cd Backend\NxtHotspot.API
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:NxtHotspot" "Host=localhost;Database=nxthotspot;Username=postgres;Password=YOUR_PASSWORD"
dotnet user-secrets set "Jwt:Key" "$([Convert]::ToBase64String((1..48 | % { Get-Random -Max 256 })))"
dotnet user-secrets set "Security:EncryptionKey" "$([Convert]::ToBase64String((1..32 | % { Get-Random -Max 256 })))"
dotnet user-secrets set "Bootstrap:SuperAdmin:Username" "superadmin"
dotnet user-secrets set "Bootstrap:SuperAdmin:Password" "REPLACE-WITH-A-STRONG-PASSWORD"
```
(Use a cryptographic RNG for production keys; `Get-Random` is fine for local development only.)
Keep a copy of `Security:EncryptionKey`: losing it makes stored SMS API keys unreadable.

## 3. Build and test
```powershell
cd <repo root>
dotnet restore
dotnet build
dotnet test Tests\NxtHotspot.Tests
```

## 4. Generate and apply the initial migration
```powershell
dotnet tool install --global dotnet-ef --version 8.*
.\Database\Scripts\New-Migration.ps1 -Name InitialCreate
$env:NXT_DESIGN_CONNECTION = "Host=localhost;Database=nxthotspot;Username=postgres;Password=YOUR_PASSWORD"
.\Database\Scripts\Update-Database.ps1
```
Review the generated files under `Backend\NxtHotspot.Infrastructure\Persistence\Migrations` (check the CHECK constraints and filtered unique indexes appear), then commit them.
Alternatively set `Database:MigrateOnStartup=true` (already on in Development).

## 5. Run
```powershell
dotnet run --project Backend\NxtHotspot.API
```
Swagger (Development): https://localhost:<port>/swagger. Log in with `POST /api/auth/login`, then use the bearer token.
The seeder is idempotent: it creates permissions, system roles, the 14 organization types with default login methods, subscription plans and the bootstrap Super Admin.
