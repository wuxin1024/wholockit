using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using WhoLockIt.Models;
using WhoLockIt.Services;

namespace WhoLockIt.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly HandleScanner _scanner = new();
    private readonly UnlockService _unlockService = new();
    private readonly ContextMenuService _contextMenuService = new();

    private string _filePath = "";
    private string _statusText = "";
    private string _dragHint = "";
    private string _contextMenuButtonText = "";
    private bool _isScanning;
    private bool _hasResults;
    private bool _isDropActive;
    private string _languageToggleText = "";
    private bool _isTopmost = true;
    private CancellationTokenSource? _scanCts;

    public MainViewModel()
    {
        ScanCommand = new RelayCommand(async () => await ScanAsync(), () => !IsScanning && !string.IsNullOrWhiteSpace(FilePath));
        UnlockSelectedCommand = new RelayCommand(async () => await UnlockSelectedAsync(), () => SelectedLock != null && !IsScanning);
        UnlockAllCommand = new RelayCommand(async () => await UnlockAllAsync(), () => HasResults && !IsScanning);
        UnlockAllAndDeleteCommand = new RelayCommand(async () => await UnlockAllAndDeleteAsync(), () => HasResults && !IsScanning);
        CopyResultsCommand = new RelayCommand(CopyResults, () => HasResults);
        ToggleContextMenuCommand = new RelayCommand(ToggleContextMenu);
        ToggleLanguageCommand = new RelayCommand(ToggleLanguage);
        ToggleTopmostCommand = new RelayCommand(ToggleTopmost);

        var loc = LocalizationService.Instance;
        StatusText = loc["Status_Ready"];
        DragHint = loc["DragDrop_Hint"];
        ContextMenuButtonText = GetContextMenuButtonText();
        LanguageToggleText = loc["Language_Toggle"];
        loc.CultureChanged += () =>
        {
            StatusText = loc["Status_Ready"];
            DragHint = loc["DragDrop_Hint"];
            ContextMenuButtonText = GetContextMenuButtonText();
            LanguageToggleText = loc["Language_Toggle"];
            OnPropertyChanged(nameof(TopmostText));
        };
    }

    public string FilePath
    {
        get => _filePath;
        set { _filePath = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public string DragHint
    {
        get => _dragHint;
        set { _dragHint = value; OnPropertyChanged(); }
    }

    public string ContextMenuButtonText
    {
        get => _contextMenuButtonText;
        set { _contextMenuButtonText = value; OnPropertyChanged(); }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set { _isScanning = value; OnPropertyChanged(); }
    }

    public bool HasResults
    {
        get => _hasResults;
        set { _hasResults = value; OnPropertyChanged(); }
    }

    public bool IsDropActive
    {
        get => _isDropActive;
        set { _isDropActive = value; OnPropertyChanged(); }
    }

    public bool IsTopmost
    {
        get => _isTopmost;
        set { _isTopmost = value; OnPropertyChanged(); OnPropertyChanged(nameof(TopmostText)); }
    }

    public string TopmostText =>
        IsTopmost ? LocalizationService.Instance["Topmost_On"] : LocalizationService.Instance["Topmost_Off"];

    public string LanguageToggleText
    {
        get => _languageToggleText;
        set { _languageToggleText = value; OnPropertyChanged(); }
    }

    public ObservableCollection<LockInfo> Locks { get; set; } = [];

    private LockInfo? _selectedLock;
    public LockInfo? SelectedLock
    {
        get => _selectedLock;
        set { _selectedLock = value; OnPropertyChanged(); }
    }

    public ICommand ScanCommand { get; }
    public ICommand UnlockSelectedCommand { get; }
    public ICommand UnlockAllCommand { get; }
    public ICommand UnlockAllAndDeleteCommand { get; }
    public ICommand CopyResultsCommand { get; }
    public ICommand ToggleContextMenuCommand { get; }
    public ICommand ToggleLanguageCommand { get; }
    public ICommand ToggleTopmostCommand { get; }

    public async Task ScanAsync()
    {
        if (string.IsNullOrWhiteSpace(FilePath)) return;

        IsScanning = true;
        StatusText = LocalizationService.Instance["Status_Scanning"];
        Locks.Clear();

        _scanCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var result = await _scanner.ScanAsync(FilePath.Trim(), _scanCts.Token);
            foreach (var l in result.Locks)
                Locks.Add(l);
            HasResults = result.HasResults;

            if (result.HasResults)
                StatusText = string.Format(LocalizationService.Instance["Status_Found"], Locks.Count, result.Elapsed.TotalSeconds);
            else
                StatusText = LocalizationService.Instance["Status_None"];
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationService.Instance["Status_Scanning"] + " (timeout)";
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationService.Instance["Status_Error"], ex.Message);
        }
        finally
        {
            IsScanning = false;
            _scanCts = null;
        }
    }

    private async Task UnlockSelectedAsync()
    {
        if (SelectedLock == null) return;

        var loc = LocalizationService.Instance;
        string message = string.Format(loc["Confirm_UnlockSelected_Message"], SelectedLock.ProcessName, SelectedLock.Pid);
        if (MessageBox.Show(message, loc["Confirm_UnlockSelected_Title"],
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var result = await _unlockService.UnlockSelectedAsync(SelectedLock);
        Locks.Remove(SelectedLock);
        SelectedLock = null;
        HasResults = Locks.Count > 0;
        StatusText = string.Format(loc["Unlock_Success"], result.Succeeded, result.Attempted);
    }

    private async Task UnlockAllAsync()
    {
        if (Locks.Count == 0) return;

        var loc = LocalizationService.Instance;
        string message = string.Format(loc["Confirm_UnlockAll_Message"], Locks.Count);
        if (MessageBox.Show(message, loc["Confirm_UnlockAll_Title"],
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var locks = Locks.ToList();
        var result = await _unlockService.UnlockAllAsync(locks);

        if (result.Succeeded > 0)
        {
            StatusText = string.Format(loc["Unlock_Success"], result.Succeeded, result.Attempted);
            // Re-scan to see remaining locks
            await ScanAsync();
        }
        else if (result.Errors.Count > 0)
        {
            StatusText = result.Errors[0];
        }
    }

    private async Task UnlockAllAndDeleteAsync()
    {
        var loc = LocalizationService.Instance;
        string message = string.Format(loc["Confirm_UnlockAllAndDelete_Message"], Locks.Count);
        if (MessageBox.Show(message, loc["Confirm_UnlockAllAndDelete_Title"],
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var locks = Locks.ToList();
        var result = await _unlockService.UnlockAllAsync(locks);

        if (result.Succeeded > 0)
        {
            var deleted = await _unlockService.DeleteFileOrFolderAsync(FilePath.Trim());
            if (deleted)
            {
                StatusText = loc["Delete_Success"];
                Locks.Clear();
                HasResults = false;
            }
            else
            {
                StatusText = loc["Delete_Failed"];
                // Re-scan to see remaining locks
                await ScanAsync();
            }
        }
        else if (result.Errors.Count > 0)
        {
            StatusText = result.Errors[0];
        }
    }

    private void ToggleTopmost()
    {
        IsTopmost = !IsTopmost;
    }

    private void CopyResults()
    {
        if (Locks.Count == 0) return;
        var text = string.Join(Environment.NewLine,
            Locks.Select(l => $"{l.ProcessName}\tPID:{l.Pid}\t{l.LockType}\t{l.Source}"));
        Clipboard.SetText(text);
        StatusText = "Copied";
    }

    private void ToggleContextMenu()
    {
        string exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
        string menuText = $"WhoLockIt - {LocalizationService.Instance["Scan_Button"]}";
        if (_contextMenuService.IsInstalled())
        {
            _contextMenuService.Uninstall();
            StatusText = LocalizationService.Instance["ContextMenu_Removed"];
        }
        else
        {
            _contextMenuService.Install(exePath, menuText);
            StatusText = LocalizationService.Instance["ContextMenu_Installed"];
        }
        ContextMenuButtonText = GetContextMenuButtonText();
    }

    private void ToggleLanguage()
    {
        LocalizationService.Instance.ToggleLanguage();
        ContextMenuButtonText = GetContextMenuButtonText();
    }

    private string GetContextMenuButtonText()
    {
        return _contextMenuService.IsInstalled()
            ? LocalizationService.Instance["ContextMenu_Remove"]
            : LocalizationService.Instance["ContextMenu_Install"];
    }

    public void CancelScan()
    {
        _scanCts?.Cancel();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => _execute();
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    private class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;
        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
        public void Execute(object? parameter) => _execute((T?)parameter);
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
