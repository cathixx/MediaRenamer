using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;

namespace MediaRenamer.Core.Tmdb;

/// <summary>A request to TMDB failed (network error, error status, invalid response).</summary>
public sealed class TmdbException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>Access to the TMDB API v3 (T-3, T-11, T-12, T-16, T-22, T-24).</summary>
public interface ITmdbClient
{
    /// <summary>Searches shows; only the first result page (up to 20 results) is returned.</summary>
    Task<IReadOnlyList<SearchTvResult>> SearchShowsAsync(string query, string language, CancellationToken cancellationToken = default);

    /// <summary>Returns the number of seasons of a show.</summary>
    Task<int> GetNumberOfSeasonsAsync(int showId, string language, CancellationToken cancellationToken = default);

    /// <summary>Returns the episodes of a season.</summary>
    Task<IReadOnlyList<TvEpisode>> GetSeasonEpisodesAsync(int showId, int season, string language, CancellationToken cancellationToken = default);

    /// <summary>Returns the languages in which TMDB has a translation of a show.</summary>
    Task<IReadOnlyList<TvTranslation>> GetTranslationsAsync(int showId, CancellationToken cancellationToken = default);

    /// <summary>Returns the poster image (JPEG) for a poster path like <c>/abc.jpg</c>.</summary>
    Task<byte[]> GetPosterAsync(string posterPath, CancellationToken cancellationToken = default);
}

/// <summary>HTTP implementation of <see cref="ITmdbClient"/>.</summary>
/// <param name="httpClient">HTTP client; its base address is ignored.</param>
/// <param name="apiKey">TMDB v3 API key, appended to every API request as query parameter <c>api_key</c>.</param>
public sealed class TmdbClient(HttpClient httpClient, string apiKey) : ITmdbClient
{
    public static readonly Uri ApiBase = new("https://api.themoviedb.org/3/");
    public static readonly Uri PosterBase = new("https://image.tmdb.org/t/p/w400/");

    public async Task<IReadOnlyList<SearchTvResult>> SearchShowsAsync(string query, string language,
        CancellationToken cancellationToken = default)
    {
        var response = await GetJsonAsync("search/tv",
            [("query", query), ("language", language), ("include_adult", "false")],
            TmdbJsonContext.Default.SearchTvResponse, cancellationToken).ConfigureAwait(false);
        return response.Results ?? [];
    }

    public async Task<int> GetNumberOfSeasonsAsync(int showId, string language,
        CancellationToken cancellationToken = default)
    {
        var response = await GetJsonAsync(Invariant($"tv/{showId}"), [("language", language)],
            TmdbJsonContext.Default.TvDetailsResponse, cancellationToken).ConfigureAwait(false);
        return response.NumberOfSeasons;
    }

    public async Task<IReadOnlyList<TvEpisode>> GetSeasonEpisodesAsync(int showId, int season, string language,
        CancellationToken cancellationToken = default)
    {
        var response = await GetJsonAsync(Invariant($"tv/{showId}/season/{season}"), [("language", language)],
            TmdbJsonContext.Default.TvSeasonResponse, cancellationToken).ConfigureAwait(false);
        return response.Episodes ?? [];
    }

    public async Task<IReadOnlyList<TvTranslation>> GetTranslationsAsync(int showId,
        CancellationToken cancellationToken = default)
    {
        var response = await GetJsonAsync(Invariant($"tv/{showId}/translations"), [],
            TmdbJsonContext.Default.TvTranslationsResponse, cancellationToken).ConfigureAwait(false);
        return response.Translations ?? [];
    }

    public async Task<byte[]> GetPosterAsync(string posterPath, CancellationToken cancellationToken = default)
    {
        var uri = new Uri(PosterBase, posterPath.TrimStart('/'));
        try
        {
            return await httpClient.GetByteArrayAsync(uri, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is HttpRequestException
                                      || (e is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new TmdbException($"Loading the poster {uri} failed: {e.Message}", e);
        }
    }

    private async Task<T> GetJsonAsync<T>(string path, IEnumerable<(string Name, string Value)> parameters,
        JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        var query = string.Join('&', parameters.Append((Name: "api_key", Value: apiKey))
            .Select(p => $"{Uri.EscapeDataString(p.Name)}={Uri.EscapeDataString(p.Value)}"));
        var uri = new Uri(ApiBase, $"{path}?{query}");
        try
        {
            using var response = await httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // the URI contains the API key, so only the path is part of the message
                throw new TmdbException(
                    Invariant($"TMDB request /3/{path} failed: {(int)response.StatusCode} {response.ReasonPhrase}"));
            }

            return await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken).ConfigureAwait(false)
                   ?? throw new TmdbException($"TMDB request /3/{path} returned no data.");
        }
        // a timeout is reported as cancellation, although the caller did not cancel
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException
                                  || (e is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new TmdbException($"TMDB request /3/{path} failed: {e.Message}", e);
        }
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
