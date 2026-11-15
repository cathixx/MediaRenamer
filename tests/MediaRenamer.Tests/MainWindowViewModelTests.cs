using MediaRenamer.Core.Tests;
using MediaRenamer.Core.Tmdb;
using MediaRenamer.ViewModels;

namespace MediaRenamer.Tests;

public sealed class MainWindowViewModelTests : IDisposable
{
    private const int EurekaId = 1;

    private readonly TempFolder _temp = new();
    private readonly FakeTmdbClient _client = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly string _show;

    public MainWindowViewModelTests()
    {
        _show = _temp.Folder("Eureka");
        _client.Searches["Eureka"] = [new SearchTvResult(EurekaId, "Eureka", "2006-07-18", "/eureka.jpg", 50)];
        _client.Searches["Other"] = [new SearchTvResult(2, "Other Show", null, null, 1)];
        _client.Episodes[(EurekaId, "de-DE")] =
        [
            new TvEpisode(1, 1, "Pilot DE"),
            new TvEpisode(1, 2, "Glückliche Wiederkehr"),
            new TvEpisode(2, 1, "Phönix"),
        ];
        _client.Episodes[(EurekaId, "en-US")] =
        [
            new TvEpisode(1, 1, "Pilot"),
            new TvEpisode(1, 2, "Many Happy Returns"),
            new TvEpisode(2, 1, "Phoenix Rising"),
        ];
    }

    public void Dispose() => _temp.Dispose();

    private MainWindowViewModel CreateViewModel() =>
        new(new TmdbService(_client, ["de-DE", "en-US"]), _dialogs, "de-DE") { SearchDelay = TimeSpan.Zero };

    private async Task<MainWindowViewModel> OpenAsync(params string[] files)
    {
        foreach (var file in files)
        {
            _temp.File(["Eureka", .. file.Split('/')]);
        }

        var viewModel = CreateViewModel();
        await viewModel.OpenAsync([_show]);
        await viewModel.PendingShow;
        return viewModel;
    }

    private static IEnumerable<(string OldName, string? NewName, NewNameState State)> Rows(MainWindowViewModel vm) =>
        vm.Episodes.Select(row => (row.OldName, row.NewName, row.State));

    [Fact]
    public async Task Open_ListsFilesSearchesShowAndProposesNames()
    {
        var vm = await OpenAsync("Staffel 01/S01E01.mkv", "Staffel 01/x.mkv", "Staffel 01/S01E03 - Old.mkv",
            "Staffel 02/S02E01 - Phönix.mkv");

        Assert.Equal(_show, vm.ShowPath);
        Assert.Null(vm.FolderMessage);
        Assert.Equal("Eureka", vm.SearchTerm);
        Assert.Equal(EurekaId, vm.SelectedShow?.Show.Id);
        Assert.Equal("Eureka (2006)", vm.SelectedShow?.DisplayName);
        Assert.NotNull(vm.Poster);
        Assert.Null(vm.TmdbError);
        Assert.False(vm.IsBusy);
        Assert.Equal(
            [
                ("S01E01", "S01E01 - Pilot DE", NewNameState.Changed),
                ("x", "S01E02 - Glückliche Wiederkehr", NewNameState.Changed),
                ("S01E03 - Old", null, NewNameState.NotFound),
                ("S02E01 - Phönix", "S02E01 - Phönix", NewNameState.Same),
            ],
            Rows(vm));
        Assert.Equal([true, true, false, false], vm.Episodes.Select(r => r.IsCheckboxVisible));
        Assert.Equal(["001", "002", "003", "004"], vm.Episodes.Select(r => r.NumberText));
        Assert.Equal([false, false, false, true], vm.Episodes.Select(r => r.StartsNewSeason));
        Assert.True(vm.RenameCommand.CanExecute(null));
    }

    [Fact]
    public async Task Open_NoPath_AsksForFolder()
    {
        var vm = CreateViewModel();

        await vm.OpenAsync([]);

        Assert.NotNull(vm.FolderMessage);
        Assert.Empty(vm.Episodes);
        Assert.Equal(0, _client.SearchRequests);
        Assert.False(vm.RenameCommand.CanExecute(null));
    }

    [Fact]
    public async Task Open_FileInsteadOfFolder_NamesIt()
    {
        var file = _temp.File("Eureka", "Staffel 01", "S01E01.mkv");
        var vm = CreateViewModel();

        await vm.OpenAsync([file]);

        Assert.Contains(file, vm.FolderMessage, StringComparison.Ordinal);
        Assert.Empty(vm.Episodes);
    }

    [Fact]
    public async Task Open_UnexpectedError_IsShownAndTheApplicationContinues()
    {
        var vm = CreateViewModel();

        await vm.OpenAsync(["invalid\0path"]); // a null character is not allowed in paths

        var error = Assert.Single(_dialogs.Errors);
        Assert.Equal(Resources.Strings.UnexpectedError, error.Header);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Open_SeasonFolderMode_ReportsIgnoredFolders()
    {
        var season = _temp.Folder("Eureka", "Staffel 01");
        var extras = _temp.Folder("Eureka", "Extras");
        _temp.File("Eureka", "Staffel 01", "S01E01.mkv");
        var vm = CreateViewModel();

        await vm.OpenAsync([season, extras]);
        await vm.PendingShow;

        Assert.Contains("Extras", vm.FolderMessage, StringComparison.Ordinal);
        Assert.Single(vm.Episodes);
        Assert.Equal("S01E01 - Pilot DE", vm.Episodes[0].NewName);
    }

    [Fact]
    public async Task ChangingTheSearchTerm_SearchesAfterTheDelay()
    {
        var vm = await OpenAsync("Staffel 01/S01E01.mkv");

        vm.SearchTerm = "Other";
        await vm.PendingSearch;
        await vm.PendingShow;

        Assert.Equal(2, vm.SelectedShow?.Show.Id);
        Assert.Equal(NewNameState.NotFound, vm.Episodes[0].State);
        Assert.Null(vm.Poster);
    }

    [Fact]
    public async Task ChangingTheSearchTerm_OnlyTheLastInputIsSearched()
    {
        var vm = new MainWindowViewModel(new TmdbService(_client, ["de-DE", "en-US"]), _dialogs, "de-DE") { SearchDelay = TimeSpan.FromHours(1) };

        vm.SearchTerm = "Eur";
        var first = vm.PendingSearch;
        vm.SearchTerm = "Eureka";

        await first; // cancelled by the second input
        Assert.Equal(0, _client.SearchRequests);
    }

    [Fact]
    public async Task EmptySearchTerm_ClearsResultsNamesAndPoster()
    {
        var vm = await OpenAsync("Staffel 01/S01E01.mkv");

        vm.SearchTerm = "  ";
        await vm.PendingSearch;
        await vm.PendingShow;

        Assert.Empty(vm.SearchResults);
        Assert.Null(vm.SelectedShow);
        Assert.Null(vm.Poster);
        Assert.Equal(NewNameState.None, vm.Episodes[0].State);
        Assert.False(vm.RenameCommand.CanExecute(null));
    }

    [Fact]
    public async Task OnlyTheLatestSearchResultIsShown()
    {
        var vm = await OpenAsync("Staffel 01/S01E01.mkv");
        var slow = new TaskCompletionSource();
        _client.Gate = query => query == "Other" ? slow.Task : Task.CompletedTask;

        vm.SearchTerm = "Other";
        var slowSearch = vm.PendingSearch;
        vm.SearchTerm = "Eureka ";
        await vm.PendingSearch;
        await vm.PendingShow;
        slow.SetResult();
        await slowSearch;
        await vm.PendingShow;

        Assert.Equal(EurekaId, vm.SelectedShow?.Show.Id);
        Assert.Equal("S01E01 - Pilot DE", vm.Episodes[0].NewName);
        Assert.False(vm.IsSearching);
    }

    [Fact]
    public async Task ChangingTheLanguage_ReloadsTheNames()
    {
        var vm = await OpenAsync("Staffel 01/S01E01.mkv");

        vm.SelectedLanguage = vm.Languages.Single(l => l.Code == "en-US");
        await vm.PendingShow;

        Assert.Equal("S01E01 - Pilot", vm.Episodes[0].NewName);
    }

    [Fact]
    public async Task Languages_OfTheSelectedShow_StartLanguageSelected()
    {
        _client.Translations[EurekaId] =
        [
            new TvTranslation("en", "US", "English"),
            new TvTranslation("de", "DE", "German"),
            new TvTranslation("fr", "FR", "French"),
        ];

        var vm = await OpenAsync("Staffel 01/S01E01.mkv");

        Assert.Equal(["de-DE", "en-US", "fr-FR"], vm.Languages.Select(l => l.Code).Order());
        Assert.Equal("de-DE", vm.SelectedLanguage?.Code);
        Assert.Equal("S01E01 - Pilot DE", vm.Episodes[0].NewName);
    }

    [Fact]
    public async Task Languages_StartLanguageNotAvailable_SameLanguageOrEnglish()
    {
        _client.Searches["Other"] = [new SearchTvResult(2, "Other Show", null, null, 1)];
        _client.Episodes[(2, "de-AT")] = [new TvEpisode(1, 1, "Pilot AT")];
        _client.Episodes[(2, "en-US")] = [new TvEpisode(1, 1, "Pilot")];
        _client.Episodes[(3, "en-US")] = [new TvEpisode(1, 1, "Third")];
        _client.Episodes[(3, "ja-JP")] = [new TvEpisode(1, 1, "Japanese")];
        _client.Searches["Third"] = [new SearchTvResult(3, "Third Show", null, null, 1)];
        var vm = await OpenAsync("Staffel 01/S01E01.mkv");

        vm.SearchTerm = "Other";
        await vm.PendingSearch;
        await vm.PendingShow;
        Assert.Equal("de-AT", vm.SelectedLanguage?.Code); // same language, other region

        vm.SearchTerm = "Third";
        await vm.PendingSearch;
        await vm.PendingShow;
        Assert.Equal("en-US", vm.SelectedLanguage?.Code); // English
        Assert.Equal("S01E01 - Third", vm.Episodes[0].NewName);
    }

    [Fact]
    public async Task Languages_SelectedLanguageIsKeptForOtherShows()
    {
        _client.Episodes[(EurekaId, "fr-FR")] = [new TvEpisode(1, 1, "Rencontre")];
        _client.Episodes[(2, "fr-FR")] = [new TvEpisode(1, 1, "Autre")];
        _client.Episodes[(2, "de-DE")] = [new TvEpisode(1, 1, "Andere")];
        var vm = await OpenAsync("Staffel 01/S01E01.mkv");

        vm.SelectedLanguage = vm.Languages.Single(l => l.Code == "fr-FR");
        await vm.PendingShow;
        Assert.Equal("S01E01 - Rencontre", vm.Episodes[0].NewName);

        vm.SearchTerm = "Other";
        await vm.PendingSearch;
        await vm.PendingShow;

        Assert.Equal("fr-FR", vm.SelectedLanguage?.Code);
        Assert.Equal("S01E01 - Autre", vm.Episodes[0].NewName);
    }

    [Fact]
    public async Task Languages_ClearedWithoutShow()
    {
        var vm = await OpenAsync("Staffel 01/S01E01.mkv");

        vm.SearchTerm = "";
        await vm.PendingSearch;
        await vm.PendingShow;

        Assert.Empty(vm.Languages);
        Assert.Null(vm.SelectedLanguage);
    }

    [Theory]
    [InlineData("de-DE", "German", "German (Germany)")]
    [InlineData("pt-BR", "Portuguese", "Portuguese (Brazil)")]
    [InlineData("xx-YY", "Unknown", "Unknown (YY)")]
    public void LanguageDisplayName(string code, string englishName, string expected)
    {
        var previous = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        try
        {
            Assert.Equal(expected, MainWindowViewModel.LanguageDisplayName(new TmdbLanguage(code, englishName)));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public async Task TmdbError_IsShownAndCanBeRetried()
    {
        _client.Fail = true;
        var vm = await OpenAsync("Staffel 01/S01E01.mkv");

        Assert.NotNull(vm.TmdbError);
        Assert.Empty(vm.SearchResults);
        Assert.True(vm.RetryCommand.CanExecute(null));

        _client.Fail = false;
        await vm.RetryCommand.ExecuteAsync(null);
        await vm.PendingShow;

        Assert.Null(vm.TmdbError);
        Assert.Equal("S01E01 - Pilot DE", vm.Episodes[0].NewName);
    }

    [Fact]
    public async Task TmdbErrorWhileLoadingEpisodes_IsShownAndCanBeRetried()
    {
        var vm = await OpenAsync("Staffel 01/S01E01.mkv");
        _client.Fail = true;

        vm.SelectedLanguage = vm.Languages.Single(l => l.Code == "en-US");
        await vm.PendingShow;

        Assert.NotNull(vm.TmdbError);
        Assert.Equal(NewNameState.None, vm.Episodes[0].State);

        _client.Fail = false;
        await vm.RetryCommand.ExecuteAsync(null);

        Assert.Null(vm.TmdbError);
        Assert.Equal("S01E01 - Pilot", vm.Episodes[0].NewName);
    }

    [Fact]
    public async Task DuplicateEpisodeNumbers_BlockRenamingUntilDeselected()
    {
        var vm = await OpenAsync("Staffel 01/S01E01.mkv", "Staffel 01/S01E01 copy.mkv", "Staffel 01/S01E02.mkv");

        Assert.NotNull(vm.ConflictMessage);
        Assert.Contains("S01E01 copy.mkv", vm.ConflictMessage, StringComparison.Ordinal);
        Assert.Equal([true, true, false], vm.Episodes.Select(r => r.IsConflict));
        Assert.False(vm.RenameCommand.CanExecute(null));

        vm.Episodes[1].IsSelected = false;

        Assert.Null(vm.ConflictMessage);
        Assert.Equal([false, false, false], vm.Episodes.Select(r => r.IsConflict));
        Assert.True(vm.RenameCommand.CanExecute(null));
    }

    [Fact]
    public async Task DuplicatesThatAreNotRenamed_AreNoConflict()
    {
        // both already have their final name (no checkbox, nothing to deselect)
        var vm = await OpenAsync("Staffel 01/S01E01 - Pilot DE.mkv", "Staffel 01/S01E01 - Pilot DE.avi");

        Assert.Null(vm.ConflictMessage);
    }

    [Fact]
    public async Task Rename_RenamesSelectedFiles()
    {
        var vm = await OpenAsync("Staffel 01/S01E01.mkv", "Staffel 01/x.MKV", "Staffel 01/S01E03.avi",
            "Staffel 02/s02e01.mkv");
        vm.Episodes[1].IsSelected = false;

        await vm.RenameCommand.ExecuteAsync(null);

        Assert.Empty(_dialogs.Errors);
        Assert.Equal(["S01E01 - Pilot DE.mkv", "S01E03.avi", "x.MKV"], _temp.Entries("Eureka", "Staffel 01"));
        Assert.Equal(["S02E01 - Phönix.mkv"], _temp.Entries("Eureka", "Staffel 02"));
        Assert.Equal(
            [
                ("S01E01 - Pilot DE", "S01E01 - Pilot DE", NewNameState.Same),
                ("x", "S01E02 - Glückliche Wiederkehr", NewNameState.Changed),
                ("S01E03", null, NewNameState.NotFound),
                ("S02E01 - Phönix", "S02E01 - Phönix", NewNameState.Same),
            ],
            Rows(vm));
        Assert.False(vm.IsRenaming);
        Assert.False(vm.RenameCommand.CanExecute(null)); // the deselected file is the only candidate left

        // renamed files can be renamed again, e.g. in another language
        vm.SelectedLanguage = vm.Languages.Single(l => l.Code == "en-US");
        await vm.PendingShow;
        await vm.RenameCommand.ExecuteAsync(null);
        Assert.Equal(["S01E01 - Pilot.mkv", "S01E03.avi", "x.MKV"], _temp.Entries("Eureka", "Staffel 01"));
    }

    [Fact]
    public async Task Rename_ReportsFailuresAndContinues()
    {
        var vm = await OpenAsync("Staffel 01/S01E01.mkv", "Staffel 01/x.mkv");
        _temp.Folder("Eureka", "Staffel 01", "S01E01 - Pilot DE.mkv"); // blocks the new name

        await vm.RenameCommand.ExecuteAsync(null);

        var error = Assert.Single(_dialogs.Errors);
        Assert.Contains("S01E01.mkv", error.Text, StringComparison.Ordinal);
        Assert.Equal(
            ["S01E01 - Pilot DE.mkv", "S01E01.mkv", "S01E02 - Glückliche Wiederkehr.mkv"],
            _temp.Entries("Eureka", "Staffel 01"));
        Assert.Equal(NewNameState.Changed, vm.Episodes[0].State); // can be retried
        Assert.Equal(NewNameState.Same, vm.Episodes[1].State);
    }

    [Fact]
    public async Task OpeningAnotherFolder_ReplacesTheFiles()
    {
        var vm = await OpenAsync("Staffel 01/S01E01.mkv");
        var other = _temp.Folder("Other");
        _temp.File("Other", "Staffel 01", "a.mkv");
        _temp.File("Other", "Staffel 01", "b.mkv");

        await vm.OpenAsync([other]);
        await vm.PendingShow;

        Assert.Equal(other, vm.ShowPath);
        Assert.Equal("Other", vm.SearchTerm);
        Assert.Equal(["a", "b"], vm.Episodes.Select(r => r.OldName));
        Assert.Equal(2, vm.SelectedShow?.Show.Id);
    }
}
