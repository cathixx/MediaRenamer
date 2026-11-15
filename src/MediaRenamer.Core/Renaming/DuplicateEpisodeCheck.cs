using MediaRenamer.Core.Episodes;

namespace MediaRenamer.Core.Renaming;

/// <summary>An episode number that occurs in more than one of the selected files.</summary>
/// <param name="Season">Season number.</param>
/// <param name="Episode">The episode number that occurs more than once.</param>
/// <param name="Files">The files containing this episode, in display order.</param>
public sealed record EpisodeConflict(int Season, int Episode, IReadOnlyList<EpisodeFile> Files);

/// <summary>
/// Ensures that every episode number occurs at most once per season among the files selected for renaming.
/// Otherwise two files would get the same name. Renaming is only possible if there are no conflicts; deselecting the
/// affected files until only one per number is left resolves them.
/// </summary>
public static class DuplicateEpisodeCheck
{
    /// <summary>
    /// Returns the conflicts among <paramref name="selectedFiles"/>, sorted by season and episode.
    /// Both episodes of a double episode count.
    /// </summary>
    public static IReadOnlyList<EpisodeConflict> FindConflicts(IEnumerable<EpisodeFile> selectedFiles) =>
        selectedFiles
            .SelectMany(file => file.Episodes.All.Select(episode => (file.Season, Episode: episode, File: file)))
            .GroupBy(e => (e.Season, e.Episode))
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key.Season)
            .ThenBy(group => group.Key.Episode)
            .Select(group => new EpisodeConflict(group.Key.Season, group.Key.Episode,
                group.Select(e => e.File).OrderBy(f => f.Number).ToList()))
            .ToList();
}
