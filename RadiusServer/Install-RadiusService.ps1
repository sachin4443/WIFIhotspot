<#
 Installs the NXT RADIUS server as a Windows Service (run in an elevated PowerShell).
 Usage:
   .\RadiusServer\Install-RadiusService.ps1 -PublishDir C:\NxtHotspot\Radius
 Then set the service's configuration (connection string and encryption key) as machine environment variables:
   [Environment]::SetEnvironmentVariable('ConnectionStrings__NxtHotspot', 'Host=...;Database=nxthotspot;Username=nxt_app;Password=...', 'Machine')
   [Environment]::SetEnvironmentVariable('Security__EncryptionKey', '<same key as the API>', 'Machine')
 and allow UDP 1812/1813 inbound from your routers only.
#>
param(
    [Parameter(Mandatory = $true)][string]$PublishDir,
    [string]$ServiceName = 'NxtHotspotRadius'
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
dotnet publish (Join-Path $root 'RadiusServer\NxtHotspot.RadiusServer\NxtHotspot.RadiusServer.csproj') -c Release -o $PublishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
$exe = Join-Path $PublishDir 'NxtHotspot.RadiusServer.exe'
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service $ServiceName -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}
New-Service -Name $ServiceName -BinaryPathName "`"$exe`"" -DisplayName 'NXT Hotspot RADIUS Server' -StartupType Automatic -Description 'Custom C# RADIUS authentication, accounting and disconnect server for NXT Hotspot.'
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/5000/restart/30000 | Out-Null
if (-not (Get-NetFirewallRule -DisplayName 'NXT RADIUS UDP 1812-1813' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName 'NXT RADIUS UDP 1812-1813' -Direction Inbound -Protocol UDP -LocalPort 1812,1813 -Action Allow | Out-Null
    Write-Host 'Firewall rule added. Restrict its RemoteAddress to your routers.' -ForegroundColor Yellow
}
Write-Host "Service '$ServiceName' installed. Set the machine environment variables, then: Start-Service $ServiceName" -ForegroundColor Green
