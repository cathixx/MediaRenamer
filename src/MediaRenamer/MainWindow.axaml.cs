using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MediaRenamer.Core.Tmdb;
using MediaRenamer.Resources;

namespace MediaRenamer;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var folders = Program.FolderArguments;
        var status = folders.Count == 0 ? Strings.NoFoldersPassed : Strings.Arguments(string.Join(", ", folders));
        var apiKey = TmdbApiKey.Resolve(Environment.GetEnvironmentVariable(TmdbApiKey.EnvironmentVariable),
            EmbeddedTmdbApiKey.Value);
        var apiKeyStatus = apiKey is null ? Strings.TmdbApiKeyMissing : Strings.TmdbApiKeyAvailable;
        StatusText.Text =
            $"{status}  |  {Strings.CurrentLanguage(CultureInfo.CurrentUICulture.Name)}  |  {apiKeyStatus}";
    }

    private void OnHelloClick(object? sender, RoutedEventArgs e) =>
        ResultText.Text = Strings.SearchedFor(SearchBox.Text ?? string.Empty);
}
