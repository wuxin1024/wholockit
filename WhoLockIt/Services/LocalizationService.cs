using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Resources;

namespace WhoLockIt.Services;

public class LocalizationService : INotifyPropertyChanged
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new();

    private ResourceManager _resourceManager;
    private CultureInfo _currentCulture;

    public event Action? CultureChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationService()
    {
        _resourceManager = new ResourceManager("WhoLockIt.Resources.Strings", typeof(LocalizationService).Assembly);
        string? savedCulture = LoadSavedCulture();
        _currentCulture = new CultureInfo(savedCulture ?? CultureInfo.CurrentUICulture.Name);
    }

    public string this[string key]
    {
        get
        {
            string? value = _resourceManager.GetString(key, _currentCulture);
            return value ?? key;
        }
    }

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (_currentCulture.Name != value.Name)
            {
                _currentCulture = value;
                CultureInfo.CurrentUICulture = value;
                CultureInfo.CurrentCulture = value;
                SaveCulture(value.Name);
                CultureChanged?.Invoke();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            }
        }
    }

    public string CurrentLanguageName => _currentCulture.Name switch
    {
        "zh-CN" => "简体中文",
        _ => "English"
    };

    public string NextLanguageName => _currentCulture.Name == "zh-CN" ? "English" : "简体中文";
    public string NextCultureName => _currentCulture.Name == "zh-CN" ? "en-US" : "zh-CN";

    public void ToggleLanguage()
    {
        CurrentCulture = new CultureInfo(NextCultureName);
    }

    private static string? LoadSavedCulture()
    {
        try
        {
            string settingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WhoLockIt");
            string settingsFile = Path.Combine(settingsDir, "settings.txt");
            if (File.Exists(settingsFile))
                return File.ReadAllText(settingsFile).Trim();
        }
        catch { }
        return null;
    }

    private static void SaveCulture(string cultureName)
    {
        try
        {
            string settingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WhoLockIt");
            Directory.CreateDirectory(settingsDir);
            File.WriteAllText(Path.Combine(settingsDir, "settings.txt"), cultureName);
        }
        catch { }
    }
}
