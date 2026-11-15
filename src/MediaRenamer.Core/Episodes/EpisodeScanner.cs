using MediaRenamer.Core.Folders;

namespace MediaRenamer.Core.Episodes;

/// <summary>A video file of an episode (or of a double episode).</summary>
/// <param name="Path">Absolute path of the file.</param>
/// <param name="Name">File name without the extension.</param>
/// <param name="Extension">Extension including the dot, in its original case, e.g. <c>.mkv</c>.</param>
/// <param name="Season">Season number, always the one of the folder.</param>
/// <param name="Episodes">Episode number(s).</param>
/// <param name="IsRecognized">Whether the episode number was recognized in the file name. If not, the file got the
/// lowest free episode number of its season.</param>
/// <param name="Number">Consecutive number of the file across all seasons in display order, starting at 1.</param>
/// <param name="StartsNewSeason">Whether this is the first file of a season other than the first one; a separator is
/// displayed before it.</param>
public sealed record EpisodeFile(
    string Path,
    string Name,
    string Extension,
    int Season,
    EpisodeNumbers Episodes,
    bool IsRecognized,
    int Number,
    bool StartsNewSeason);

/// <summary>Lists the video files of the season folders and determines their season and episode numbers.</summary>
public static class EpisodeScanner
{
    /// <summary>Extensions of video files (case-insensitive). All other files are ignored and never renamed.</summary>
    public static readonly IReadOnlyList<string> VideoExtensions = [".avi", ".mkv", ".mpg", ".mp4", ".mov"];

    /// <summary>
    /// Returns the video files directly contained in the season folders (not recursive), sorted by season and episode.
    /// </summary>
    /// <remarks>
    /// Files without a recognized episode number get the lowest free episode number of their season folder, in the
    /// order of their file names (ordinal, i.e. upper case before lower case). They fill the gaps between the
    /// recognized episodes. Both numbers of a double episode are taken.
    /// </remarks>
    public static IReadOnlyList<EpisodeFile> Scan(IEnumerable<SeasonFolder> seasons)
    {
        var files = new List<(string Path, int Season, EpisodeNumbers Episodes, bool IsRecognized)>();
        foreach (var season in seasons)
        {
            var videoFiles = Directory.EnumerateFiles(season.Path)
                .Where(IsVideoFile)
                .Order(StringComparer.Ordinal)
                .ToList();

            var taken = new HashSet<int>();
            var unrecognized = new List<string>();
            foreach (var file in videoFiles)
            {
                if (EpisodeNumberParser.Parse(Path.GetFileName(file), season.Number) is { } episodes)
                {
                    taken.UnionWith(episodes.All);
                    files.Add((file, season.Number, episodes, true));
                }
                else
                {
                    unrecognized.Add(file);
                }
            }

            var next = 1;
            foreach (var file in unrecognized)
            {
                while (!taken.Add(next))
                {
                    next++;
                }

                files.Add((file, season.Number, new EpisodeNumbers(next), false));
            }
        }

        // OrderBy is stable: files with the same numbers keep the order of their names
        var sorted = files
            .OrderBy(f => f.Season)
            .ThenBy(f => f.Episodes.First)
            .Select((f, index) => new EpisodeFile(
                f.Path,
                Path.GetFileNameWithoutExtension(f.Path),
                Path.GetExtension(f.Path),
                f.Season,
                f.Episodes,
                f.IsRecognized,
                index + 1,
                StartsNewSeason: false))
            .ToList();

        for (var i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].Season != sorted[i - 1].Season)
            {
                sorted[i] = sorted[i] with { StartsNewSeason = true };
            }
        }

        return sorted;
    }

    private static bool IsVideoFile(string path) =>
        VideoExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
