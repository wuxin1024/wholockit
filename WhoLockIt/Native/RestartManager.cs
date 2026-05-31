using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace WhoLockIt.Native;

public static class RestartManager
{
    private const int CCH_RM_SESSION_KEY = 32;
    private const int CCH_RM_MAX_APP_NAME = 255;
    private const int CCH_RM_MAX_SVC_NAME = 63;
    private const int RM_SESSION_KEY_LEN = CCH_RM_SESSION_KEY * 2;
    private const int ERROR_SUCCESS = 0;
    private const int ERROR_MORE_DATA = 234;

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmEndSession(uint dwSessionHandle);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(
        uint dwSessionHandle,
        uint nFiles,
        string[] rgsFileNames,
        uint nApplications,
        RM_UNIQUE_PROCESS[]? rgApplications,
        uint nServices,
        string[]? rgsServiceNames);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmGetList(
        uint dwSessionHandle,
        out uint pnProcInfoNeeded,
        ref uint pnProcInfo,
        [In, Out] RM_PROCESS_INFO[] rgAffectedApps,
        ref uint lpdwRebootReasons);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct RM_UNIQUE_PROCESS
    {
        public uint dwProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
        public string strAppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
        public string strServiceShortName;
        public RM_APP_TYPE ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;
    }

    public enum RM_APP_TYPE
    {
        RmUnknownApp = 0,
        RmMainWindow = 1,
        RmOtherWindow = 2,
        RmService = 3,
        RmExplorer = 4,
        RmConsole = 5,
        RmCritical = 1000
    }

    public class LockProcessInfo
    {
        public uint ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public RM_APP_TYPE AppType { get; set; }
        public string LockType { get; set; } = "File";
    }

    public static List<LockProcessInfo> GetLockingProcesses(string filePath)
    {
        var result = new List<LockProcessInfo>();
        uint sessionHandle;

        string sessionKey = Guid.NewGuid().ToString();
        int ret = RmStartSession(out sessionHandle, 0, sessionKey);
        if (ret != ERROR_SUCCESS)
            return result;

        try
        {
            var files = new List<string>();

            if (Directory.Exists(filePath))
            {
                // Register files inside the directory — RestartManager may not detect
                // locks on children when only the parent directory is registered
                try
                {
                    files.AddRange(Directory.EnumerateFiles(filePath, "*", SearchOption.TopDirectoryOnly).Take(256));
                }
                catch { }
            }

            // Always include the original path (file or directory)
            if (files.Count == 0)
                files.Add(filePath);

            ret = RmRegisterResources(sessionHandle, (uint)files.Count, files.ToArray(), 0, null, 0, null);
            if (ret != ERROR_SUCCESS)
                return result;

            uint procInfoNeeded = 0;
            uint procInfoCount = 0;
            uint rebootReasons = 0;

            ret = RmGetList(sessionHandle, out procInfoNeeded, ref procInfoCount, null!, ref rebootReasons);
            if (ret == ERROR_MORE_DATA && procInfoNeeded > 0)
            {
                var procInfos = new RM_PROCESS_INFO[procInfoNeeded];
                procInfoCount = procInfoNeeded;
                ret = RmGetList(sessionHandle, out procInfoNeeded, ref procInfoCount, procInfos, ref rebootReasons);
                if (ret == ERROR_SUCCESS)
                {
                    for (int i = 0; i < procInfoCount; i++)
                    {
                        result.Add(new LockProcessInfo
                        {
                            ProcessId = procInfos[i].Process.dwProcessId,
                            ProcessName = procInfos[i].strAppName,
                            AppType = procInfos[i].ApplicationType,
                            LockType = procInfos[i].ApplicationType switch
                            {
                                RM_APP_TYPE.RmService => "Service",
                                RM_APP_TYPE.RmExplorer => "Explorer",
                                RM_APP_TYPE.RmConsole => "Console",
                                _ => "File"
                            }
                        });
                    }
                }
            }
        }
        finally
        {
            RmEndSession(sessionHandle);
        }

        return result;
    }
}
