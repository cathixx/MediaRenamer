using System.Globalization;
using System.Text.RegularExpressions;

namespace MediaRenamer.Core.Tmdb;

/// <summary>
/// Show search and episode titles from TMDB, with an in-memory cache (section 4 of the requirements).
/// All operations may throw <see cref="TmdbException"/>; failed requests are not cached.
/// </summary>
/// <param name="client">TMDB access.</param>
/// <param name="searchLanguages">Languages of the show search, see <see cref="TmdbLanguages.SearchLanguages"/>.</param>
public sealed partial class TmdbService(ITmdbClient client, IReadOnlyList<string> searchLanguages)
{
    private readonly AsyncCache<string, IReadOnlyList<Show>> _searches = new();
    private readonly AsyncCache<int, IReadOnlyList<TmdbLanguage>> _languages = new();
    private readonly AsyncCache<(int ShowId, string Language), EpisodeTitles> _episodes = new();
    private readonly AsyncCache<string, byte[]> _posters = new();

    [GeneratedRegex("[A-Za-z0-9]")]
    private static partial Regex LatinLetterOrDigit();

    [GeneratedRegex(@"\s*\([0-9]{4}\)$")]
    private static partial Regex YearAtEnd();

    /// <summary>
    /// Searches shows in all search languages and returns them merged and sorted (T-2 to T-8). An empty search term returns
    /// no results.
    /// </summary>
    public Task<IReadOnlyList<Show>> SearchAsync(string searchTerm)
    {
        var query = searchTerm.Trim();
        return query.Length == 0
            ? Task.FromResult<IReadOnlyList<Show>>([])
            : _searches.GetAsync(query, SearchUncachedAsync);
    }

    /// <summary>
    /// Returns the languages in which TMDB has a translation of the show (T-24), in the order of TMDB, without
    /// duplicates.
    /// </summary>
    public Task<IReadOnlyList<TmdbLanguage>> GetLanguagesAsync(int showId) =>
        _languages.GetAsync(showId, LoadLanguagesAsync);

    /// <summary>Loads the episode titles of all seasons of a show except the specials (T-11, T-12).</summary>
    public Task<EpisodeTitles> GetEpisodeTitlesAsync(int showId, string language) =>
        _episodes.GetAsync((showId, language), key => LoadEpisodeTitlesAsync(key.ShowId, key.Language));

    /// <summary>Loads the poster image (T-16), or returns <c>null</c> if the show has no poster.</summary>
    public async Task<byte[]?> GetPosterAsync(string? posterPath) =>
        posterPath is null ? null : await _posters.GetAsync(posterPath, p => client.GetPosterAsync(p)).ConfigureAwait(false);

    private async Task<IReadOnlyList<Show>> SearchUncachedAsync(string query)
    {
        var languages = searchLanguages;
        var results = await Task.WhenAll(languages.Select(l => client.SearchShowsAsync(query, l))).ConfigureAwait(false);

        var shows = new Dictionary<int, ShowBuilder>();
        for (var i = 0; i < languages.Count; i++)
        {
            foreach (var result in results[i])
            {
                if (result.Name is not { } name || !LatinLetterOrDigit().IsMatch(name))
                {
                    continue; // T-4
                }

                if (!shows.TryGetValue(result.Id, out var show))
                {
                    show = new ShowBuilder(result.Id, result.Popularity);
                    shows.Add(result.Id, show);
                }

                show.Names.TryAdd(languages[i], RemoveYear(name));
                show.Year ??= ParseYear(result.FirstAirDate);
                show.PosterPath ??= string.IsNullOrEmpty(result.PosterPath) ? null : result.PosterPath;
            }
        }

        return shows.Values
            .Select(s => new Show(s.Id, s.Names, s.Year, s.PosterPath, s.Popularity))
            .OrderByDescending(s => s.Names.Values.Any(n => string.Equals(n, query, StringComparison.OrdinalIgnoreCase)))
            .ThenByDescending(s => s.Popularity)
            .ThenByDescending(s => s.Names.Count)
            .ThenByDescending(s => s.Year ?? int.MaxValue) // shows without year first
            .ThenBy(s => s.Id)
            .ToList();
    }

    private async Task<IReadOnlyList<TmdbLanguage>> LoadLanguagesAsync(int showId)
    {
        var translations = await client.GetTranslationsAsync(showId).ConfigureAwait(false);
        return translations
            .Where(t => !string.IsNullOrWhiteSpace(t.Language))
            .Select(t => new TmdbLanguage(
                string.IsNullOrWhiteSpace(t.Region) ? t.Language! : $"{t.Language}-{t.Region}",
                string.IsNullOrWhiteSpace(t.EnglishName) ? t.Language! : t.EnglishName))
            .DistinctBy(l => l.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<EpisodeTitles> LoadEpisodeTitlesAsync(int showId, string language)
    {
        var numberOfSeasons = await client.GetNumberOfSeasonsAsync(showId, language).ConfigureAwait(false);
        var seasons = await Task.WhenAll(Enumerable.Range(1, Math.Max(numberOfSeasons, 0))
            .Select(season => client.GetSeasonEpisodesAsync(showId, season, language))).ConfigureAwait(false);

        var episodes = new Dictionary<(int, int), EpisodeDetails>();
        foreach (var episode in seasons.SelectMany(s => s))
        {
            episodes.TryAdd((episode.SeasonNumber, episode.EpisodeNumber), new EpisodeDetails(
                episode.SeasonNumber,
                episode.EpisodeNumber,
                NullIfBlank(episode.Name),
                DateOnly.TryParseExact(episode.AirDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var airDate) ? airDate : null,
                episode.Runtime is > 0 ? episode.Runtime : null,
                episode.VoteCount > 0 ? episode.VoteAverage : null,
                NullIfBlank(episode.Overview)));
        }

        return new EpisodeTitles(episodes);
    }

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    /// <summary>Removes a year in parentheses at the end: <c>Doctor Who (2005)</c> → <c>Doctor Who</c> (T-6).</summary>
    internal static string RemoveYear(string name)
    {
        var withoutYear = YearAtEnd().Replace(name, string.Empty);
        return withoutYear.Length > 0 ? withoutYear : name;
    }

    private static int? ParseYear(string? date) =>
        date is { Length: >= 4 } && int.TryParse(date.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            ? year
            : null;

    private sealed class ShowBuilder(int id, double popularity)
    {
        public int Id { get; } = id;
        public double Popularity { get; } = popularity;
        public Dictionary<string, string> Names { get; } = new();
        public int? Year { get; set; }
        public string? PosterPath { get; set; }
    }
}
