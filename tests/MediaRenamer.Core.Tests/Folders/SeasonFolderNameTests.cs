using MediaRenamer.Core.Folders;

namespace MediaRenamer.Core.Tests.Folders;

public class SeasonFolderNameTests
{
    [Theory]
    [InlineData("Staffel 01", 1)]
    [InlineData("Staffel 1", 1)]
    [InlineData("Staffel 12", 12)]
    [InlineData("Staffel 00", 0)]
    [InlineData("Season 3 extras", 3)]
    [InlineData("Season 3", 3)]
    public void RecognizesSeasonFolders(string name, int expectedSeason)
    {
        Assert.True(SeasonFolderName.TryParse(name, out var season));
        Assert.Equal(expectedSeason, season);
    }

    [Theory]
    [InlineData("Staffel01")]       // no space
    [InlineData("Staffel  1")]      // two spaces
    [InlineData("Specials")]        // no number
    [InlineData("Zusätze 1")]       // umlaut
    [InlineData("1 Staffel")]
    [InlineData(" Staffel 1")]
    [InlineData("Staffel 99999999999")] // too large
    public void RejectsOtherFolders(string name)
    {
        Assert.False(SeasonFolderName.TryParse(name, out _));
    }

    [Theory]
    [InlineData("Staffel 01", true)]
    [InlineData("Staffel01", true)]
    [InlineData("staffel 01", false)] // case-sensitive
    [InlineData("Season 1", false)]
    [InlineData("Doctor Who", false)]
    public void SeasonKeywordSelectsSeasonFolderMode(string name, bool expected)
    {
        Assert.Equal(expected, SeasonFolderName.StartsWithSeasonKeyword(name));
    }
}
