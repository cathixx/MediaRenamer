using MediaRenamer.Core.Episodes;
using MediaRenamer.Core.Folders;

namespace MediaRenamer.Core.Tests.Episodes;

public sealed class EpisodeScannerTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private SeasonFolder Season(int number, params string[] files)
    {
        var folder = _temp.Folder($"Staffel {number:00}");
        foreach (var file in files)
        {
            _temp.File($"Staffel {number:00}", file);
        }

        return new SeasonFolder(folder, number);
    }

    [Fact]
    public void ListsOnlyVideoFiles()
    {
        var season = Season(1, "a.avi", "b.MKV", "c.mpg", "d.Mp4", "e.mov", "f.srt", "g.nfo", "h.jpg", "mkv");
        _temp.Folder("Staffel 01", "sub.mkv");
        _temp.File("Staffel 01", "Extras", "S01E09.mkv"); // not recursive

        var files = EpisodeScanner.Scan([season]);

        Assert.Equal(["a", "b", "c", "d", "e"], files.Select(f => f.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void KeepsNameAndExtension()
    {
        var season = Season(1, "Show.S01E02.Title.MKV");

        var file = Assert.Single(EpisodeScanner.Scan([season]));

        Assert.Equal(Path.Combine(season.Path, "Show.S01E02.Title.MKV"), file.Path);
        Assert.Equal("Show.S01E02.Title", file.Name);
        Assert.Equal(".MKV", file.Extension);
        Assert.Equal(1, file.Season);
        Assert.Equal(new EpisodeNumbers(2), file.Episodes);
        Assert.True(file.IsRecognized);
    }

    [Fact]
    public void UnrecognizedFilesFillTheGapsInNameOrder()
    {
        // ordinal order: upper case before lower case
        var season = Season(1, "S01E02.mkv", "S01E04.mkv", "b.mkv", "B.mkv", "a.mkv");

        var files = EpisodeScanner.Scan([season]);

        Assert.Equal(
            [("B", 1, false), ("S01E02", 2, true), ("a", 3, false), ("S01E04", 4, true), ("b", 5, false)],
            files.Select(f => (f.Name, f.Episodes.First, f.IsRecognized)));
    }

    [Fact]
    public void DoubleEpisodeTakesBothNumbers()
    {
        var season = Season(1, "S01E01E02.mkv", "x.mkv", "S01E04.mkv");

        var files = EpisodeScanner.Scan([season]);

        Assert.Equal(
            [("S01E01E02", new EpisodeNumbers(1, 2)), ("x", new EpisodeNumbers(3)), ("S01E04", new EpisodeNumbers(4))],
            files.Select(f => (f.Name, f.Episodes)));
    }

    [Fact]
    public void DuplicateNumbersAreKept()
    {
        var season = Season(1, "S01E02 - b.mkv", "S01E02 - a.mkv", "x.mkv");

        var files = EpisodeScanner.Scan([season]);

        Assert.Equal(
            [("x", 1), ("S01E02 - a", 2), ("S01E02 - b", 2)],
            files.Select(f => (f.Name, f.Episodes.First)));
    }

    [Fact]
    public void SortsBySeasonAndEpisode_NumbersRowsInDisplayOrder_MarksSeasonStarts()
    {
        var season2 = Season(2, "S02E02.mkv", "S02E01.mkv");
        var season1 = Season(1, "z.mkv", "S01E03E04.mkv", "S01E01.mkv");

        var files = EpisodeScanner.Scan([season2, season1]);

        Assert.Equal(
            [
                ("S01E01", 1, false),
                ("z", 2, false),
                ("S01E03E04", 3, false),
                ("S02E01", 4, true),
                ("S02E02", 5, false),
            ],
            files.Select(f => (f.Name, f.Number, f.StartsNewSeason)));
    }

    [Fact]
    public void SeasonWithOnlyUnrecognizedFiles_MarksOnlyFirstRow()
    {
        var season1 = Season(1, "S01E01.mkv");
        var season2 = Season(2, "b.mkv", "a.mkv");

        var files = EpisodeScanner.Scan([season1, season2]);

        Assert.Equal([false, true, false], files.Select(f => f.StartsNewSeason));
    }

    [Fact]
    public void EmptySeason()
    {
        Assert.Empty(EpisodeScanner.Scan([Season(1)]));
    }
}
