using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MediaRenamer.Core.Tmdb;
using MediaRenamer.Resources;
using MediaRenamer.ViewModels;
using MediaRenamer.Views;

namespace MediaRenamer;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var apiKey = TmdbApiKey.Resolve(Environment.GetEnvironmentVariable(TmdbApiKey.EnvironmentVariable),
                EmbeddedTmdbApiKey.Value);
            if (apiKey is null)
            {
                // T-23: without an API key the application shows an error and exits
                desktop.MainWindow = new MessageWindow(Strings.MissingApiKeyHeader, Strings.MissingApiKeyText)
                {
                    WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen,
                };
            }
            else
            {
                var window = new MainWindow();
                var viewModel = new MainWindowViewModel(
                    new TmdbService(new TmdbClient(new HttpClient(), apiKey)),
                    new DialogService(window));
                window.DataContext = viewModel;
                window.Opened += async (_, _) => await viewModel.OpenAsync(Program.FolderArguments);
                desktop.MainWindow = window;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
