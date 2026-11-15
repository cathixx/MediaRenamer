using MediaRenamer.Core.Episodes;

namespace MediaRenamer.Core.Tests.Episodes;

public class EpisodeNumberParserTests
{
    [Theory]
    // 1. SssEee, S and E case-insensitive
    [InlineData("Show S01E02 Title.mkv", 1, 2)]
    [InlineData("show.s1e02.mkv", 1, 2)]
    [InlineData("S12E05.mkv", 12, 5)]
    // 2. SssXee
    [InlineData("Show S01x02.mkv", 1, 2)]
    [InlineData("Show s01X02.mkv", 1, 2)]
    // 3. ssXee
    [InlineData("Show 01x02.mkv", 1, 2)]
    [InlineData("Show 1x02.mkv", 1, 2)]
    // 4. Sss, any character, Eee
    [InlineData("Show S01.E02.mkv", 1, 2)]
    [InlineData("Show S01_E02.mkv", 1, 2)]
    [InlineData("Show S01 E02.mkv", 1, 2)]
    // 5. "- ssee -"
    [InlineData("Show - 0102 - Title.mkv", 1, 2)]
    // 6. Epee
    [InlineData("Show Ep02.mkv", 1, 2)]
    [InlineData("Show EP02.mkv", 1, 2)]
    // 7. -Eee-
    [InlineData("show-E02-x.mkv", 1, 2)]
    // 8. ss-ee
    [InlineData("Show 01-02.mkv", 1, 2)]
    public void RecognizesEpisodeNumber(string fileName, int season, int expectedEpisode)
    {
        Assert.Equal(new EpisodeNumbers(expectedEpisode), EpisodeNumberParser.Parse(fileName, season));
    }

    [Theory]
    [InlineData("Folge 02.mkv")]
    [InlineData("E02.mkv")]
    [InlineData("02.mkv")]
    [InlineData("Show 0102.mkv")]
    [InlineData("Show 102.mkv")]
    [InlineData("Show S01E1.mkv")]   // one-digit episode numbers are not recognized
    [InlineData("Show S02E03.mkv")]  // season in the name differs from the folder
    [InlineData("Show S01 -E02.mkv")] // two characters between season and episode
    public void DoesNotRecognize(string fileName)
    {
        Assert.Null(EpisodeNumberParser.Parse(fileName, 1));
    }

    [Fact]
    public void ThreeDigitEpisodeNumber_UsesFirstTwoDigits()
    {
        Assert.Equal(new EpisodeNumbers(12), EpisodeNumberParser.Parse("S01E123.mkv", 1));
    }

    [Fact]
    public void FirstPatternWins()
    {
        // pattern 3 (01x05) occurs first in the name, but pattern 1 (S01E07) has priority
        Assert.Equal(new EpisodeNumbers(7), EpisodeNumberParser.Parse("01x05 S01E07.mkv", 1));
    }

    [Fact]
    public void TwoDigitSeasonHasPriority()
    {
        // "S01E05" (two-digit season) is tried before "S1E06" (one-digit season) of the same pattern
        Assert.Equal(new EpisodeNumbers(5), EpisodeNumberParser.Parse("S1E06 S01E05.mkv", 1));
    }

    [Fact]
    public void RemovesX264()
    {
        Assert.Equal(new EpisodeNumbers(2), EpisodeNumberParser.Parse("Show.S01-x264-E02.mkv", 1));
    }

    [Theory]
    [InlineData("S01E02E03 - Title.mkv", 2, 3)]
    [InlineData("show.s01e02e03.mkv", 2, 3)]
    [InlineData("Show S1E09E10.mkv", 9, 10)]
    public void RecognizesDoubleEpisode(string fileName, int first, int second)
    {
        Assert.Equal(new EpisodeNumbers(first, second), EpisodeNumberParser.Parse(fileName, 1));
    }

    [Theory]
    [InlineData("S01E02-E03.mkv")]
    [InlineData("S01E02-03.mkv")]
    public void OtherDoubleEpisodeNotations_AreSingleEpisodes(string fileName)
    {
        Assert.Equal(new EpisodeNumbers(2), EpisodeNumberParser.Parse(fileName, 1));
    }

    [Fact]
    public void AllContainsBothEpisodesOfDoubleEpisode()
    {
        Assert.Equal([2, 3], new EpisodeNumbers(2, 3).All);
        Assert.Equal([2], new EpisodeNumbers(2).All);
    }
}
