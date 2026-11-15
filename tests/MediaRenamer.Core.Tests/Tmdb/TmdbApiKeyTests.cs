using MediaRenamer.Core.Tmdb;

namespace MediaRenamer.Core.Tests.Tmdb;

public class TmdbApiKeyTests
{
    [Theory]
    [InlineData("env", "embedded", "env")]
    [InlineData(" env ", null, "env")]
    [InlineData(null, "embedded", "embedded")]
    [InlineData("", "embedded", "embedded")]
    [InlineData("  ", "embedded", "embedded")]
    [InlineData(null, null, null)]
    [InlineData("", "", null)]
    public void EnvironmentVariableHasPriority(string? environment, string? embedded, string? expected)
    {
        Assert.Equal(expected, TmdbApiKey.Resolve(environment, embedded));
    }
}
