using System.Text.Json.Serialization;

namespace MediaRenamer.Core.Tmdb;

// Data transfer objects of the TMDB API v3, only the fields that are used.
// See https://developer.themoviedb.org/reference/search-tv

/// <summary>Response of <c>GET /3/search/tv</c>.</summary>
public sealed record SearchTvResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<SearchTvResult>? Results);

/// <summary>A show in the response of <c>GET /3/search/tv</c>.</summary>
public sealed record SearchTvResult(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("first_air_date")] string? FirstAirDate,
    [property: JsonPropertyName("poster_path")] string? PosterPath,
    [property: JsonPropertyName("popularity")] double Popularity);

/// <summary>Response of <c>GET /3/tv/{id}</c>.</summary>
public sealed record TvDetailsResponse(
    [property: JsonPropertyName("number_of_seasons")] int NumberOfSeasons);

/// <summary>Response of <c>GET /3/tv/{id}/season/{season}</c>.</summary>
public sealed record TvSeasonResponse(
    [property: JsonPropertyName("season_number")] int SeasonNumber,
    [property: JsonPropertyName("episodes")] IReadOnlyList<TvEpisode>? Episodes);

/// <summary>An episode in the response of <c>GET /3/tv/{id}/season/{season}</c>.</summary>
public sealed record TvEpisode(
    [property: JsonPropertyName("season_number")] int SeasonNumber,
    [property: JsonPropertyName("episode_number")] int EpisodeNumber,
    [property: JsonPropertyName("name")] string? Name);

/// <summary>Response of <c>GET /3/tv/{id}/translations</c>.</summary>
public sealed record TvTranslationsResponse(
    [property: JsonPropertyName("translations")] IReadOnlyList<TvTranslation>? Translations);

/// <summary>A language in which TMDB has a translation of a show.</summary>
public sealed record TvTranslation(
    [property: JsonPropertyName("iso_639_1")] string? Language,
    [property: JsonPropertyName("iso_3166_1")] string? Region,
    [property: JsonPropertyName("english_name")] string? EnglishName);

/// <summary>Source generated JSON metadata, so (de)serialization works in the trimmed executable.</summary>
[JsonSerializable(typeof(SearchTvResponse))]
[JsonSerializable(typeof(TvDetailsResponse))]
[JsonSerializable(typeof(TvSeasonResponse))]
[JsonSerializable(typeof(TvTranslationsResponse))]
internal sealed partial class TmdbJsonContext : JsonSerializerContext;
