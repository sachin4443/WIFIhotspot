<#
 Applies all pending migrations to PostgreSQL.
 Usage:
   $env:NXT_DESIGN_CONNECTION = 'Host=localhost;Database=nxthotspot;Username=postgres;Password=...'
   .\Database\Scripts\Update-Database.ps1
 Optional: -Script writes idempotent SQL to .\Database\Scripts\migrate.sql instead of applying it.
#>
param([switch]$Script)
$ErrorActionPreference = 'Stop'
if (-not $env:NXT_DESIGN_CONNECTION) { throw "Set NXT_DESIGN_CONNECTION to your PostgreSQL connection string first." }
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Push-Location $root
try {
    $common = @('--project', 'Backend\NxtHotspot.Infrastructure', '--startup-project', 'Backend\NxtHotspot.API')
    if ($Script) { dotnet ef migrations script --idempotent @common --output Database\Scripts\migrate.sql }
    else { dotnet ef database update @common }
    if ($LASTEXITCODE -ne 0) { throw "dotnet ef failed." }
} finally { Pop-Location }
