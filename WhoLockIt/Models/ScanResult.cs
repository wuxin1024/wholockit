namespace WhoLockIt.Models;

public class ScanResult
{
    public string FilePath { get; set; } = "";
    public List<LockInfo> Locks { get; set; } = [];
    public bool FromRestartManager { get; set; }
    public bool FromNtApi { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Elapsed { get; set; }

    public bool HasResults => Locks.Count > 0;
    public bool AllCritical => Locks.Count > 0 && Locks.TrueForAll(l => l.IsCritical);
}
