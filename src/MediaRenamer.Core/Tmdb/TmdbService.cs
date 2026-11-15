using System.Globalization;
using System.Text.RegularExpressions;

namespace MediaRenamer.Core.Tmdb;

/// <summary>
/// Show search and episode titles from TMDB, with an in-memory cache (section 4 of the requirements).
/// All operations may throw <see cref="TmdbException"/>; failed requests are not cached.
/// </summary>
public sealed partial class TmdbService(ITmdbClient client)
{
    private readonly AsyncCache<string, IReadOnlyList<Show>> _searches = new();
    private readonly AsyncCache<(int ShowId, string Language), EpisodeTitles> _episodes = new();
    private readonly AsyncCache<string, byte[]> _posters = new();

    [GeneratedRegex("[A-Za-z0-9]")]
    private static partial Regex LatinLetterOrDigit();

    [GeneratedRegex(@"\s*\([0-9]{4}\)$")]
    private static partial Regex YearAtEnd();

    /// <summary>
    /// Searches shows in all languages and returns them merged and sorted (T-2 to T-8). An empty search term returns
    /// no results.
    /// </summary>
    public Task<IReadOnlyList<Show>> SearchAsync(string searchTerm)
    {
        var query = searchTerm.Trim();
        return query.Length == 0
            ? Task.FromResult<IReadOnlyList<Show>>([])
            : _searches.GetAsync(query, SearchUncachedAsync);
    }

    /// <summary>Loads the episode titles of all seasons of a show except the specials (T-11, T-12).</summary>
    public Task<EpisodeTitles> GetEpisodeTitlesAsync(int showId, string language) =>
        _episodes.GetAsync((showId, language), key => LoadEpisodeTitlesAsync(key.ShowId, key.Language));

    /// <summary>Loads the poster image (T-16), or returns <c>null</c> if the show has no poster.</summary>
    public async Task<byte[]?> GetPosterAsync(string? posterPath) =>
        posterPath is null ? null : await _posters.GetAsync(posterPath, p => client.GetPosterAsync(p)).ConfigureAwait(false);

    private async Task<IReadOnlyList<Show>> SearchUncachedAsync(string query)
    {
        var languages = TmdbLanguages.All;
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

    private async Task<EpisodeTitles> LoadEpisodeTitlesAsync(int showId, string language)
    {
        var numberOfSeasons = await client.GetNumberOfSeasonsAsync(showId, language).ConfigureAwait(false);
        var seasons = await Task.WhenAll(Enumerable.Range(1, Math.Max(numberOfSeasons, 0))
            .Select(season => client.GetSeasonEpisodesAsync(showId, season, language))).ConfigureAwait(false);

        var titles = new Dictionary<(int, int), string?>();
        foreach (var episode in seasons.SelectMany(s => s))
        {
            titles.TryAdd((episode.SeasonNumber, episode.EpisodeNumber),
                string.IsNullOrWhiteSpace(episode.Name) ? null : episode.Name);
        }

        return new EpisodeTitles(titles);
    }

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
