namespace MediaRenamer.Core.Tmdb;

/// <summary>Determines the TMDB API key (T-21).</summary>
public static class TmdbApiKey
{
    /// <summary>Environment variable with the API key; also used at build time to embed the key.</summary>
    public const string EnvironmentVariable = "TMDB_API_KEY";

    /// <summary>
    /// Returns the key from the environment variable at runtime, otherwise the key embedded at build time, otherwise
    /// <c>null</c>.
    /// </summary>
    public static string? Resolve(string? environmentValue, string? embeddedValue) =>
        !string.IsNullOrWhiteSpace(environmentValue) ? environmentValue.Trim()
        : !string.IsNullOrWhiteSpace(embeddedValue) ? embeddedValue.Trim()
        : null;
}
