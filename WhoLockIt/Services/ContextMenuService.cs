using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WhoLockIt.Services;

public class ContextMenuService
{
    // Use HKCU\Software\Classes so no admin is required for install/uninstall
    private const string ClassesRoot = @"Software\Classes";
    private const string FileMenuPath = @"*\shell\WhoLockIt";
    private const string DirectoryMenuPath = @"Directory\shell\WhoLockIt";
    private const string BackgroundMenuPath = @"Directory\Background\shell\WhoLockIt";

    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    public bool IsInstalled()
    {
        try
        {
            using var classes = Registry.CurrentUser.OpenSubKey(ClassesRoot);
            using var key = classes?.OpenSubKey(FileMenuPath);
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    public void Install(string exePath, string menuText)
    {
        using var classes = Registry.CurrentUser.CreateSubKey(ClassesRoot);
        if (classes == null) return;

        InstallMenuEntry(classes, FileMenuPath, exePath, menuText);
        InstallMenuEntry(classes, DirectoryMenuPath, exePath, menuText);
        InstallMenuEntry(classes, BackgroundMenuPath, exePath, menuText);

        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }

    public void Uninstall()
    {
        using var classes = Registry.CurrentUser.OpenSubKey(ClassesRoot, writable: true);
        if (classes == null) return;

        try { classes.DeleteSubKeyTree(FileMenuPath, false); } catch { }
        try { classes.DeleteSubKeyTree(DirectoryMenuPath, false); } catch { }
        try { classes.DeleteSubKeyTree(BackgroundMenuPath, false); } catch { }

        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }

    private static void InstallMenuEntry(RegistryKey classesRoot, string registryPath, string exePath, string menuText)
    {
        try
        {
            using var key = classesRoot.CreateSubKey(registryPath);
            if (key == null) return;

            key.SetValue(null, menuText);
            key.SetValue("Icon", $"\"{exePath}\",0");

            using var cmdKey = key.CreateSubKey("command");
            cmdKey?.SetValue(null, $"\"{exePath}\" \"%1\"");
        }
        catch { }
    }
}
