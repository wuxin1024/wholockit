using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WhoLockIt.Native;

public static class ProcessHelper
{
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const uint PROCESS_DUP_HANDLE = 0x0040;
    private const uint PROCESS_TERMINATE = 0x0001;
    private const uint DUPLICATE_CLOSE_SOURCE = 0x00000001;
    private const uint DUPLICATE_SAME_ACCESS = 0x00000002;

    private static readonly HashSet<string> CriticalProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "System Idle Process", "Registry",
        "smss.exe", "csrss.exe", "wininit.exe", "winlogon.exe",
        "services.exe", "lsass.exe", "svchost.exe"
    };

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern uint NtDuplicateObject(
        IntPtr sourceProcessHandle,
        IntPtr sourceHandle,
        IntPtr targetProcessHandle,
        out IntPtr targetHandle,
        uint desiredAccess,
        uint handleAttributes,
        uint options);

    public static bool IsCriticalProcess(string processName)
    {
        return CriticalProcesses.Contains(processName);
    }

    public static Process? GetProcessById(uint pid)
    {
        try { return Process.GetProcessById((int)pid); }
        catch { return null; }
    }

    public static bool CloseRemoteHandle(uint pid, IntPtr handle)
    {
        IntPtr processHandle = OpenProcess(PROCESS_DUP_HANDLE, false, pid);
        if (processHandle == IntPtr.Zero)
            return false;

        try
        {
            IntPtr dupHandle;
            uint status = NtDuplicateObject(
                processHandle,
                handle,
                IntPtr.Zero,
                out dupHandle,
                0, 0,
                DUPLICATE_CLOSE_SOURCE);

            return status == 0;
        }
        finally
        {
            CloseHandle(processHandle);
        }
    }

    public static bool TerminateProcess(uint pid)
    {
        if (pid == 0 || pid == 4)
            return false;

        IntPtr processHandle = OpenProcess(PROCESS_TERMINATE, false, pid);
        if (processHandle == IntPtr.Zero)
        {
            return ForceTerminate(pid);
        }

        try
        {
            if (!TerminateProcess(processHandle, 1))
            {
                CloseHandle(processHandle);
                return ForceTerminate(pid);
            }
            return true;
        }
        finally
        {
            CloseHandle(processHandle);
        }
    }

    private static bool ForceTerminate(uint pid)
    {
        try
        {
            var process = Process.GetProcessById((int)pid);
            process.Kill();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string GetProcessName(uint pid)
    {
        try
        {
            return Process.GetProcessById((int)pid).ProcessName + ".exe";
        }
        catch
        {
            return $"PID:{pid}";
        }
    }

    public static IntPtr GetProcessHandle(uint pid, uint desiredAccess)
    {
        return OpenProcess(desiredAccess, false, pid);
    }
}
