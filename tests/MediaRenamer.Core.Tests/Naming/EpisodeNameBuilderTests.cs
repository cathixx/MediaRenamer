using MediaRenamer.Core.Episodes;
using MediaRenamer.Core.Naming;

namespace MediaRenamer.Core.Tests.Naming;

public class EpisodeNameBuilderTests
{
    [Theory]
    [InlineData(1, 2, "Pilot", "S01E02 - Pilot")]
    [InlineData(12, 123, "Pilot", "S12E123 - Pilot")]
    [InlineData(1, 2, "Teil 1: Der Anfang", "S01E02 - Teil 1 Der Anfang")]
    [InlineData(1, 2, "Wait... what?", "S01E02 - Wait… what")]
    [InlineData(1, 2, "Say \"Hi\" <now> | later", "S01E02 - Say 'Hi' _now_ _ later")]
    public void BuildsNameWithTitle(int season, int episode, string title, string expected)
    {
        Assert.Equal(expected, EpisodeNameBuilder.Build(season, new EpisodeNumbers(episode), title));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("?")]
    public void WithoutTitle_OnlyEpisodeCode(string? title)
    {
        Assert.Equal("S01E01", EpisodeNameBuilder.Build(1, new EpisodeNumbers(1), title));
    }

    [Fact]
    public void DoubleEpisode()
    {
        Assert.Equal("S01E02E03 - Pilot", EpisodeNameBuilder.Build(1, new EpisodeNumbers(2, 3), "Pilot"));
        Assert.Equal("S01E02E03", EpisodeNameBuilder.Build(1, new EpisodeNumbers(2, 3), null));
    }

    [Theory]
    [InlineData("...und dann?", "S01E02 - und dann")]
    [InlineData("Ende.", "S01E02 - Ende")]
    [InlineData("Dr. House & Co.", "S01E02 - Dr. House & Co")]
    [InlineData("...", "S01E02 - …")]   // fewer than two characters would remain: unchanged
    [InlineData("..a..", "S01E02 - ..a..")]
    [InlineData(".", "S01E02 - .")]
    public void RemovesDotsAtStartAndEndOfTitle(string title, string expected)
    {
        Assert.Equal(expected, EpisodeNameBuilder.Build(1, new EpisodeNumbers(2), title));
    }

    [Fact]
    public void EpisodeCode()
    {
        Assert.Equal("S01E02", EpisodeNameBuilder.EpisodeCode(1, new EpisodeNumbers(2)));
        Assert.Equal("S10E09E10", EpisodeNameBuilder.EpisodeCode(10, new EpisodeNumbers(9, 10)));
    }
}
