param(
    [string]$Target = "locked_folder"
)

$folderPath = Join-Path $PSScriptRoot $Target

if (-not (Test-Path $folderPath)) {
    New-Item -Path $folderPath -ItemType Directory -Force | Out-Null
    Write-Host "Created folder: $folderPath" -ForegroundColor Green
}

# Create a file inside and lock it — this prevents folder deletion
$innerFile = Join-Path $folderPath "inner_file.txt"
if (-not (Test-Path $innerFile)) {
    New-Item -Path $innerFile -ItemType File -Force | Out-Null
}

# Lock the inner file
$fileStream = [System.IO.File]::Open($innerFile, [System.IO.FileMode]::OpenOrCreate, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)

Write-Host ("Locked folder: $folderPath") -ForegroundColor Yellow
Write-Host ("  holding: $innerFile") -ForegroundColor DarkGray
Write-Host ("  PID: $pid") -ForegroundColor Cyan
Write-Host "Press Enter to release..."
Read-Host

$fileStream.Close()
$fileStream.Dispose()
Write-Host "Released." -ForegroundColor Green
