using System.Globalization;
using Avalonia;

namespace MediaRenamer;

internal static class Program
{
    private const string LanguageOption = "--lang=";

    /// <summary>Command line arguments without options, i.e. the folder paths (e.g. from the "Send to" menu).</summary>
    public static IReadOnlyList<string> FolderArguments { get; private set; } = [];

    [STAThread]
    public static void Main(string[] args)
    {
        // UI language: "--lang=<culture>" (e.g. --lang=de) overrides the operating system language.
        // Without it, .NET uses the UI language of the operating system; untranslated languages fall back to English.
        var languageArgument = args.FirstOrDefault(a => a.StartsWith(LanguageOption, StringComparison.OrdinalIgnoreCase));
        if (languageArgument is not null)
        {
            var culture = CultureInfo.GetCultureInfo(languageArgument[LanguageOption.Length..]);
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        FolderArguments = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
