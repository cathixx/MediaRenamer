using System.Globalization;

namespace MediaRenamer.Core.Tmdb;

/// <summary>A language in which TMDB has a translation of a show (T-24).</summary>
/// <param name="Code">TMDB language code: ISO 639-1 language and ISO 3166-1 region, e.g. <c>de-DE</c>, <c>pt-BR</c>.</param>
/// <param name="EnglishName">English name of the language from TMDB, e.g. <c>German</c>.</param>
public sealed record TmdbLanguage(string Code, string EnglishName)
{
    /// <summary>ISO 639-1 language, e.g. <c>de</c>.</summary>
    public string Language => TmdbLanguages.LanguageOf(Code);

    /// <summary>ISO 3166-1 region, e.g. <c>DE</c>, or <c>null</c>.</summary>
    public string? Region => TmdbLanguages.RegionOf(Code);
}

/// <summary>TMDB language codes: default language, search languages and selection of the best match.</summary>
public static class TmdbLanguages
{
    /// <summary>Fallback language.</summary>
    public const string English = "en-US";

    /// <summary>
    /// TMDB language code of a culture, e.g. <c>de-DE</c> for German (Germany), <c>de</c> for German and
    /// <see cref="English"/> for the invariant culture.
    /// </summary>
    public static string FromCulture(CultureInfo culture)
    {
        if (culture.Name.Length == 0)
        {
            return English;
        }

        var language = culture.TwoLetterISOLanguageName;
        if (culture.IsNeutralCulture)
        {
            return language;
        }

        try
        {
            return $"{language}-{new RegionInfo(culture.Name).TwoLetterISORegionName}";
        }
        catch (ArgumentException)
        {
            return language;
        }
    }

    /// <summary>
    /// Languages of the show search (T-3): the preferred language and English, so shows are found by their local and
    /// by their international name.
    /// </summary>
    public static IReadOnlyList<string> SearchLanguages(string preferred) =>
        SameLanguage(preferred, English) ? [preferred] : [preferred, English];

    /// <summary>
    /// Returns the available language that fits the preferred one best (T-25): the same code, the same language in its
    /// main region (e.g. <c>de-DE</c> for <c>de</c>), the same language in any region, then English likewise, then the
    /// first available language. Returns <c>null</c> if no language is available.
    /// </summary>
    public static TmdbLanguage? BestMatch(IReadOnlyList<TmdbLanguage> available, string preferred) =>
        available.Count == 0 ? null : Match(available, preferred) ?? Match(available, English) ?? available[0];

    private static TmdbLanguage? Match(IReadOnlyList<TmdbLanguage> available, string preferred)
    {
        var language = LanguageOf(preferred);
        return available.FirstOrDefault(l => string.Equals(l.Code, preferred, StringComparison.OrdinalIgnoreCase))
               ?? available.FirstOrDefault(l => SameLanguage(l.Code, preferred)
                                                && string.Equals(l.Region, MainRegion(language),
                                                    StringComparison.OrdinalIgnoreCase))
               ?? available.FirstOrDefault(l => SameLanguage(l.Code, preferred));
    }

    internal static string LanguageOf(string code)
    {
        var dash = code.IndexOf('-', StringComparison.Ordinal);
        return dash < 0 ? code : code[..dash];
    }

    internal static string? RegionOf(string code)
    {
        var dash = code.IndexOf('-', StringComparison.Ordinal);
        return dash < 0 || dash == code.Length - 1 ? null : code[(dash + 1)..];
    }

    private static bool SameLanguage(string a, string b) =>
        string.Equals(LanguageOf(a), LanguageOf(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>Region in which a language is mainly used on TMDB, e.g. <c>DE</c> for <c>de</c>, <c>US</c> for <c>en</c>.</summary>
    private static string MainRegion(string language) =>
        language.ToLowerInvariant() switch
        {
            "en" => "US",
            "zh" => "CN",
            "ja" => "JP",
            "ko" => "KR",
            "el" => "GR",
            "he" => "IL",
            "da" => "DK",
            "sv" => "SE",
            "cs" => "CZ",
            "uk" => "UA",
            "fa" => "IR",
            "ka" => "GE",
            "sl" => "SI",
            "hi" => "IN",
            "ar" => "SA",
            "vi" => "VN",
            "nb" or "no" => "NO",
            _ => language.ToUpperInvariant(),
        };
}
