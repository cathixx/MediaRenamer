using MediaRenamer.Core.Folders;

namespace MediaRenamer.Core.Tests.Folders;

public sealed class ShowFolderResolverTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void NoPath()
    {
        var result = ShowFolderResolver.Resolve([]);

        Assert.Equal(ShowFolderProblem.NoPath, result.Problem);
        Assert.Empty(result.Seasons);
    }

    [Fact]
    public void ShowFolderMode_FindsSeasonFoldersSortedByNumber()
    {
        var show = _temp.Folder("Eureka");
        _temp.Folder("Eureka", "Staffel 10");
        _temp.Folder("Eureka", "Staffel 2");
        _temp.Folder("Eureka", "Season 1");
        _temp.Folder("Eureka", "Extras");
        _temp.Folder("Eureka", "Staffel01");
        _temp.File("Eureka", "Staffel 3"); // a file, not a folder

        var result = ShowFolderResolver.Resolve([show]);

        Assert.Equal(ShowFolderProblem.None, result.Problem);
        Assert.Equal(show, result.ShowPath);
        Assert.Equal([1, 2, 10], result.Seasons.Select(s => s.Number));
        Assert.Equal(Path.Combine(show, "Season 1"), result.Seasons[0].Path);
        Assert.Empty(result.Ignored);
    }

    [Fact]
    public void ShowFolderMode_IgnoresFurtherPaths()
    {
        var show = _temp.Folder("Eureka");
        _temp.Folder("Eureka", "Staffel 1");
        var other = _temp.Folder("Other", "Staffel 2");

        var result = ShowFolderResolver.Resolve([show, other]);

        Assert.Equal([1], result.Seasons.Select(s => s.Number));
        Assert.Empty(result.Ignored);
    }

    [Theory]
    [InlineData("Staffel 00")]
    [InlineData("Staffel 0")]
    public void ShowFolderMode_SkipsSpecialsFolder(string specials)
    {
        var show = _temp.Folder("Eureka");
        _temp.Folder("Eureka", specials);
        _temp.Folder("Eureka", "Staffel 01");

        var result = ShowFolderResolver.Resolve([show]);

        Assert.Equal([1], result.Seasons.Select(s => s.Number));
    }

    [Fact]
    public void ShowFolderMode_OnlySpecialsFolder_NoSeasonFolders()
    {
        var show = _temp.Folder("Eureka");
        _temp.Folder("Eureka", "Staffel 00");

        var result = ShowFolderResolver.Resolve([show]);

        Assert.Equal(ShowFolderProblem.NoSeasonFolders, result.Problem);
        Assert.Equal(show, result.ShowPath);
        Assert.Empty(result.Seasons);
    }

    [Fact]
    public void ShowFolderMode_TrailingSeparator()
    {
        var show = _temp.Folder("Eureka");
        _temp.Folder("Eureka", "Staffel 1");

        var result = ShowFolderResolver.Resolve([show + Path.DirectorySeparatorChar]);

        Assert.Equal(show, result.ShowPath);
        Assert.Single(result.Seasons);
    }

    [Fact]
    public void SeasonFolderInEnglish_IsTreatedAsShowFolder()
    {
        // only the keyword "Staffel" selects the season folder mode
        var season = _temp.Folder("Eureka", "Season 1");

        var result = ShowFolderResolver.Resolve([season]);

        Assert.Equal(ShowFolderProblem.NoSeasonFolders, result.Problem);
        Assert.Equal(season, result.ShowPath);
    }

    [Fact]
    public void SeasonFolderMode_ShowFolderIsParentOfFirstPath()
    {
        var show = _temp.Folder("Eureka");
        var season3 = _temp.Folder("Eureka", "Staffel 03");
        var season1 = _temp.Folder("Eureka", "Staffel 01");
        _temp.Folder("Eureka", "Staffel 02"); // not selected

        var result = ShowFolderResolver.Resolve([season3, season1]);

        Assert.Equal(ShowFolderProblem.None, result.Problem);
        Assert.Equal(show, result.ShowPath);
        Assert.Equal([season1, season3], result.Seasons.Select(s => s.Path));
        Assert.Empty(result.Ignored);
    }

    [Fact]
    public void SeasonFolderMode_IgnoresAndReportsInvalidPaths()
    {
        var season1 = _temp.Folder("Eureka", "Staffel 01");
        var extras = _temp.Folder("Eureka", "Extras");
        var specials = _temp.Folder("Eureka", "Staffel 00");
        var file = _temp.File("Eureka", "notes.txt");
        var missing = Path.Combine(_temp.Path, "Eureka", "Staffel 05");
        var otherShow = _temp.Folder("Other", "Staffel 02");

        var result = ShowFolderResolver.Resolve([season1, extras, specials, file, missing, otherShow, season1]);

        Assert.Equal([season1], result.Seasons.Select(s => s.Path));
        Assert.Equal(
            [
                new IgnoredPath(extras, IgnoreReason.NotASeasonFolder),
                new IgnoredPath(specials, IgnoreReason.SpecialsFolder),
                new IgnoredPath(file, IgnoreReason.NotAFolder),
                new IgnoredPath(missing, IgnoreReason.NotAFolder),
                new IgnoredPath(otherShow, IgnoreReason.DifferentShowFolder),
            ],
            result.Ignored);
    }

    [Fact]
    public void SeasonFolderMode_FirstPathNotASeasonFolder_NoSeasonFolders()
    {
        var show = _temp.Folder("Eureka");
        var invalid = _temp.Folder("Eureka", "Staffel01");

        var result = ShowFolderResolver.Resolve([invalid]);

        Assert.Equal(ShowFolderProblem.NoSeasonFolders, result.Problem);
        Assert.Equal(show, result.ShowPath);
        Assert.Equal([new IgnoredPath(invalid, IgnoreReason.NotASeasonFolder)], result.Ignored);
    }

    [Fact]
    public void FileInsteadOfFolder()
    {
        var file = _temp.File("Eureka", "S01E01.mkv");

        var result = ShowFolderResolver.Resolve([file]);

        Assert.Equal(ShowFolderProblem.NotAFolder, result.Problem);
        Assert.Equal(file, result.InvalidPath);
        Assert.Null(result.ShowPath);
    }

    [Fact]
    public void MissingFolder()
    {
        var missing = Path.Combine(_temp.Path, "missing");

        var result = ShowFolderResolver.Resolve([missing]);

        Assert.Equal(ShowFolderProblem.NotAFolder, result.Problem);
        Assert.Equal(missing, result.InvalidPath);
    }
}
