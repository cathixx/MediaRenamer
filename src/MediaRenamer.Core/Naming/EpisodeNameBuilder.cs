using System.Globalization;
using MediaRenamer.Core.Episodes;

namespace MediaRenamer.Core.Naming;

/// <summary>Builds the new file name (without extension) of an episode.</summary>
public static class EpisodeNameBuilder
{
    /// <summary>Separator between the episode code and the title.</summary>
    public const string TitleSeparator = " - ";

    /// <summary>
    /// Returns the new file name without extension: <c>S01E02 - Title</c>, for a double episode
    /// <c>S01E02E03 - Title</c> (the title of the first episode). Without a title (missing or empty after cleaning)
    /// the name is only the episode code, e.g. <c>S01E02</c>.
    /// </summary>
    /// <remarks>
    /// Season and episode numbers have at least two digits, larger numbers are kept completely (<c>S12E123</c>).
    /// Three dots become the ellipsis <c>…</c> everywhere, also at the start and end (<c>Wait...</c> → <c>Wait…</c>).
    /// Other dots at the start and end of the title are removed (<c>.and then?</c> → <c>and then</c>), unless fewer than
    /// two characters would remain. Then the title is cleaned with <see cref="TitleSanitizer.Sanitize"/>. Finally dots at
    /// the end are removed in any case, so the name never ends with a dot followed by the extension's dot
    /// (<c>A.?</c> → <c>A</c>).
    /// </remarks>
    public static string Build(int season, EpisodeNumbers episodes, string? title)
    {
        var code = EpisodeCode(season, episodes);
        var cleanTitle = title is null
            ? string.Empty
            : TitleSanitizer.Sanitize(TrimDots(title.Replace("...", "…", StringComparison.Ordinal))).TrimEnd('.', ' ');
        return cleanTitle.Length == 0 ? code : code + TitleSeparator + cleanTitle;
    }

    /// <summary>Returns the episode code, e.g. <c>S01E02</c> or <c>S01E02E03</c>.</summary>
    public static string EpisodeCode(int season, EpisodeNumbers episodes)
    {
        var code = string.Create(CultureInfo.InvariantCulture, $"S{season:00}E{episodes.First:00}");
        return episodes.Second is { } second
            ? code + string.Create(CultureInfo.InvariantCulture, $"E{second:00}")
            : code;
    }

    private static string TrimDots(string title)
    {
        var trimmed = title.Trim('.');
        return trimmed.Length >= 2 ? trimmed : title;
    }
}
