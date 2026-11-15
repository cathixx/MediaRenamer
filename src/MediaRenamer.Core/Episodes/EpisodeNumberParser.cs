using System.Globalization;
using System.Text.RegularExpressions;

namespace MediaRenamer.Core.Episodes;

/// <summary>Episode number(s) of a video file. A file can contain two episodes (double episode).</summary>
/// <param name="First">The (first) episode number.</param>
/// <param name="Second">The second episode number of a double episode, otherwise <c>null</c>.</param>
public readonly record struct EpisodeNumbers(int First, int? Second = null)
{
    /// <summary>All episode numbers contained in the file.</summary>
    public IEnumerable<int> All => Second is { } second ? [First, second] : [First];
}

/// <summary>Recognizes the episode number(s) in the name of a video file.</summary>
/// <remarks>
/// The patterns below are tried in order, each first with the two-digit season number (<c>01</c>) and then with the
/// one-digit one (<c>1</c>). The first match anywhere in the file name (including the extension) wins.
/// <c>SS</c> is the season number of the folder, <c>EE</c> exactly two digits; episode numbers are always two digits.
/// <list type="number">
/// <item><c>SssEeeEee</c> double episode, e.g. <c>S01E02E03</c></item>
/// <item><c>SssEee</c>, e.g. <c>S01E02</c>, <c>s1e02</c></item>
/// <item><c>SssXee</c>, e.g. <c>S01x02</c></item>
/// <item><c>ssXee</c>, e.g. <c>01x02</c>, <c>1x02</c></item>
/// <item><c>Sss</c>, any character, <c>Eee</c>, e.g. <c>S01.E02</c>, <c>S01 E02</c></item>
/// <item><c>- ssee -</c>, e.g. <c>Show - 0102 - Title</c></item>
/// <item><c>Epee</c>, e.g. <c>Show Ep02</c></item>
/// <item><c>-Eee-</c>, e.g. <c>show-E02-x</c></item>
/// <item><c>ss-ee</c>, e.g. <c>01-02</c></item>
/// </list>
/// The letters S, E, x and Ep are case-insensitive. The text <c>-x264-</c> is removed from the name beforehand.
/// </remarks>
public static class EpisodeNumberParser
{
    private const string SeasonPlaceholder = "{S}";
    private const string Episode = "([0-9]{2})";

    private static readonly string[] RemovedTexts = ["-x264-"];

    private static readonly string[] Patterns =
    [
        $"[Ss]{SeasonPlaceholder}[Ee]{Episode}[Ee]{Episode}",
        $"[Ss]{SeasonPlaceholder}[Ee]{Episode}",
        $"[Ss]{SeasonPlaceholder}[Xx]{Episode}",
        $"{SeasonPlaceholder}[Xx]{Episode}",
        $"[Ss]{SeasonPlaceholder}.[Ee]{Episode}",
        $"- {SeasonPlaceholder}{Episode} -",
        $"[Ee][Pp]{Episode}",
        $"-[Ee]{Episode}-",
        $"{SeasonPlaceholder}-{Episode}",
    ];

    /// <summary>
    /// Returns the episode number(s) recognized in <paramref name="fileName"/>, or <c>null</c> if no pattern matches.
    /// </summary>
    /// <param name="fileName">File name including the extension, without the folder.</param>
    /// <param name="season">Season number of the folder containing the file.</param>
    public static EpisodeNumbers? Parse(string fileName, int season)
    {
        foreach (var removed in RemovedTexts)
        {
            fileName = fileName.Replace(removed, string.Empty, StringComparison.Ordinal);
        }

        string[] seasonTexts =
        [
            season.ToString("00", CultureInfo.InvariantCulture),
            season.ToString(CultureInfo.InvariantCulture),
        ];

        foreach (var pattern in Patterns)
        {
            foreach (var seasonText in seasonTexts)
            {
                var regex = pattern.Replace(SeasonPlaceholder, seasonText, StringComparison.Ordinal);
                var match = Regex.Match(fileName, regex, RegexOptions.CultureInvariant);
                if (match.Success)
                {
                    return new EpisodeNumbers(Number(match.Groups[1]),
                        match.Groups.Count > 2 ? Number(match.Groups[2]) : null);
                }
            }
        }

        return null;
    }

    private static int Number(Group group) => int.Parse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture);
}
