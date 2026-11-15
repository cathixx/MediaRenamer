using MediaRenamer.Core.Tmdb;

namespace MediaRenamer.Core.Tests.Tmdb;

/// <summary>
/// Tests against the real TMDB API. They need network access and the environment variable <c>TMDB_API_KEY</c>, so they
/// only run on request: <c>dotnet test -- --explicit only</c>.
/// </summary>
public class TmdbIntegrationTests
{
    private const int EurekaId = 4620;

    private static TmdbService CreateService()
    {
        var apiKey = Environment.GetEnvironmentVariable(TmdbApiKey.EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Assert.Skip($"environment variable {TmdbApiKey.EnvironmentVariable} is not set");
        }

        return new TmdbService(new TmdbClient(new HttpClient(), apiKey));
    }

    [Fact(Explicit = true)]
    public async Task SearchFindsShowInBothLanguages()
    {
        var shows = await CreateService().SearchAsync("Eureka");

        var eureka = shows[0];
        Assert.Equal(EurekaId, eureka.Id);
        Assert.Equal(["de", "en"], eureka.Names.Keys.Order());
        Assert.Equal(2006, eureka.Year);
        Assert.NotNull(eureka.PosterPath);
    }

    [Fact(Explicit = true)]
    public async Task EpisodeTitlesAndPoster()
    {
        var service = CreateService();

        var german = await service.GetEpisodeTitlesAsync(EurekaId, "de");
        var english = await service.GetEpisodeTitlesAsync(EurekaId, "en");

        Assert.True(german.Count > 50);
        Assert.True(german.TryGetTitle(1, 1, out var germanTitle));
        Assert.True(english.TryGetTitle(1, 1, out var englishTitle));
        Assert.NotNull(germanTitle);
        Assert.NotEqual(germanTitle, englishTitle);

        var show = (await service.SearchAsync("Eureka"))[0];
        var poster = await service.GetPosterAsync(show.PosterPath);
        Assert.NotNull(poster);
        Assert.True(poster.Length > 1000);
    }

    [Fact(Explicit = true)]
    public async Task InvalidApiKeyFails()
    {
        var service = new TmdbService(new TmdbClient(new HttpClient(), "invalid"));

        var e = await Assert.ThrowsAsync<TmdbException>(() => service.SearchAsync("Eureka"));
        Assert.Contains("401", e.Message, StringComparison.Ordinal);
    }
}
