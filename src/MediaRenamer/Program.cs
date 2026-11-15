using System.Globalization;
using Avalonia;
using Avalonia.Styling;
using MediaRenamer.Core.Updates;

namespace MediaRenamer;

internal static class Program
{
    private const string LanguageOption = "--lang=";
    private const string ThemeOption = "--theme=";

    /// <summary>The command line arguments (without the options of an update).</summary>
    public static IReadOnlyList<string> Arguments { get; private set; } = [];

    /// <summary>Command line arguments without options, i.e. the folder paths (e.g. from the "Send to" menu).</summary>
    public static IReadOnlyList<string> FolderArguments { get; private set; } = [];

    /// <summary>Theme from <c>--theme=light|dark</c>, or <c>null</c> to follow the operating system.</summary>
    public static ThemeVariant? Theme { get; private set; }

    /// <summary>An error while completing an update, shown after the start.</summary>
    public static string? StartupError { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        Arguments = args;
        var executable = Environment.ProcessPath;

        // started by the previous version to complete an update (UP-4): replace the executable, start it, exit
        if (UpdateArguments.TryParse(args, out var target, out var previousProcess, out var originalArguments))
        {
            Arguments = originalArguments;
            if (executable is not null && TryCompleteUpdate(executable, target, previousProcess))
            {
                return 0;
            }
        }
        else if (executable is not null)
        {
            // remove the files left by an update (UP-6)
            _ = UpdateService.CleanUpAsync(executable);
        }

        // UI language: "--lang=<culture>" (e.g. --lang=de) overrides the operating system language.
        // Without it, .NET uses the UI language of the operating system; untranslated languages fall back to English.
        var languageArgument = Arguments.FirstOrDefault(a => a.StartsWith(LanguageOption, StringComparison.OrdinalIgnoreCase));
        if (languageArgument is not null)
        {
            var culture = CultureInfo.GetCultureInfo(languageArgument[LanguageOption.Length..]);
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        Theme = ParseTheme(Arguments);
        FolderArguments = Arguments.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime([.. Arguments]);
    }

    /// <summary>
    /// Copies this (new) executable over the target and starts it. If that fails, this process continues as the
    /// application (it is the new version) and shows the error.
    /// </summary>
    private static bool TryCompleteUpdate(string newExecutable, string target, int? previousProcess)
    {
        try
        {
            UpdateService.CompleteUpdate(newExecutable, target, previousProcess, TimeSpan.FromSeconds(30));
            App.Start(target, Arguments);
            return true;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            StartupError = Resources.Strings.UpdateFailed(e.Message, UpdateService.ReleasesUrl);
            return false;
        }
    }

    /// <summary>
    /// Theme from the option <c>--theme=light</c> or <c>--theme=dark</c> (case-insensitive), e.g. for testing; any other
    /// value or no option: <c>null</c>, i.e. the light or dark mode of the operating system.
    /// </summary>
    internal static ThemeVariant? ParseTheme(IEnumerable<string> arguments)
    {
        var value = arguments.LastOrDefault(a => a.StartsWith(ThemeOption, StringComparison.OrdinalIgnoreCase))?
            [ThemeOption.Length..];
        return value?.ToLowerInvariant() switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => null,
        };
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
