<#
 Creates an EF Core migration for NxtHotspot (Windows PowerShell 5.1+ / PowerShell 7).
 Generating a migration does NOT connect to the database.
 Usage:  .\Database\Scripts\New-Migration.ps1 -Name InitialCreate
#>
param([Parameter(Mandatory = $true)][string]$Name)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Push-Location $root
try {
    dotnet tool restore 2>$null
    if (-not (Get-Command dotnet-ef -ErrorAction SilentlyContinue)) { dotnet tool install --global dotnet-ef --version 8.* }
    dotnet ef migrations add $Name `
        --project Backend\NxtHotspot.Infrastructure `
        --startup-project Backend\NxtHotspot.API `
        --output-dir Persistence\Migrations
    if ($LASTEXITCODE -ne 0) { throw "dotnet ef migrations add failed." }
    Write-Host "Migration '$Name' created under Backend\NxtHotspot.Infrastructure\Persistence\Migrations. Review it before applying." -ForegroundColor Green
} finally { Pop-Location }
