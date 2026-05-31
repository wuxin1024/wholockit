param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "=== 1. Publish WhoLockIt ===" -ForegroundColor Cyan
Stop-Process -Name WhoLockIt -Force -ErrorAction SilentlyContinue
dotnet publish "$projectRoot\..\WhoLockIt\WhoLockIt.csproj" -c Release -o "$projectRoot\..\publish"
if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

Write-Host "=== 2. Build MSI Installer ===" -ForegroundColor Cyan
wix build -ext WixToolset.UI.wixext "$projectRoot\WhoLockIt.wxs" -o "$projectRoot\..\publish_installer\WhoLockIt_Setup.msi"
if ($LASTEXITCODE -ne 0) { throw "WiX build failed" }

$msiPath = "$projectRoot\..\publish_installer\WhoLockIt_Setup.msi"
$msiSize = [math]::Round((Get-Item $msiPath).Length / 1MB, 2)
Write-Host "Done: $msiPath ($msiSize MB)" -ForegroundColor Green
