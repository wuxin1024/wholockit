using System.Diagnostics;
using System.IO;
using WhoLockIt.Models;
using WhoLockIt.Native;

namespace WhoLockIt.Services;

public class HandleScanner
{
    public bool IsAdmin => PrivilegeHelper.IsRunningAsAdmin();

    public async Task<ScanResult> ScanAsync(string filePath, CancellationToken ct = default)
    {
        var result = new ScanResult { FilePath = filePath };
        var sw = Stopwatch.StartNew();
        var allLocks = new Dictionary<uint, LockInfo>();

        filePath = Path.GetFullPath(filePath);

        await Task.Run(() =>
        {
            // Channel 1: Restart Manager (works without admin)
            try
            {
                var rmResults = RestartManager.GetLockingProcesses(filePath);
                foreach (var rmLock in rmResults)
                {
                    if (!allLocks.ContainsKey(rmLock.ProcessId))
                    {
                        allLocks[rmLock.ProcessId] = new LockInfo
                        {
                            ProcessName = rmLock.ProcessName,
                            Pid = rmLock.ProcessId,
                            LockType = rmLock.LockType,
                            FilePath = filePath,
                            Source = "RestartManager",
                            IsCritical = ProcessHelper.IsCriticalProcess(rmLock.ProcessName)
                        };
                    }
                }
                if (rmResults.Count > 0) result.FromRestartManager = true;
            }
            catch { }

            // Channel 2: NT API (requires admin)
            if (!IsAdmin) return;

            try
            {
                if (!PrivilegeHelper.EnableDebugPrivilege()) return;

                var handles = NtApi.EnumerateHandles();
                var processHandles = new Dictionary<uint, IntPtr>();
                var processNames = new Dictionary<uint, string>();
                string searchPath = filePath.ToLowerInvariant();

                foreach (var handle in handles)
                {
                    ct.ThrowIfCancellationRequested();
                    if (allLocks.ContainsKey(handle.UniqueProcessId)) continue;

                    if (!processHandles.TryGetValue(handle.UniqueProcessId, out var procHandle))
                    {
                        procHandle = ProcessHelper.GetProcessHandle(handle.UniqueProcessId, 0x1040);
                        processHandles[handle.UniqueProcessId] = procHandle;
                    }
                    if (procHandle == IntPtr.Zero) continue;

                    string? objectName = NtApi.GetObjectName(handle.Object, procHandle);
                    if (string.IsNullOrEmpty(objectName)) continue;

                    string resolvedPath = NtApi.ResolveNtPath(objectName).ToLowerInvariant();
                    string normalizedSearch = searchPath.EndsWith('\\') ? searchPath : searchPath + '\\';

                    if (resolvedPath.Equals(searchPath, StringComparison.Ordinal) ||
                        resolvedPath.StartsWith(normalizedSearch, StringComparison.Ordinal) ||
                        FilePathsOverlap(searchPath, resolvedPath))
                    {
                        if (!processNames.TryGetValue(handle.UniqueProcessId, out var procName))
                        {
                            procName = ProcessHelper.GetProcessName(handle.UniqueProcessId);
                            processNames[handle.UniqueProcessId] = procName;
                        }

                        allLocks[handle.UniqueProcessId] = new LockInfo
                        {
                            ProcessName = procName,
                            Pid = handle.UniqueProcessId,
                            LockType = "Handle",
                            FilePath = resolvedPath,
                            HandleValue = new IntPtr(handle.HandleValue),
                            Source = "NtApi",
                            IsCritical = ProcessHelper.IsCriticalProcess(procName)
                        };
                    }
                }

                foreach (var kvp in processHandles)
                    if (kvp.Value != IntPtr.Zero) NtApi.CloseHandle(kvp.Value);

                if (allLocks.Count > 0) result.FromNtApi = true;
            }
            catch (OperationCanceledException) { throw; }
            catch { }
        }, ct);

        result.Locks = allLocks.Values
            .OrderBy(l => l.IsCritical ? 0 : 1)
            .ThenBy(l => l.ProcessName)
            .ToList();

        sw.Stop();
        result.Elapsed = sw.Elapsed;
        return result;
    }

    private static bool FilePathsOverlap(string target, string candidate)
    {
        try
        {
            if (candidate.StartsWith("\\device\\", StringComparison.Ordinal) &&
                target.Length > 2 && target[1] == ':')
            {
                int thirdSlash = candidate.IndexOf('\\', 9);
                if (thirdSlash > 0)
                {
                    string candidateRest = candidate[thirdSlash..];
                    return candidateRest.Equals(target[2..], StringComparison.OrdinalIgnoreCase) ||
                           target[2..].StartsWith(candidateRest, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        catch { }
        return false;
    }
}
