using MediaRenamer.Core.Renaming;

namespace MediaRenamer.Core.Tests.Renaming;

public sealed class FileRenamerTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Renames()
    {
        var source = _temp.File("show.s01e02.mkv");

        var result = FileRenamer.Rename(new RenameRequest(source, "S01E02 - Pilot.mkv"));

        Assert.Equal(RenameOutcome.Renamed, result.Outcome);
        Assert.Equal(Path.Combine(_temp.Path, "S01E02 - Pilot.mkv"), result.NewPath);
        Assert.Equal(["S01E02 - Pilot.mkv"], _temp.Entries());
        Assert.Equal(source, File.ReadAllText(result.NewPath!));
    }

    [Fact]
    public void NeverOverwritesExistingFile()
    {
        var source = _temp.File("a.mkv");
        var existing = _temp.File("S01E02.mkv");

        var result = FileRenamer.Rename(new RenameRequest(source, "S01E02.mkv"));

        Assert.Equal(RenameOutcome.TargetExists, result.Outcome);
        Assert.Equal(["S01E02.mkv", "a.mkv"], _temp.Entries());
        Assert.Equal(existing, File.ReadAllText(existing));
    }

    [Fact]
    public void NeverOverwritesExistingFolder()
    {
        var source = _temp.File("a.mkv");
        _temp.Folder("S01E02.mkv");

        var result = FileRenamer.Rename(new RenameRequest(source, "S01E02.mkv"));

        Assert.Equal(RenameOutcome.TargetExists, result.Outcome);
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void ChangesOnlyTheCase()
    {
        var source = _temp.File("s01e02 - pilot.mkv");

        var result = FileRenamer.Rename(new RenameRequest(source, "S01E02 - Pilot.mkv"));

        Assert.Equal(RenameOutcome.Renamed, result.Outcome);
        Assert.Equal(["S01E02 - Pilot.mkv"], _temp.Entries());
    }

    [Fact]
    public void ChangesOnlyTheCase_OtherFileWithExactNameExists()
    {
        // only possible on case-sensitive file systems (Linux)
        var source = _temp.File("s01e02.mkv");
        var other = Path.Combine(_temp.Path, "S01E02.mkv");
        if (File.Exists(other))
        {
            Assert.Skip("case-insensitive file system");
        }

        _temp.File("S01E02.mkv");

        var result = FileRenamer.Rename(new RenameRequest(source, "S01E02.mkv"));

        Assert.Equal(RenameOutcome.TargetExists, result.Outcome);
        Assert.Equal(["S01E02.mkv", "s01e02.mkv"], _temp.Entries());
        Assert.Equal(other, File.ReadAllText(other));
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("sub/S01E02.mkv")]
    [InlineData("sub\\S01E02.mkv")]
    [InlineData("../S01E02.mkv")]
    public void RejectsNamesWithFolders(string newFileName)
    {
        var source = _temp.File("Show", "a.mkv");

        var result = FileRenamer.Rename(new RenameRequest(source, newFileName));

        Assert.Equal(RenameOutcome.Failed, result.Outcome);
        Assert.NotNull(result.Error);
        Assert.Equal(["a.mkv"], _temp.Entries("Show"));
    }

    [Fact]
    public void MissingSourceFails()
    {
        var result = FileRenamer.Rename(new RenameRequest(Path.Combine(_temp.Path, "missing.mkv"), "S01E02.mkv"));

        Assert.Equal(RenameOutcome.Failed, result.Outcome);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void RenameAll_ContinuesAfterFailure()
    {
        var a = _temp.File("a.mkv");
        var b = _temp.File("b.mkv");
        _temp.File("S01E01.mkv");

        var results = FileRenamer.RenameAll(
        [
            new RenameRequest(a, "S01E01.mkv"),
            new RenameRequest(b, "S01E02.mkv"),
        ]);

        Assert.Equal([RenameOutcome.TargetExists, RenameOutcome.Renamed], results.Select(r => r.Outcome));
        Assert.Equal(["S01E01.mkv", "S01E02.mkv", "a.mkv"], _temp.Entries());
    }
}
