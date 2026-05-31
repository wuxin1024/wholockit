param(
    [switch]$Debug,
    [switch]$NoClean
)

$project = "D:\claudAll\wholockit\WhoLockIt\WhoLockIt.csproj"
$publishDir = "D:\claudAll\wholockit\publish"
$config = if ($Debug) { "Debug" } else { "Release" }

if (-not $NoClean) {
    Write-Host "Cleaning publish output..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $publishDir -ErrorAction SilentlyContinue
}

Write-Host "Publishing ($config)..." -ForegroundColor Cyan
dotnet publish $project -c $config -o $publishDir

if ($LASTEXITCODE -eq 0) {
    Write-Host "Done: $publishDir\WhoLockIt.exe" -ForegroundColor Green
}
else {
    Write-Host "Build failed." -ForegroundColor Red
}
