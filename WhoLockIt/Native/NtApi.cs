using System.Runtime.InteropServices;

namespace WhoLockIt.Native;

public static class NtApi
{
    private const int SystemHandleInformation = 16;
    private const uint STATUS_SUCCESS = 0;
    private const uint STATUS_INFO_LENGTH_MISMATCH = 0xC0000004;
    private const uint STATUS_BUFFER_OVERFLOW = 0x80000005;
    private const uint DuplicateSameAccess = 0x00000002;
    private const int ObjectNameInformation = 1;

    [DllImport("ntdll.dll", SetLastError = true)]
    public static extern uint NtQuerySystemInformation(
        int systemInformationClass,
        IntPtr systemInformation,
        uint systemInformationLength,
        out uint returnLength);

    [DllImport("ntdll.dll", SetLastError = true)]
    public static extern uint NtQueryObject(
        IntPtr handle,
        int objectInformationClass,
        IntPtr objectInformation,
        uint objectInformationLength,
        out uint returnLength);

    [DllImport("ntdll.dll", SetLastError = true)]
    public static extern uint NtDuplicateObject(
        IntPtr sourceProcessHandle,
        IntPtr sourceHandle,
        IntPtr targetProcessHandle,
        out IntPtr targetHandle,
        uint desiredAccess,
        uint handleAttributes,
        uint options);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(
        uint processAccess,
        bool bInheritHandle,
        uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint QueryDosDeviceW(
        string lpDeviceName,
        IntPtr lpTargetPath,
        uint ucchMax);

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_HANDLE_TABLE_ENTRY_INFO
    {
        public ushort UniqueProcessId;
        public ushort CreatorBackTraceIndex;
        public byte ObjectTypeIndex;
        public byte HandleAttributes;
        public ushort HandleValue;
        public IntPtr Object;
        public uint GrantedAccess;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_HANDLE_INFORMATION
    {
        public uint NumberOfHandles;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public SYSTEM_HANDLE_TABLE_ENTRY_INFO[] Handles;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UNICODE_STRING
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct OBJECT_NAME_INFORMATION
    {
        public UNICODE_STRING Name;
    }

    public static string? GetObjectName(IntPtr handle, IntPtr processHandle)
    {
        IntPtr dupHandle;
        var status = NtDuplicateObject(
            processHandle,
            handle,
            GetCurrentProcess(),
            out dupHandle,
            0, 0, DuplicateSameAccess);

        if (status != 0 || dupHandle == IntPtr.Zero)
            return null;

        try
        {
            uint size = 512;
            while (true)
            {
                IntPtr ptr = Marshal.AllocHGlobal((int)size);
                try
                {
                    uint returnLength;
                    status = NtQueryObject(dupHandle, ObjectNameInformation, ptr, size, out returnLength);
                    if (status == STATUS_SUCCESS)
                    {
                        var nameInfo = Marshal.PtrToStructure<OBJECT_NAME_INFORMATION>(ptr);
                        if (nameInfo.Name.Buffer != IntPtr.Zero && nameInfo.Name.Length > 0)
                        {
                            return Marshal.PtrToStringUni(nameInfo.Name.Buffer, nameInfo.Name.Length / 2);
                        }
                        return null;
                    }
                    else if (status == STATUS_INFO_LENGTH_MISMATCH || status == STATUS_BUFFER_OVERFLOW)
                    {
                        size *= 2;
                        if (size > 65536) return null;
                    }
                    else
                    {
                        return null;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
        }
        finally
        {
            CloseHandle(dupHandle);
        }
    }

    public static List<SYSTEM_HANDLE_TABLE_ENTRY_INFO> EnumerateHandles()
    {
        var handles = new List<SYSTEM_HANDLE_TABLE_ENTRY_INFO>();
        uint bufferSize = 0x400000;
        IntPtr buffer = IntPtr.Zero;

        while (true)
        {
            buffer = Marshal.AllocHGlobal((int)bufferSize);
            uint returnLength;
            uint status = NtQuerySystemInformation(SystemHandleInformation, buffer, bufferSize, out returnLength);

            if (status == STATUS_SUCCESS)
            {
                int count = Marshal.ReadInt32(buffer);
                int entrySize = Marshal.SizeOf<SYSTEM_HANDLE_TABLE_ENTRY_INFO>();
                IntPtr entryPtr = IntPtr.Add(buffer, 4);

                for (int i = 0; i < count; i++)
                {
                    var entry = Marshal.PtrToStructure<SYSTEM_HANDLE_TABLE_ENTRY_INFO>(entryPtr + i * entrySize);
                    handles.Add(entry);
                }

                Marshal.FreeHGlobal(buffer);
                break;
            }
            else if (status == STATUS_INFO_LENGTH_MISMATCH)
            {
                Marshal.FreeHGlobal(buffer);
                bufferSize *= 2;
                if (bufferSize > 0x4000000) break;
            }
            else
            {
                Marshal.FreeHGlobal(buffer);
                break;
            }
        }

        return handles;
    }

    public static string ResolveNtPath(string ntPath)
    {
        if (string.IsNullOrEmpty(ntPath))
            return ntPath;

        const string devicePrefix = "\\Device\\";
        if (!ntPath.StartsWith(devicePrefix, StringComparison.OrdinalIgnoreCase))
        {
            if (ntPath.StartsWith("\\??\\"))
                return ntPath[4..];
            return ntPath;
        }

        string remaining = ntPath[devicePrefix.Length..];
        int slashIndex = remaining.IndexOf('\\');
        string deviceName = slashIndex >= 0 ? remaining[..slashIndex] : remaining;
        string suffix = slashIndex >= 0 ? remaining[slashIndex..] : "";

        var driveLetters = GetDosDeviceMapping(deviceName);
        if (driveLetters.Count > 0)
            return driveLetters[0] + suffix;

        return ntPath;
    }

    private static List<string> GetDosDeviceMapping(string ntDeviceName)
    {
        var result = new List<string>();
        uint size = 256;
        IntPtr buffer = Marshal.AllocHGlobal((int)size);

        try
        {
            for (char drive = 'A'; drive <= 'Z'; drive++)
            {
                string driveStr = drive + ":";
                uint ret = QueryDosDeviceW(driveStr, buffer, size);
                if (ret > 0)
                {
                    string target = Marshal.PtrToStringUni(buffer, (int)ret) ?? "";
                    if (target.Equals(ntDeviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(driveStr);
                    }
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return result;
    }
}
