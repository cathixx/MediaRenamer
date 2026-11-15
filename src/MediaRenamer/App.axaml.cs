using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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
                // T-25: episode titles in the language the application was started with, if available
                var language = TmdbLanguages.FromCulture(CultureInfo.CurrentUICulture);
                var window = new MainWindow();
                var viewModel = new MainWindowViewModel(
                    new TmdbService(new TmdbClient(new HttpClient(), apiKey), TmdbLanguages.SearchLanguages(language)),
                    new DialogService(window),
                    language);
                window.DataContext = viewModel;
                HandleUnexpectedErrors(viewModel);
                window.Opened += async (_, _) => await viewModel.OpenAsync(Program.FolderArguments);
                desktop.MainWindow = window;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Fallback for errors that are not handled where they occur (e.g. in event handlers): the error is shown and the
    /// application continues instead of crashing (section 8).
    /// </summary>
    private static void HandleUnexpectedErrors(MainWindowViewModel viewModel)
    {
        var showing = false;
        Dispatcher.UIThread.UnhandledException += async (_, e) =>
        {
            e.Handled = true;
            if (showing)
            {
                Console.Error.WriteLine(e.Exception); // one dialog at a time
                return;
            }

            showing = true;
            try
            {
                await viewModel.ShowUnexpectedErrorAsync(e.Exception);
            }
            finally
            {
                showing = false;
            }
        };

        // errors of background tasks nobody waits for: only log them
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Console.Error.WriteLine(e.Exception);
            e.SetObserved();
        };
    }
}
