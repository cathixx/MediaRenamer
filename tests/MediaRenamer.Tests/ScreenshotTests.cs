using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using MediaRenamer.Core.Tests;
using MediaRenamer.Core.Tmdb;
using MediaRenamer.ViewModels;
using MediaRenamer.Views;

namespace MediaRenamer.Tests;

/// <summary>
/// Renders the windows without a display and saves them as PNG, to check the layout without starting the
/// application. Run on request: <c>dotnet test -- --explicit only --filter-class *ScreenshotTests</c>.
/// The images are written to the folder in the environment variable <c>SCREENSHOT_DIR</c> (default: temp folder).
/// </summary>
public sealed class ScreenshotTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact(Explicit = true)]
    public void MainWindow()
    {
        var output = Environment.GetEnvironmentVariable("SCREENSHOT_DIR") ?? Path.GetTempPath();
        Directory.CreateDirectory(output);

        // test data: files with all states of the new name and a conflict
        var show = _temp.Folder("Eureka");
        foreach (var file in new[]
                 {
                     "Staffel 01/S01E01.mkv", "Staffel 01/eureka.s01e02.720p.mkv", "Staffel 01/s01e03 - dr. kimbrough.avi",
                     "Staffel 01/S01E04 - Dr. Kimbrough.avi", "Staffel 01/S01E05E06.mkv", "Staffel 01/unknown.mkv",
                     "Staffel 01/S01E09.mkv", "Staffel 02/S02E01 - Phoenix Rising.mkv", "Staffel 02/S02E02.mkv",
                     "Staffel 02/S02E02 copy.mkv",
                 })
        {
            _temp.File(["Eureka", .. file.Split('/')]);
        }

        var client = new FakeTmdbClient
        {
            PosterData = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "poster.png")),
        };
        client.Searches["Eureka"] =
        [
            new SearchTvResult(1, "Eureka", "2006-07-18", "/eureka.jpg", 50),
            new SearchTvResult(2, "Eureka Seven", "2005-04-17", null, 20),
            new SearchTvResult(3, "Eureka!", "1980-01-01", null, 1),
        ];
        client.Translations[1] =
        [
            new TvTranslation("en", "US", "English"), new TvTranslation("de", "DE", "German"),
            new TvTranslation("fr", "FR", "French"), new TvTranslation("pt", "BR", "Portuguese"),
            new TvTranslation("es", "ES", "Spanish"), new TvTranslation("es", "MX", "Spanish"),
            new TvTranslation("it", "IT", "Italian"), new TvTranslation("nl", "NL", "Dutch"),
            new TvTranslation("pl", "PL", "Polish"), new TvTranslation("sv", "SE", "Swedish"),
            new TvTranslation("cs", "CZ", "Czech"), new TvTranslation("hu", "HU", "Hungarian"),
        ];
        client.Episodes[(1, "en-US")] =
        [
            new TvEpisode(1, 1, "Pilot"), new TvEpisode(1, 2, "Many Happy Returns"),
            new TvEpisode(1, 3, "Before I Forget"), new TvEpisode(1, 4, "Dr. Kimbrough"),
            new TvEpisode(1, 5, "Invincible"), new TvEpisode(1, 6, "Blink"), new TvEpisode(1, 7, "Purple Haze"),
            new TvEpisode(2, 1, "Phoenix Rising"), new TvEpisode(2, 2, "Try, Try Again"),
        ];

        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        AppBuilder.Configure<App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var viewModel = new MainWindowViewModel(new TmdbService(client, ["en-US"]), new FakeDialogs(), "en-US")
        {
            SearchDelay = TimeSpan.Zero,
        };
        // the continuations run on the Avalonia dispatcher of this thread, so it must be pumped while waiting
        Wait(viewModel.OpenAsync([show]));
        Wait(viewModel.PendingShow);

        var window = new MainWindow { DataContext = viewModel, Width = 1400, Height = 700 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()!.Save(Path.Combine(output, "main-window.png"), new PngBitmapEncoderOptions());

        // the language selection opened above the result list; the popup is drawn into the window for the screenshot
        var languages = window.GetVisualDescendants().OfType<ComboBox>().Single();
        foreach (var popup in languages.GetVisualDescendants().OfType<Popup>())
        {
            popup.ShouldUseOverlayLayer = true;
        }

        languages.IsDropDownOpen = true;
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()!.Save(Path.Combine(output, "main-window-languages.png"),
            new PngBitmapEncoderOptions());
        languages.IsDropDownOpen = false;

        // the same in the dark theme
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        languages.IsDropDownOpen = true;
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()!.Save(Path.Combine(output, "main-window-languages-dark.png"),
            new PngBitmapEncoderOptions());
        languages.IsDropDownOpen = false;
        Application.Current.RequestedThemeVariant = ThemeVariant.Light;
        Dispatcher.UIThread.RunJobs();

        var message = new MessageWindow("An error occurred",
            "The following files could not be renamed:\nS01E01.mkv: a file with the new name already exists");
        message.Show();
        Dispatcher.UIThread.RunJobs();
        message.CaptureRenderedFrame()!.Save(Path.Combine(output, "message-window.png"), new PngBitmapEncoderOptions());

        var question = new MessageWindow(Resources.Strings.UpdateAvailableTitle, Resources.Strings.UpdateAvailableHeader,
            Resources.Strings.UpdateAvailable("1.2.0", "1.1.0"), Resources.Strings.UpdateNow,
            Resources.Strings.UpdateLater);
        question.Show();
        Dispatcher.UIThread.RunJobs();
        question.CaptureRenderedFrame()!.Save(Path.Combine(output, "update-window.png"), new PngBitmapEncoderOptions());
    }

    private static void Wait(Task task)
    {
        var timeout = DateTime.UtcNow.AddSeconds(30);
        while (!task.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
            if (DateTime.UtcNow > timeout)
            {
                throw new TimeoutException($"Task did not complete: {task.Status}");
            }
        }

        Dispatcher.UIThread.RunJobs();
        task.GetAwaiter().GetResult();
    }
}
