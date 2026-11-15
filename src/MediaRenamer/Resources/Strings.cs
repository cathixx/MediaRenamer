using System.Globalization;
using System.Resources;

[assembly: NeutralResourcesLanguage("en")]

namespace MediaRenamer.Resources;

/// <summary>
/// Typed access to the localized UI texts in <c>Strings.resx</c> (English, neutral) and
/// <c>Strings.&lt;culture&gt;.resx</c> (translations). The language is taken from
/// <see cref="CultureInfo.CurrentUICulture"/> at the time of the lookup.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager = new("MediaRenamer.Resources.Strings", typeof(Strings).Assembly);

    private static string Get(string name) => Manager.GetString(name, CultureInfo.CurrentUICulture) ?? $"!{name}!";

    public static string Greeting => Get(nameof(Greeting));
    public static string SearchWatermark => Get(nameof(SearchWatermark));
    public static string ExampleCheckbox => Get(nameof(ExampleCheckbox));
    public static string ClickMe => Get(nameof(ClickMe));
    public static string NoFoldersPassed => Get(nameof(NoFoldersPassed));

    public static string Arguments(string value) => string.Format(Get("ArgumentsFormat"), value);
    public static string SearchedFor(string value) => string.Format(Get("SearchedForFormat"), value);
    public static string CurrentLanguage(string value) => string.Format(Get("CurrentLanguageFormat"), value);
}
