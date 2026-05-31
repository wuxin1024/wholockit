namespace WhoLockIt.Models;

public class LockInfo
{
    public string ProcessName { get; set; } = "";
    public uint Pid { get; set; }
    public string LockType { get; set; } = "File";
    public string FilePath { get; set; } = "";
    public IntPtr HandleValue { get; set; }
    public bool IsCritical { get; set; }
    public string Source { get; set; } = ""; // "RestartManager" or "NtApi"
}
