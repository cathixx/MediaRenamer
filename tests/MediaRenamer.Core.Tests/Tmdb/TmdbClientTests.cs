using System.Net;
using System.Text;
using MediaRenamer.Core.Tmdb;

namespace MediaRenamer.Core.Tests.Tmdb;

public class TmdbClientTests
{
    private const string ApiKey = "secret-key";

    /// <summary>Answers every request with a fixed response and records the requested URIs.</summary>
    private sealed class FakeHandler(HttpStatusCode status, string content) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("No such host is known.");
    }

    private static (TmdbClient Client, FakeHandler Handler) Create(string content,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new FakeHandler(status, content);
        return (new TmdbClient(new HttpClient(handler), ApiKey), handler);
    }

    [Fact]
    public async Task SearchShows()
    {
        var (client, handler) = Create("""
            {"page":1,"results":[
              {"id":4620,"name":"EUReKA - Die geheime Stadt","first_air_date":"2006-07-18",
               "poster_path":"/x.jpg","popularity":257.3,"overview":"..."},
              {"id":11771,"name":"Eureka!","first_air_date":"","poster_path":null,"popularity":1.4}
            ],"total_pages":1}
            """);

        var results = await client.SearchShowsAsync("Eureka & Co", "de", TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new SearchTvResult(4620, "EUReKA - Die geheime Stadt", "2006-07-18", "/x.jpg", 257.3),
                new SearchTvResult(11771, "Eureka!", "", null, 1.4),
            ],
            results);
        var uri = Assert.Single(handler.Requests);
        Assert.Equal("https://api.themoviedb.org/3/search/tv", uri.GetLeftPart(UriPartial.Path));
        Assert.Equal("?query=Eureka%20%26%20Co&language=de&include_adult=false&api_key=secret-key", uri.Query);
    }

    [Fact]
    public async Task GetNumberOfSeasons()
    {
        var (client, handler) = Create("""{"id":4620,"name":"Eureka","number_of_seasons":5}""");

        Assert.Equal(5, await client.GetNumberOfSeasonsAsync(4620, "en", TestContext.Current.CancellationToken));
        Assert.Equal("https://api.themoviedb.org/3/tv/4620?language=en&api_key=secret-key",
            Assert.Single(handler.Requests).ToString());
    }

    [Fact]
    public async Task GetSeasonEpisodes()
    {
        var (client, handler) = Create("""
            {"season_number":2,"episodes":[
              {"season_number":2,"episode_number":1,"name":"Phoenix Rising"},
              {"season_number":2,"episode_number":2,"name":null}
            ]}
            """);

        var episodes = await client.GetSeasonEpisodesAsync(4620, 2, "de", TestContext.Current.CancellationToken);

        Assert.Equal([new TvEpisode(2, 1, "Phoenix Rising"), new TvEpisode(2, 2, null)], episodes);
        Assert.Equal("https://api.themoviedb.org/3/tv/4620/season/2?language=de&api_key=secret-key",
            Assert.Single(handler.Requests).ToString());
    }

    [Fact]
    public async Task GetTranslations()
    {
        var (client, handler) = Create("""
            {"id":4620,"translations":[
              {"iso_3166_1":"US","iso_639_1":"en","name":"English","english_name":"English",
               "data":{"name":"","overview":"..."}},
              {"iso_3166_1":"BR","iso_639_1":"pt","name":"Português","english_name":"Portuguese","data":{}}
            ]}
            """);

        var translations = await client.GetTranslationsAsync(4620, TestContext.Current.CancellationToken);

        Assert.Equal([new TvTranslation("en", "US", "English"), new TvTranslation("pt", "BR", "Portuguese")],
            translations);
        Assert.Equal("https://api.themoviedb.org/3/tv/4620/translations?api_key=secret-key",
            Assert.Single(handler.Requests).ToString());
    }

    [Fact]
    public async Task GetPoster_WithoutApiKey()
    {
        var (client, handler) = Create("image");

        var poster = await client.GetPosterAsync("/x.jpg", TestContext.Current.CancellationToken);

        Assert.Equal("image"u8.ToArray(), poster);
        Assert.Equal("https://image.tmdb.org/t/p/w400/x.jpg", Assert.Single(handler.Requests).ToString());
    }

    [Fact]
    public async Task ErrorStatus_ThrowsWithoutApiKeyInMessage()
    {
        var (client, _) = Create("""{"status_message":"Invalid API key"}""", HttpStatusCode.Unauthorized);

        var e = await Assert.ThrowsAsync<TmdbException>(
            () => client.SearchShowsAsync("Eureka", "de", TestContext.Current.CancellationToken));

        Assert.Contains("401", e.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ApiKey, e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidJson_Throws()
    {
        var (client, _) = Create("<html>");

        await Assert.ThrowsAsync<TmdbException>(
            () => client.GetNumberOfSeasonsAsync(1, "de", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NetworkError_Throws()
    {
        var client = new TmdbClient(new HttpClient(new FailingHandler()), ApiKey);

        var e = await Assert.ThrowsAsync<TmdbException>(
            () => client.SearchShowsAsync("Eureka", "de", TestContext.Current.CancellationToken));
        Assert.IsType<HttpRequestException>(e.InnerException);

        await Assert.ThrowsAsync<TmdbException>(
            () => client.GetPosterAsync("/x.jpg", TestContext.Current.CancellationToken));
    }
}
