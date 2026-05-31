using System.Diagnostics;
using System.IO;
using WhoLockIt.Models;
using WhoLockIt.Native;

namespace WhoLockIt.Services;

public class UnlockService
{
    public record UnlockResult(int Attempted, int Succeeded, List<string> Errors);

    public async Task<UnlockResult> UnlockAllAsync(List<LockInfo> locks)
    {
        int succeeded = 0;
        var errors = new List<string>();

        await Task.Run(() =>
        {
            foreach (var lockInfo in locks)
            {
                if (lockInfo.IsCritical)
                {
                    errors.Add($"{lockInfo.ProcessName} (PID:{lockInfo.Pid}) 是系统关键进程，无法结束");
                    continue;
                }

                if (lockInfo.Source == "NtApi" && lockInfo.HandleValue != IntPtr.Zero)
                {
                    if (ProcessHelper.CloseRemoteHandle(lockInfo.Pid, lockInfo.HandleValue))
                    {
                        succeeded++;
                    }
                    else
                    {

                        if (ProcessHelper.TerminateProcess(lockInfo.Pid))
                            succeeded++;
                        else
                            errors.Add($"无法关闭 {lockInfo.ProcessName} (PID:{lockInfo.Pid}) 的句柄");
                    }
                }
                else
                {
                    if (ProcessHelper.TerminateProcess(lockInfo.Pid))
                        succeeded++;
                    else
                        errors.Add($"无法结束 {lockInfo.ProcessName} (PID:{lockInfo.Pid})");
                }
            }
        });

        return new UnlockResult(locks.Count, succeeded, errors);
    }

    public async Task<bool> DeleteFileOrFolderAsync(string path)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }
                else
                {
                    return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task<UnlockResult> UnlockSelectedAsync(LockInfo lockInfo)
    {
        var errors = new List<string>();

        if (lockInfo.IsCritical)
        {
            errors.Add($"{lockInfo.ProcessName} 是系统关键进程，无法结束");
            return new UnlockResult(1, 0, errors);
        }

        bool ok = await Task.Run(() =>
        {
            if (lockInfo.Source == "NtApi" && lockInfo.HandleValue != IntPtr.Zero)
            {
                if (ProcessHelper.CloseRemoteHandle(lockInfo.Pid, lockInfo.HandleValue))
                    return true;
            }
            return ProcessHelper.TerminateProcess(lockInfo.Pid);
        });

        if (!ok)
            errors.Add($"无法解锁 {lockInfo.ProcessName} (PID:{lockInfo.Pid})");

        return new UnlockResult(1, ok ? 1 : 0, errors);
    }
}
