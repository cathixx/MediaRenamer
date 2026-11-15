using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System.Diagnostics;
using MediaRenamer.Core.Tmdb;
using MediaRenamer.Core.Updates;
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
                var httpClient = new HttpClient();
                var window = new MainWindow();
                var viewModel = new MainWindowViewModel(
                    new TmdbService(new TmdbClient(httpClient, apiKey), TmdbLanguages.SearchLanguages(language)),
                    new DialogService(window),
                    language,
                    UpdateService.ForCurrentPlatform(httpClient));
                window.DataContext = viewModel;
                HandleUnexpectedErrors(viewModel);
                window.Opened += async (_, _) =>
                {
                    await viewModel.OpenAsync(Program.FolderArguments);
                    if (Program.StartupError is { } startupError)
                    {
                        await new DialogService(window).ShowErrorAsync(Strings.UpdateFailedHeader, startupError);
                    }

                    var executable = Environment.ProcessPath;
                    if (await viewModel.CheckForUpdateAsync(AppInfo.ReleaseVersion, executable) is { } newExecutable)
                    {
                        try
                        {
                            // the new version replaces this one as soon as this process has exited (UP-4)
                            Start(newExecutable,
                                UpdateArguments.Create(executable!, Environment.ProcessId, Program.Arguments));
                            desktop.Shutdown();
                        }
                        catch (Exception e)
                        {
                            Console.Error.WriteLine(e);
                            await new DialogService(window).ShowErrorAsync(Strings.UpdateFailedHeader,
                                Strings.UpdateFailed(MainWindowViewModel.ErrorDetails(e), UpdateService.ReleasesUrl));
                        }
                    }
                };
                desktop.MainWindow = window;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Starts an executable with the given arguments in its folder.</summary>
    internal static void Start(string executable, IEnumerable<string> arguments)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executable) ?? Environment.CurrentDirectory,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        Process.Start(start);
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
