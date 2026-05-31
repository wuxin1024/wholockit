param(
    [Parameter(Mandatory=$true)]
    [string]$Target
)

$targetPath = Join-Path $PSScriptRoot $Target

if (-not (Test-Path $targetPath)) {
    Write-Host "File not found: $targetPath" -ForegroundColor Red
    exit 1
}

Write-Host "Trying to release: $targetPath" -ForegroundColor Cyan

# Approach: use Restart Manager to see who's locking
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class Rm {
    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    public static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, StringBuilder strSessionKey);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    public static extern int RmRegisterResources(uint dwSessionHandle, uint nFiles, string[] rgsFileNames, uint nApplications, object[] rgApplications, uint nServices, string[] rgsServiceNames);

    [DllImport("rstrtmgr.dll")]
    public static extern int RmGetList(uint dwSessionHandle, out uint pnProcInfoNeeded, ref uint pnProcInfo, RM_PROCESS_INFO[] rgAffectedApps, ref uint lpdwRebootReasons);

    [DllImport("rstrtmgr.dll")]
    public static extern int RmShutdown(uint dwSessionHandle, uint lFlags, IntPtr fnStatus);

    [DllImport("rstrtmgr.dll")]
    public static extern int RmEndSession(uint dwSessionHandle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct RM_PROCESS_INFO {
        public uint ProcessId;
        public uint ProcessNameOffset;
        public uint AppNameOffset;
        public uint ServiceNameOffset;
        public uint AppType;
        public uint AppStatus;
        public uint TSSessionId;
        [MarshalAs(UnmanagedType.Bool)] public bool bRestartable;
    }
}
'@

$session = 0u
$res = [Rm]::RmStartSession([ref]$session, 0, [Text.StringBuilder]::new())
if ($res -ne 0) { Write-Host "Failed to start RM session (error $res)" -ForegroundColor Red; exit 1 }

$res = [Rm]::RmRegisterResources($session, 1, @($targetPath), 0, $null, 0, $null)
if ($res -ne 0) { Write-Host "No locks found or RM error $res" -ForegroundColor Yellow; [Rm]::RmEndSession($session); exit 0 }

$info = New-Object 'Rm+RM_PROCESS_INFO'[] 10
$count = 10u
$reboot = 0u
$needed = 0u

$res = [Rm]::RmGetList($session, [ref]$needed, [ref]$count, $info, [ref]$reboot)
if ($res -ne 0 -or $count -eq 0) {
    Write-Host "No locking processes found." -ForegroundColor Yellow
    [Rm]::RmEndSession($session)
    exit 0
}

Write-Host "Found $count process(es) locking the file:" -ForegroundColor Yellow
for ($i = 0; $i -lt $count; $i++) {
    $proc = Get-Process -Id $info[$i].ProcessId -ErrorAction SilentlyContinue
    Write-Host "  PID $($info[$i].ProcessId): $($proc?.ProcessName ?? 'unknown')"
}

Write-Host ""
$confirm = Read-Host "Kill these processes? (y/N)"
if ($confirm -ne 'y' -and $confirm -ne 'Y') {
    Write-Host "Cancelled."
    [Rm]::RmEndSession($session)
    exit 0
}

for ($i = 0; $i -lt $count; $i++) {
    $proc = Get-Process -Id $info[$i].ProcessId -ErrorAction SilentlyContinue
    if ($proc) {
        Write-Host "Killing PID $($info[$i].ProcessId) ($($proc.ProcessName))..." -ForegroundColor Red
        Stop-Process -Id $info[$i].ProcessId -Force -ErrorAction SilentlyContinue
    }
}

[Rm]::RmEndSession($session)
Write-Host "Done." -ForegroundColor Green
