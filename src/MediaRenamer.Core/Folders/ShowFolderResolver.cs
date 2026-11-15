namespace MediaRenamer.Core.Folders;

/// <summary>A season folder of a show.</summary>
/// <param name="Path">Absolute path of the folder.</param>
/// <param name="Number">Season number taken from the folder name.</param>
public sealed record SeasonFolder(string Path, int Number);

/// <summary>Why a path passed in season folder mode was ignored.</summary>
public enum IgnoreReason
{
    /// <summary>The path does not exist or is not a folder.</summary>
    NotAFolder,

    /// <summary>The folder name does not match the season folder pattern.</summary>
    NotASeasonFolder,

    /// <summary>The folder is the specials folder (season 0), whose files are never renamed.</summary>
    SpecialsFolder,

    /// <summary>The folder is not located in the same show folder as the first one.</summary>
    DifferentShowFolder,
}

/// <summary>A path that was passed to the application but is ignored.</summary>
public sealed record IgnoredPath(string Path, IgnoreReason Reason);

/// <summary>Why no show could be opened.</summary>
public enum ShowFolderProblem
{
    /// <summary>The show folder and at least one season folder were found.</summary>
    None,

    /// <summary>No path was passed.</summary>
    NoPath,

    /// <summary>The (first) path does not exist or is not a folder, see <see cref="ShowFolderResult.InvalidPath"/>.</summary>
    NotAFolder,

    /// <summary>The show folder contains no season folders (apart from the specials folder).</summary>
    NoSeasonFolders,
}

/// <summary>Result of <see cref="ShowFolderResolver.Resolve"/>.</summary>
/// <param name="Problem">Why no show could be opened, or <see cref="ShowFolderProblem.None"/>.</param>
/// <param name="ShowPath">Absolute path of the show folder, if known.</param>
/// <param name="Seasons">The season folders, sorted by season number.</param>
/// <param name="Ignored">Paths passed in season folder mode that are ignored; the user should be told about them.</param>
/// <param name="InvalidPath">The path that is not a folder (<see cref="ShowFolderProblem.NotAFolder"/>).</param>
public sealed record ShowFolderResult(
    ShowFolderProblem Problem,
    string? ShowPath,
    IReadOnlyList<SeasonFolder> Seasons,
    IReadOnlyList<IgnoredPath> Ignored,
    string? InvalidPath = null);

/// <summary>
/// Determines the show folder and its season folders from the paths passed to the application, e.g. via the
/// "Send to" menu of the Windows Explorer, or from a folder chosen in the application.
/// </summary>
/// <remarks>
/// <para><b>Show folder mode:</b> the name of the first path does not start with one of the
/// <see cref="SeasonFolderName.SeasonKeywords"/>. It is the show folder; all its direct sub folders whose names match
/// the season folder pattern are the seasons. Further paths are ignored.</para>
/// <para><b>Season folder mode:</b> the name of the first path starts with one of the
/// <see cref="SeasonFolderName.SeasonKeywords"/>.
/// All paths are season folders and the show folder is the parent of the first one. This allows processing single
/// seasons of a show. Paths that are not season folders of that show are ignored and reported.</para>
/// <para>In both modes the specials folder (season 0) is skipped.</para>
/// </remarks>
public static class ShowFolderResolver
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    public static ShowFolderResult Resolve(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return new ShowFolderResult(ShowFolderProblem.NoPath, null, [], []);
        }

        var first = Normalize(paths[0]);
        if (!Directory.Exists(first))
        {
            return new ShowFolderResult(ShowFolderProblem.NotAFolder, null, [], [], paths[0]);
        }

        var firstName = Path.GetFileName(first);
        var showPath = Path.GetDirectoryName(first);
        return SeasonFolderName.StartsWithSeasonKeyword(firstName) && showPath is not null
            ? ResolveSeasonFolders(showPath, paths)
            : ResolveShowFolder(first);
    }

    private static ShowFolderResult ResolveShowFolder(string showPath)
    {
        var seasons = new List<SeasonFolder>();
        foreach (var directory in Directory.EnumerateDirectories(showPath))
        {
            if (SeasonFolderName.TryParse(Path.GetFileName(directory), out var number)
                && number != SeasonFolderName.SpecialsSeason)
            {
                seasons.Add(new SeasonFolder(directory, number));
            }
        }

        return Result(showPath, seasons, []);
    }

    private static ShowFolderResult ResolveSeasonFolders(string showPath, IReadOnlyList<string> paths)
    {
        var seasons = new List<SeasonFolder>();
        var ignored = new List<IgnoredPath>();
        foreach (var path in paths)
        {
            var normalized = Normalize(path);
            if (seasons.Any(s => string.Equals(s.Path, normalized, PathComparison)))
            {
                continue; // passed twice
            }

            if (!Directory.Exists(normalized))
            {
                ignored.Add(new IgnoredPath(path, IgnoreReason.NotAFolder));
            }
            else if (!string.Equals(Path.GetDirectoryName(normalized), showPath, PathComparison))
            {
                ignored.Add(new IgnoredPath(path, IgnoreReason.DifferentShowFolder));
            }
            else if (!SeasonFolderName.TryParse(Path.GetFileName(normalized), out var number))
            {
                ignored.Add(new IgnoredPath(path, IgnoreReason.NotASeasonFolder));
            }
            else if (number == SeasonFolderName.SpecialsSeason)
            {
                ignored.Add(new IgnoredPath(path, IgnoreReason.SpecialsFolder));
            }
            else
            {
                seasons.Add(new SeasonFolder(normalized, number));
            }
        }

        return Result(showPath, seasons, ignored);
    }

    private static ShowFolderResult Result(string showPath, List<SeasonFolder> seasons, List<IgnoredPath> ignored)
    {
        var sorted = seasons
            .OrderBy(s => s.Number)
            .ThenBy(s => s.Path, StringComparer.Ordinal)
            .ToList();
        var problem = sorted.Count == 0 ? ShowFolderProblem.NoSeasonFolders : ShowFolderProblem.None;
        return new ShowFolderResult(problem, showPath, sorted, ignored);
    }

    /// <summary>Absolute path without a trailing directory separator (except for root folders).</summary>
    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
