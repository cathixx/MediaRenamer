using System.Globalization;
using System.Text.RegularExpressions;

namespace MediaRenamer.Core.Folders;

/// <summary>Recognizes season folders by their name, e.g. <c>Staffel 01</c> or <c>Season 3 extras</c>.</summary>
public static partial class SeasonFolderName
{
    /// <summary>
    /// A name starting with this word marks a season folder when it is passed directly to the application
    /// (season folder mode, see <see cref="ShowFolderResolver"/>). Case-sensitive.
    /// </summary>
    public const string SeasonKeyword = "Staffel";

    /// <summary>Season number of the specials folder. Its files are never shown or renamed.</summary>
    public const int SpecialsSeason = 0;

    // A word of the letters A-Z, exactly one space, a number, then any text. The number is the season number.
    [GeneratedRegex("^[a-zA-Z]+ ([0-9]+)", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    /// <summary>
    /// Returns whether <paramref name="folderName"/> is the name of a season folder and, if so, its season number.
    /// </summary>
    public static bool TryParse(string folderName, out int seasonNumber)
    {
        var match = Pattern().Match(folderName);
        if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture,
                out seasonNumber))
        {
            return true;
        }

        seasonNumber = 0;
        return false;
    }

    /// <summary>Returns whether a folder passed to the application selects the season folder mode.</summary>
    public static bool StartsWithSeasonKeyword(string folderName) =>
        folderName.StartsWith(SeasonKeyword, StringComparison.Ordinal);
}
