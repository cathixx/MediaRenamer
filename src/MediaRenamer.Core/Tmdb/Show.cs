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

/// <summary>Information about an episode on TMDB, shown as tool tip of a row (6.2).</summary>
/// <param name="Season">Season number.</param>
/// <param name="Episode">Episode number.</param>
/// <param name="Title">Title, <c>null</c> if TMDB has none in the language.</param>
/// <param name="AirDate">Date of the first broadcast, if known.</param>
/// <param name="Runtime">Runtime in minutes, if known.</param>
/// <param name="Rating">Average rating from 0 to 10, <c>null</c> without votes.</param>
/// <param name="Overview">Summary of the plot, <c>null</c> if TMDB has none in the language.</param>
public sealed record EpisodeDetails(
    int Season,
    int Episode,
    string? Title,
    DateOnly? AirDate,
    int? Runtime,
    double? Rating,
    string? Overview);

/// <summary>Episodes of a show in one language with their titles (T-12, T-13) and further details.</summary>
public sealed class EpisodeTitles(IReadOnlyDictionary<(int Season, int Episode), EpisodeDetails> episodes)
{
    public static readonly EpisodeTitles Empty = new(new Dictionary<(int, int), EpisodeDetails>());

    /// <summary>Number of known episodes.</summary>
    public int Count => episodes.Count;

    /// <summary>
    /// Returns whether TMDB knows the episode. Its title may still be missing (<c>null</c>), then the new file name
    /// consists of the episode code only (N-2).
    /// </summary>
    public bool TryGetTitle(int season, int episode, out string? title)
    {
        var found = episodes.TryGetValue((season, episode), out var details);
        title = details?.Title;
        return found;
    }

    /// <summary>Returns the details of the episode, or <c>null</c> if TMDB does not know it.</summary>
    public EpisodeDetails? GetEpisode(int season, int episode) => episodes.GetValueOrDefault((season, episode));
}
