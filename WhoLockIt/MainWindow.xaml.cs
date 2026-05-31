using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using WhoLockIt.Native;
using WhoLockIt.ViewModels;

namespace WhoLockIt;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();

        _vm = new MainViewModel();
        DataContext = _vm;

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_vm.IsTopmost))
                Topmost = _vm.IsTopmost;
        };
        Topmost = _vm.IsTopmost;

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AllowDragDropFromLowerIntegrity();

        if (PrivilegeHelper.IsRunningAsAdmin())
        {
            PrivilegeHelper.EnableDebugPrivilege();
        }

        string[] args = Environment.GetCommandLineArgs();
        if (args.Length > 1)
        {
            string path = args[1];
            if (!string.IsNullOrWhiteSpace(path) && (File.Exists(path) || Directory.Exists(path)))
            {
                _vm.FilePath = path;
                _ = _vm.ScanAsync();
            }
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _vm.CancelScan();
    }

    private void AllowDragDropFromLowerIntegrity()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        // WM_DROPFILES (0x0233), WM_COPYDATA (0x004A), WM_COPYGLOBALDATA (0x0049)
        // Allow these messages from lower-integrity processes so drag-drop from Explorer works when running as admin
        foreach (uint msg in new uint[] { 0x0233, 0x004A, 0x0049 })
        {
            ChangeWindowMessageFilterEx(hwnd, msg, 1 /* MSGFLT_ALLOW */, IntPtr.Zero);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint msg, uint action, IntPtr changeInfo);

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            _vm.DragHint = WhoLockIt.Services.LocalizationService.Instance["DragDrop_Active"];
            _vm.IsDropActive = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        _vm.IsDropActive = false;
        _vm.DragHint = WhoLockIt.Services.LocalizationService.Instance["DragDrop_Hint"];
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        _vm.IsDropActive = false;
        _vm.DragHint = WhoLockIt.Services.LocalizationService.Instance["DragDrop_Hint"];

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0)
            {
                _vm.FilePath = files[0];
                _ = _vm.ScanAsync();
            }
        }
    }
}
