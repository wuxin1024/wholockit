param(
    [string]$Target = "locked_file.txt"
)

$targetPath = Join-Path $PSScriptRoot $Target

if (-not (Test-Path $targetPath)) {
    New-Item -Path $targetPath -ItemType File -Force | Out-Null
    Write-Host "Created: $targetPath" -ForegroundColor Green
}

$stream = [System.IO.File]::Open($targetPath, [System.IO.FileMode]::OpenOrCreate, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)

Write-Host ("Locked: $targetPath") -ForegroundColor Yellow
Write-Host ("PID: $pid") -ForegroundColor Cyan
Write-Host "Press Enter to release lock..."
Read-Host

$stream.Close()
$stream.Dispose()
Write-Host "Released." -ForegroundColor Green
