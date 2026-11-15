using MediaRenamer.Core.Tmdb;
using MediaRenamer.Core.Updates;
using MediaRenamer.ViewModels;

namespace MediaRenamer.Tests;

/// <summary>In-memory TMDB. Requests can be held back with <see cref="Gate"/> and made to fail.</summary>
internal sealed class FakeTmdbClient : ITmdbClient
{
    public Dictionary<string, List<SearchTvResult>> Searches { get; } = new();
    public Dictionary<(int ShowId, string Language), List<TvEpisode>> Episodes { get; } = new();

    /// <summary>Languages of the shows; by default the languages of <see cref="Episodes"/>.</summary>
    public Dictionary<int, List<TvTranslation>> Translations { get; } = new();
    public bool Fail { get; set; }

    /// <summary>Data returned for every poster.</summary>
    public byte[] PosterData { get; set; } = [1, 2, 3];

    private int _searchRequests;

    /// <summary>Number of search requests (one per language and search).</summary>
    public int SearchRequests => _searchRequests;

    /// <summary>If set, search requests wait for it (to control the order of concurrent requests).</summary>
    public Func<string, Task>? Gate { get; set; }

    public async Task<IReadOnlyList<SearchTvResult>> SearchShowsAsync(string query, string language,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _searchRequests);
        if (Gate is not null)
        {
            await Gate(query);
        }

        ThrowIfFailing();
        // the shows are found in English only
        return language == "en-US" ? Searches.GetValueOrDefault(query) ?? [] : [];
    }

    public Task<int> GetNumberOfSeasonsAsync(int showId, string language, CancellationToken cancellationToken = default)
    {
        ThrowIfFailing();
        var episodes = Episodes.GetValueOrDefault((showId, language)) ?? [];
        return Task.FromResult(episodes.Count == 0 ? 0 : episodes.Max(e => e.SeasonNumber));
    }

    public Task<IReadOnlyList<TvEpisode>> GetSeasonEpisodesAsync(int showId, int season, string language,
        CancellationToken cancellationToken = default)
    {
        ThrowIfFailing();
        return Task.FromResult<IReadOnlyList<TvEpisode>>(
            (Episodes.GetValueOrDefault((showId, language)) ?? []).Where(e => e.SeasonNumber == season).ToList());
    }

    public Task<IReadOnlyList<TvTranslation>> GetTranslationsAsync(int showId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfFailing();
        var translations = Translations.GetValueOrDefault(showId)
                           ?? Episodes.Keys.Where(k => k.ShowId == showId)
                               .Select(k => new TvTranslation(k.Language[..2], k.Language[3..], k.Language))
                               .ToList();
        return Task.FromResult<IReadOnlyList<TvTranslation>>(translations);
    }

    public Task<byte[]> GetPosterAsync(string posterPath, CancellationToken cancellationToken = default)
    {
        ThrowIfFailing();
        return Task.FromResult(PosterData);
    }

    private void ThrowIfFailing()
    {
        if (Fail)
        {
            throw new TmdbException("offline");
        }
    }
}

internal sealed class FakeDialogs : IDialogService
{
    public List<(string Header, string Text)> Errors { get; } = [];

    public List<string> Questions { get; } = [];

    /// <summary>Answer to all questions.</summary>
    public bool Confirm { get; set; }

    public Task<bool> ConfirmAsync(string title, string header, string text, string yes, string no)
    {
        Questions.Add(text);
        return Task.FromResult(Confirm);
    }

    public Task ShowErrorAsync(string header, string text)
    {
        Errors.Add((header, text));
        return Task.CompletedTask;
    }
}

internal sealed class FakeUpdates : IUpdateService
{
    public AvailableUpdate? Update { get; set; }
    public bool FailCheck { get; set; }
    public bool FailInstall { get; set; }

    /// <summary>Thrown by <see cref="DownloadAsync"/> if set (an unexpected error).</summary>
    public Exception? InstallException { get; set; }
    public List<(AvailableUpdate Update, string ExecutablePath)> Installed { get; } = [];
    public int Checks { get; private set; }

    public Task<AvailableUpdate?> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        Checks++;
        return FailCheck
            ? Task.FromException<AvailableUpdate?>(new UpdateException("offline"))
            : Task.FromResult(Update is not null && Update.Version > currentVersion ? Update : null);
    }

    public Task<string> DownloadAsync(AvailableUpdate update, string executablePath,
        CancellationToken cancellationToken = default)
    {
        if (InstallException is not null)
        {
            return Task.FromException<string>(InstallException);
        }

        if (FailInstall)
        {
            return Task.FromException<string>(new UpdateException("no permission"));
        }

        Installed.Add((update, executablePath));
        return Task.FromResult(executablePath + ".new");
    }
}
