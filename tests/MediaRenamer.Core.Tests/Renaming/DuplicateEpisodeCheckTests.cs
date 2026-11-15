using MediaRenamer.Core.Episodes;
using MediaRenamer.Core.Renaming;

namespace MediaRenamer.Core.Tests.Renaming;

public class DuplicateEpisodeCheckTests
{
    private static EpisodeFile File(int number, int season, int episode, int? second = null) =>
        new($"/show/{number}.mkv", $"{number}", ".mkv", season, new EpisodeNumbers(episode, second), true, number,
            false);

    [Fact]
    public void NoConflicts()
    {
        Assert.Empty(DuplicateEpisodeCheck.FindConflicts([File(1, 1, 1), File(2, 1, 2), File(3, 2, 1)]));
    }

    [Fact]
    public void SameEpisodeTwice()
    {
        var a = File(1, 1, 2);
        var b = File(2, 1, 2);

        var conflict = Assert.Single(DuplicateEpisodeCheck.FindConflicts([b, a, File(3, 1, 3)]));

        Assert.Equal(1, conflict.Season);
        Assert.Equal(2, conflict.Episode);
        Assert.Equal([a, b], conflict.Files);
    }

    [Fact]
    public void DoubleEpisodeConflictsWithBothEpisodes()
    {
        var single2 = File(1, 1, 2);
        var double23 = File(2, 1, 2, 3);
        var single3 = File(3, 1, 3);

        var conflicts = DuplicateEpisodeCheck.FindConflicts([single3, double23, single2]);

        Assert.Equal(
            [(1, 2, new[] { single2, double23 }), (1, 3, new[] { double23, single3 })],
            conflicts.Select(c => (c.Season, c.Episode, c.Files.ToArray())));
    }

    [Fact]
    public void ResolvedByDeselecting()
    {
        // only the selected files are passed
        Assert.Empty(DuplicateEpisodeCheck.FindConflicts([File(1, 1, 2)]));
    }
}
