using System.Windows;
using WhoLockIt.Services;

namespace WhoLockIt;

public partial class LanguageSelector : Window
{
    private readonly LocalizationService _loc = LocalizationService.Instance;

    public string PromptText => _loc["LanguageSelector_Prompt"];
    public string NoteText => _loc["LanguageSelector_Note"];

    public LanguageSelector()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void OnChineseClick(object sender, RoutedEventArgs e)
    {
        SelectLanguage("zh-CN");
    }

    private void OnEnglishClick(object sender, RoutedEventArgs e)
    {
        SelectLanguage("en-US");
    }

    private void SelectLanguage(string cultureName)
    {
        _loc.CurrentCulture = new System.Globalization.CultureInfo(cultureName);
        DialogResult = true;
        Close();
    }
}
