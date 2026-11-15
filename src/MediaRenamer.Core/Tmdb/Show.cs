using System.Globalization;

namespace MediaRenamer.Core.Tmdb;

/// <summary>A show found on TMDB, merged from the search results of all languages (T-5, T-7).</summary>
/// <param name="Id">TMDB ID.</param>
/// <param name="Names">Name per search language in which the show was found (in the order of the search languages),
/// without a year at the end (T-6).</param>
/// <param name="Year">Year of the first air date, if known.</param>
/// <param name="PosterPath">Path of the poster image, e.g. <c>/abc.jpg</c>, if known.</param>
/// <param name="Popularity">TMDB popularity.</param>
public sealed record Show(
    int Id,
    IReadOnlyDictionary<string, string> Names,
    int? Year,
    string? PosterPath,
    double Popularity)
{
    /// <summary>
    /// Name in the given language, otherwise in the first search language in which the show was found, followed by
    /// <c>(year)</c> if known (T-10).
    /// </summary>
    public string DisplayName(string language)
    {
        var name = Names.TryGetValue(language, out var inLanguage) ? inLanguage : Names.Values.First();
        return Year is { } year ? string.Create(CultureInfo.InvariantCulture, $"{name} ({year})") : name;
    }
}

/// <summary>Episode titles of a show in one language (T-12, T-13).</summary>
public sealed class EpisodeTitles(IReadOnlyDictionary<(int Season, int Episode), string?> titles)
{
    public static readonly EpisodeTitles Empty = new(new Dictionary<(int, int), string?>());

    /// <summary>Number of known episodes.</summary>
    public int Count => titles.Count;

    /// <summary>
    /// Returns whether TMDB knows the episode. Its title may still be missing (<c>null</c>), then the new file name
    /// consists of the episode code only (N-2).
    /// </summary>
    public bool TryGetTitle(int season, int episode, out string? title) =>
        titles.TryGetValue((season, episode), out title);
}
