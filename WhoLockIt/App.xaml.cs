using System.IO;
using System.Windows;
using WhoLockIt.Services;

namespace WhoLockIt;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Prevent shutdown when language selector closes
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Check if first run (no settings saved)
        string settingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WhoLockIt");
        string settingsFile = Path.Combine(settingsDir, "settings.txt");

        if (!File.Exists(settingsFile))
        {
            var langSelector = new LanguageSelector();
            langSelector.ShowDialog();
        }

        // Open main window
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Closed += (_, _) => Shutdown();
        mainWindow.Show();

        // Switch to normal shutdown mode now that main window exists
        ShutdownMode = ShutdownMode.OnMainWindowClose;
    }
}
