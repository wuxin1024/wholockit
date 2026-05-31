Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32 {
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$settingsDir = "$env:LOCALAPPDATA\WhoLockIt"
$settingsFile = "$settingsDir\settings.txt"

# Create a test file and lock it
$testFile = "$projectRoot\test_locks\locked_file.txt"
$null = New-Item -ItemType Directory -Force -Path (Split-Path $testFile)
if (-not (Test-Path $testFile)) {
    "This is a locked test file for screenshot." | Out-File -FilePath $testFile -Encoding utf8
}

# Lock the file in a background job (holds FileShare.None lock)
$lockJob = Start-Job -ScriptBlock {
    param($path)
    $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::OpenOrCreate,
        [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
    # Keep alive until signaled
    while ($true) {
        Start-Sleep -Seconds 1
        if ((Get-Item $path -Force -ErrorAction SilentlyContinue) -eq $null) { break }
    }
    try { $stream.Close(); $stream.Dispose() } catch {}
} -ArgumentList $testFile
Start-Sleep -Seconds 1
Write-Host "Test file locked: $testFile"

function Capture-WhoLockIt {
    param([string]$outputPath)
    $proc = $null
    for ($i = 0; $i -lt 30; $i++) {
        $proc = Get-Process -Name WhoLockIt -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($proc -and $proc.MainWindowHandle -ne [IntPtr]::Zero) { break }
        Start-Sleep -Seconds 1
    }
    if (-not $proc -or $proc.MainWindowHandle -eq [IntPtr]::Zero) {
        Write-Host "ERROR: WhoLockIt window not found"
        return
    }
    Start-Sleep -Seconds 1
    $hwnd = $proc.MainWindowHandle
    [Win32]::ShowWindow($hwnd, 9)  # SW_RESTORE
    [Win32]::SetForegroundWindow($hwnd) | Out-Null
    Start-Sleep -Milliseconds 500
    $rect = New-Object Win32+RECT
    [Win32]::GetWindowRect($hwnd, [ref]$rect)
    $w = $rect.Right - $rect.Left
    $h = $rect.Bottom - $rect.Top
    Write-Host "Window: ${w}x${h} at ($($rect.Left), $($rect.Top))"
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size $w, $h))
    $bmp.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Host "Saved: $outputPath"
}

# === Chinese screenshot ===
Write-Host "--- Taking Chinese screenshot ---"
$null = New-Item -ItemType Directory -Force -Path $settingsDir
"zh-CN" | Out-File -FilePath $settingsFile -Encoding utf8
Stop-Process -Name WhoLockIt -Force -ErrorAction SilentlyContinue
Start-Process "$projectRoot\publish\WhoLockIt.exe" -ArgumentList $testFile
Start-Sleep -Seconds 5
Capture-WhoLockIt "$projectRoot\screenshot_zh-CN.png"
Stop-Process -Name WhoLockIt -Force -ErrorAction SilentlyContinue

# === English screenshot ===
Write-Host "--- Taking English screenshot ---"
"en-US" | Out-File -FilePath $settingsFile -Encoding utf8
Start-Process "$projectRoot\publish\WhoLockIt.exe" -ArgumentList $testFile
Start-Sleep -Seconds 5
Capture-WhoLockIt "$projectRoot\screenshot_en-US.png"
Stop-Process -Name WhoLockIt -Force -ErrorAction SilentlyContinue

# Clean up lock
Stop-Job -Job $lockJob -ErrorAction SilentlyContinue
Remove-Job -Job $lockJob -Force -ErrorAction SilentlyContinue
Write-Host "Done!"
