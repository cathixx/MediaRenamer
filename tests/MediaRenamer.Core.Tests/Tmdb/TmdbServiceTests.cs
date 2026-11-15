using MediaRenamer.Core.Tmdb;

namespace MediaRenamer.Core.Tests.Tmdb;

public class TmdbServiceTests
{
    /// <summary>In-memory TMDB with request counters and switchable failures.</summary>
    private sealed class FakeClient : ITmdbClient
    {
        public Dictionary<(string Query, string Language), List<SearchTvResult>> Searches { get; } = new();
        public Dictionary<int, int> Seasons { get; } = new();
        public Dictionary<(int ShowId, int Season, string Language), List<TvEpisode>> Episodes { get; } = new();
        public Dictionary<int, List<TvTranslation>> Translations { get; } = new();
        public int TranslationRequests { get; private set; }
        public bool Fail { get; set; }
        public int SearchRequests { get; private set; }
        public int SeasonRequests { get; private set; }
        public int PosterRequests { get; private set; }

        public Task<IReadOnlyList<SearchTvResult>> SearchShowsAsync(string query, string language,
            CancellationToken cancellationToken = default)
        {
            SearchRequests++;
            ThrowIfFailing();
            return Task.FromResult<IReadOnlyList<SearchTvResult>>(
                Searches.GetValueOrDefault((query, language)) ?? []);
        }

        public Task<int> GetNumberOfSeasonsAsync(int showId, string language,
            CancellationToken cancellationToken = default)
        {
            ThrowIfFailing();
            return Task.FromResult(Seasons[showId]);
        }

        public Task<IReadOnlyList<TvEpisode>> GetSeasonEpisodesAsync(int showId, int season, string language,
            CancellationToken cancellationToken = default)
        {
            SeasonRequests++;
            ThrowIfFailing();
            return Task.FromResult<IReadOnlyList<TvEpisode>>(
                Episodes.GetValueOrDefault((showId, season, language)) ?? []);
        }

        public Task<IReadOnlyList<TvTranslation>> GetTranslationsAsync(int showId,
            CancellationToken cancellationToken = default)
        {
            TranslationRequests++;
            ThrowIfFailing();
            return Task.FromResult<IReadOnlyList<TvTranslation>>(Translations.GetValueOrDefault(showId) ?? []);
        }

        public Task<byte[]> GetPosterAsync(string posterPath, CancellationToken cancellationToken = default)
        {
            PosterRequests++;
            ThrowIfFailing();
            return Task.FromResult(new byte[] { 1, 2, 3 });
        }

        private void ThrowIfFailing()
        {
            if (Fail)
            {
                throw new TmdbException("offline");
            }
        }
    }

    private static SearchTvResult Result(int id, string name, string? date = null, double popularity = 1,
        string? poster = null) => new(id, name, date, poster, popularity);

    [Fact]
    public async Task Search_MergesLanguagesByIdAndRemovesYear()
    {
        var client = new FakeClient();
        client.Searches[("Doctor Who", "de")] = [Result(57243, "Doctor Who (2005)", "2005-03-26", 50, "/de.jpg")];
        client.Searches[("Doctor Who", "en")] = [Result(57243, "Doctor Who", "2005-03-26", 50, "/en.jpg")];

        var show = Assert.Single(await new TmdbService(client, ["de", "en"]).SearchAsync("  Doctor Who "));

        Assert.Equal(57243, show.Id);
        Assert.Equal(new Dictionary<string, string> { ["de"] = "Doctor Who", ["en"] = "Doctor Who" }, show.Names);
        Assert.Equal(2005, show.Year);
        Assert.Equal("/de.jpg", show.PosterPath);
    }

    [Fact]
    public async Task Search_DiscardsNamesWithoutLatinLettersOrDigits()
    {
        var client = new FakeClient();
        client.Searches[("x", "de")] = [Result(1, "日本語"), Result(2, "Ärger 日本"), Result(3, "24")];

        var shows = await new TmdbService(client, ["de", "en"]).SearchAsync("x");

        Assert.Equal([2, 3], shows.Select(s => s.Id).Order());
    }

    [Fact]
    public async Task Search_Sorting()
    {
        var client = new FakeClient();
        client.Searches[("eureka", "de")] =
        [
            Result(1, "Eureka Seven", "2005-01-01", popularity: 30),
            Result(2, "Eureka Kids", "2010-01-01", popularity: 5),
            Result(3, "Eureka Old", "1980-01-01", popularity: 5),
            Result(4, "Eureka Unknown", null, popularity: 5),
            Result(5, "EUReKA - Die geheime Stadt", "2006-01-01", popularity: 10),
            Result(6, "Eureka Both", "2000-01-01", popularity: 5),
            Result(8, "Eureka Same", "2010-01-01", popularity: 5),
        ];
        client.Searches[("eureka", "en")] =
        [
            Result(5, "Eureka", "2006-01-01", popularity: 10), // exact match in another language
            Result(6, "Eureka Both", "2000-01-01", popularity: 5),
        ];

        var shows = await new TmdbService(client, ["de", "en"]).SearchAsync("eureka");

        // 1. exact match, 2. popularity, 3. number of languages, 4. year descending (unknown first), 5. ID
        Assert.Equal([5, 1, 6, 4, 2, 8, 3], shows.Select(s => s.Id));
    }

    [Fact]
    public async Task Search_EmptyTermReturnsNothingWithoutRequest()
    {
        var client = new FakeClient();

        Assert.Empty(await new TmdbService(client, ["de", "en"]).SearchAsync("   "));
        Assert.Equal(0, client.SearchRequests);
    }

    [Fact]
    public async Task Search_SingleCharacterIsSearched()
    {
        var client = new FakeClient();
        client.Searches[("V", "en")] = [Result(1, "V")];

        Assert.Single(await new TmdbService(client, ["de", "en"]).SearchAsync("V"));
    }

    [Fact]
    public async Task Search_IsCached()
    {
        var client = new FakeClient();
        var service = new TmdbService(client, ["de", "en"]);

        await service.SearchAsync("Eureka");
        await service.SearchAsync(" Eureka ");

        Assert.Equal(2, client.SearchRequests); // one per language
    }

    [Fact]
    public async Task Search_FailureIsNotCached()
    {
        var client = new FakeClient { Fail = true };
        client.Searches[("Eureka", "de")] = [Result(1, "Eureka")];
        var service = new TmdbService(client, ["de", "en"]);

        await Assert.ThrowsAsync<TmdbException>(() => service.SearchAsync("Eureka"));
        client.Fail = false;

        Assert.Single(await service.SearchAsync("Eureka"));
    }

    [Fact]
    public async Task EpisodeTitles_AllSeasonsExceptSpecials()
    {
        var client = new FakeClient();
        client.Seasons[7] = 2;
        client.Episodes[(7, 0, "de")] = [new TvEpisode(0, 1, "Special")];
        client.Episodes[(7, 1, "de")] = [new TvEpisode(1, 1, "Pilot"), new TvEpisode(1, 2, "")];
        client.Episodes[(7, 2, "de")] = [new TvEpisode(2, 1, "Zwei")];

        var titles = await new TmdbService(client, ["de", "en"]).GetEpisodeTitlesAsync(7, "de");

        Assert.Equal(3, titles.Count);
        Assert.True(titles.TryGetTitle(1, 1, out var pilot));
        Assert.Equal("Pilot", pilot);
        Assert.True(titles.TryGetTitle(1, 2, out var empty)); // known episode without title
        Assert.Null(empty);
        Assert.True(titles.TryGetTitle(2, 1, out _));
        Assert.False(titles.TryGetTitle(0, 1, out _));
        Assert.False(titles.TryGetTitle(1, 3, out _));
        Assert.Equal(2, client.SeasonRequests);
    }

    [Fact]
    public async Task EpisodeDetails()
    {
        var client = new FakeClient();
        client.Seasons[7] = 1;
        client.Episodes[(7, 1, "de")] =
        [
            new TvEpisode(1, 1, "Pilot", "2006-07-18", "A town of geniuses.", 44, 7.84, 12),
            new TvEpisode(1, 2, "", "", " ", 0, 0, 0),
            new TvEpisode(1, 3, null, "invalid"),
        ];

        var titles = await new TmdbService(client, ["de"]).GetEpisodeTitlesAsync(7, "de");

        Assert.Equal(new EpisodeDetails(1, 1, "Pilot", new DateOnly(2006, 7, 18), 44, 7.84, "A town of geniuses."),
            titles.GetEpisode(1, 1));
        Assert.Equal(new EpisodeDetails(1, 2, null, null, null, null, null), titles.GetEpisode(1, 2));
        Assert.Equal(new EpisodeDetails(1, 3, null, null, null, null, null), titles.GetEpisode(1, 3));
        Assert.Null(titles.GetEpisode(1, 4));
    }

    [Fact]
    public async Task EpisodeTitles_CachedPerShowAndLanguage_FailureNotCached()
    {
        var client = new FakeClient { Fail = true };
        client.Seasons[7] = 1;
        client.Episodes[(7, 1, "en")] = [new TvEpisode(1, 1, "Pilot")];
        var service = new TmdbService(client, ["de", "en"]);

        await Assert.ThrowsAsync<TmdbException>(() => service.GetEpisodeTitlesAsync(7, "en"));
        client.Fail = false;
        var titles = await service.GetEpisodeTitlesAsync(7, "en");
        await service.GetEpisodeTitlesAsync(7, "en");
        await service.GetEpisodeTitlesAsync(7, "de");

        Assert.Equal(1, titles.Count);
        Assert.Equal(2, client.SeasonRequests); // en and de; the failed request stopped before loading seasons
    }

    [Fact]
    public async Task Poster_CachedAndNullWithoutPath()
    {
        var client = new FakeClient();
        var service = new TmdbService(client, ["de", "en"]);

        Assert.Null(await service.GetPosterAsync(null));
        Assert.Equal([1, 2, 3], await service.GetPosterAsync("/x.jpg"));
        Assert.Equal([1, 2, 3], await service.GetPosterAsync("/x.jpg"));
        Assert.Equal(1, client.PosterRequests);
    }

    [Fact]
    public async Task Search_UsesTheGivenSearchLanguages()
    {
        var client = new FakeClient();
        client.Searches[("Lupin", "fr-FR")] = [Result(1, "Lupin")];

        var show = Assert.Single(await new TmdbService(client, ["fr-FR"]).SearchAsync("Lupin"));

        Assert.Equal(["fr-FR"], show.Names.Keys);
        Assert.Equal(1, client.SearchRequests);
    }

    [Fact]
    public async Task Languages_FromTheTranslationsWithoutDuplicates()
    {
        var client = new FakeClient();
        client.Translations[7] =
        [
            new TvTranslation("en", "US", "English"),
            new TvTranslation("pt", "BR", "Portuguese"),
            new TvTranslation("de", "DE", "German"),
            new TvTranslation("de", "DE", "German"),
            new TvTranslation("xx", "", null),
            new TvTranslation(null, "XX", "Invalid"),
        ];

        var languages = await new TmdbService(client, ["de", "en"]).GetLanguagesAsync(7);

        Assert.Equal(
            [
                new TmdbLanguage("en-US", "English"),
                new TmdbLanguage("pt-BR", "Portuguese"),
                new TmdbLanguage("de-DE", "German"),
                new TmdbLanguage("xx", "xx"),
            ],
            languages);
    }

    [Fact]
    public async Task Languages_CachedAndFailureNotCached()
    {
        var client = new FakeClient { Fail = true };
        client.Translations[7] = [new TvTranslation("en", "US", "English")];
        var service = new TmdbService(client, ["en"]);

        await Assert.ThrowsAsync<TmdbException>(() => service.GetLanguagesAsync(7));
        client.Fail = false;
        Assert.Single(await service.GetLanguagesAsync(7));
        Assert.Single(await service.GetLanguagesAsync(7));

        Assert.Equal(2, client.TranslationRequests);
    }

    [Theory]
    [InlineData("Doctor Who (2005)", "Doctor Who")]
    [InlineData("Doctor Who", "Doctor Who")]
    [InlineData("1984 (2023)", "1984")]
    [InlineData("(2005)", "(2005)")]
    [InlineData("Show (2005) Reboot", "Show (2005) Reboot")]
    public void RemoveYear(string name, string expected)
    {
        Assert.Equal(expected, TmdbService.RemoveYear(name));
    }

    [Fact]
    public void DisplayName()
    {
        var show = new Show(1, new Dictionary<string, string> { ["en"] = "Eureka" }, 2006, null, 1);

        Assert.Equal("Eureka (2006)", show.DisplayName("en"));
        Assert.Equal("Eureka (2006)", show.DisplayName("de")); // fallback to the first search language
        Assert.Equal("Eureka", (show with { Year = null }).DisplayName("de"));
    }
}
