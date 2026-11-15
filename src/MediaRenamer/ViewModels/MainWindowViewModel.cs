using System.Collections.ObjectModel;
using System.Globalization;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaRenamer.Core.Episodes;
using MediaRenamer.Core.Folders;
using MediaRenamer.Core.Naming;
using MediaRenamer.Core.Renaming;
using MediaRenamer.Core.Tmdb;
using MediaRenamer.Core.Updates;
using MediaRenamer.Resources;

namespace MediaRenamer.ViewModels;

/// <summary>Shows dialogs; implemented by the view.</summary>
public interface IDialogService
{
    Task ShowErrorAsync(string header, string text);

    /// <summary>Asks a question; returns whether the user chose <paramref name="yes"/>.</summary>
    Task<bool> ConfirmAsync(string title, string header, string text, string yes, string no);
}

/// <summary>
/// Controls the main window: show folder, show search, episode titles, new names and renaming
/// (sections 2 to 8 of the requirements).
/// </summary>
/// <remarks>
/// All public members are used on the UI thread. File system work runs in the background; network requests are
/// asynchronous. For every kind of request only the result of the most recently started one is shown (T-19).
/// </remarks>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly TmdbService _tmdb;
    private readonly IDialogService _dialogs;
    private readonly IUpdateService? _updates;
    private CancellationTokenSource? _searchDelay;
    private bool _settingSearchTerm;
    private int _searchVersion;
    private int _showVersion;
    private Func<Task>? _retry;
    private bool _settingLanguages;

    /// <summary>The language the user wants: initially the start language, then the last selected one (T-25).</summary>
    private string _preferredLanguage;

    /// <param name="tmdb">TMDB access.</param>
    /// <param name="dialogs">Dialogs of the view.</param>
    /// <param name="preferredLanguage">TMDB language code of the language the application was started with, e.g.
    /// <c>de-DE</c>; the default language of the episode titles.</param>
    /// <param name="updates">Updates of the application, <c>null</c> to never check for updates.</param>
    public MainWindowViewModel(TmdbService tmdb, IDialogService dialogs, string preferredLanguage,
        IUpdateService? updates = null)
    {
        _tmdb = tmdb;
        _dialogs = dialogs;
        _updates = updates;
        _preferredLanguage = preferredLanguage;
    }

    /// <summary>Delay between the last change of the search term and the search (T-1).</summary>
    public TimeSpan SearchDelay { get; init; } = TimeSpan.FromSeconds(1);

    public ObservableCollection<EpisodeRowViewModel> Episodes { get; } = [];

    public ObservableCollection<ShowItemViewModel> SearchResults { get; } = [];

    /// <summary>Languages of the selected show, sorted by their display name (T-24).</summary>
    public ObservableCollection<LanguageOption> Languages { get; } = [];

    /// <summary>The running delayed search, for tests.</summary>
    internal Task PendingSearch { get; private set; } = Task.CompletedTask;

    /// <summary>The running load of the selected show, for tests.</summary>
    internal Task PendingShow { get; private set; } = Task.CompletedTask;

    /// <summary>Absolute path of the show folder (status bar, E-5).</summary>
    [ObservableProperty]
    public partial string? ShowPath { get; private set; }

    /// <summary>
    /// Folder in which the folder selection opens: the current show folder if it still exists, otherwise <c>null</c>
    /// (the default of the operating system) (E-8).
    /// </summary>
    public string? FolderSelectionStart => ShowPath is { } path && Directory.Exists(path) ? path : null;

    [ObservableProperty]
    public partial string SearchTerm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ShowItemViewModel? SelectedShow { get; set; }

    [ObservableProperty]
    public partial LanguageOption? SelectedLanguage { get; set; }

    /// <summary>Poster image of the selected show (T-16).</summary>
    [ObservableProperty]
    public partial byte[]? Poster { get; private set; }

    /// <summary>Hint about the folder (E-8 to E-10).</summary>
    [ObservableProperty]
    public partial string? FolderMessage { get; private set; }

    /// <summary>A failed TMDB request (T-20); <see cref="RetryCommand"/> repeats it.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    public partial string? TmdbError { get; private set; }

    /// <summary>Episode numbers that occur more than once among the selected files (R-1).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RenameCommand))]
    public partial string? ConflictMessage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(IsTableEnabled))]
    [NotifyCanExecuteChangedFor(nameof(RenameCommand))]
    public partial bool IsScanning { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(IsTableEnabled), nameof(IsResultListEnabled))]
    [NotifyCanExecuteChangedFor(nameof(RenameCommand))]
    public partial bool IsSearching { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(IsTableEnabled))]
    [NotifyCanExecuteChangedFor(nameof(RenameCommand))]
    public partial bool IsLoadingShow { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(IsTableEnabled), nameof(IsResultListEnabled),
        nameof(IsSearchEnabled))]
    [NotifyCanExecuteChangedFor(nameof(RenameCommand))]
    public partial bool IsRenaming { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(IsTableEnabled), nameof(IsResultListEnabled),
        nameof(IsSearchEnabled))]
    [NotifyCanExecuteChangedFor(nameof(RenameCommand))]
    public partial bool IsUpdating { get; private set; }

    /// <summary>Shown while an update is installed (UP-4).</summary>
    [ObservableProperty]
    public partial string? UpdateMessage { get; private set; }

    /// <summary>Any background task is running: progress indicator visible (6.3).</summary>
    public bool IsBusy => IsScanning || IsSearching || IsLoadingShow || IsRenaming || IsUpdating;

    public bool IsTableEnabled => !IsBusy;

    public bool IsResultListEnabled => !IsSearching && !IsRenaming && !IsUpdating;

    /// <summary>Search field, language and folder selection are disabled while renaming or updating.</summary>
    public bool IsSearchEnabled => !IsRenaming && !IsUpdating;

    /// <summary>
    /// Checks for a newer version and, after confirmation, downloads it (section 10). Returns the path of the new
    /// executable, which has to be started to complete the update (see <see cref="UpdateArguments"/>), or <c>null</c>.
    /// Errors while checking are only logged; errors while downloading are shown and the current version keeps
    /// running.
    /// </summary>
    /// <param name="currentVersion">Version of the running application, <c>null</c> for local builds (no check).</param>
    /// <param name="executablePath">Path of the running executable.</param>
    public async Task<string?> CheckForUpdateAsync(Version? currentVersion, string? executablePath)
    {
        if (_updates is null || currentVersion is null || executablePath is null)
        {
            return null;
        }

        AvailableUpdate? update;
        try
        {
            update = await _updates.CheckAsync(currentVersion);
        }
        catch (UpdateException e)
        {
            Console.Error.WriteLine(e.Message); // UP-1: e.g. offline, not worth a message
            return null;
        }

        if (update is null
            || !await _dialogs.ConfirmAsync(Strings.UpdateAvailableTitle, Strings.UpdateAvailableHeader,
                Strings.UpdateAvailable(Format(update.Version), Format(currentVersion)), Strings.UpdateNow,
                Strings.UpdateLater))
        {
            return null;
        }

        // a running rename is finished first
        while (IsRenaming)
        {
            await Task.Delay(100);
        }

        IsUpdating = true;
        UpdateMessage = Strings.Updating(Format(update.Version));
        try
        {
            return await _updates.DownloadAsync(update, executablePath);
        }
        catch (Exception e)
        {
            // every error while installing (not only the expected ones) keeps the current version running
            Console.Error.WriteLine(e);
            var details = e is UpdateException { InnerException: null } ? e.Message : ErrorDetails(e);
            await _dialogs.ShowErrorAsync(Strings.UpdateFailedHeader, Strings.UpdateFailed(details, update.ReleaseUrl));
            return null;
        }
        finally
        {
            IsUpdating = false;
            UpdateMessage = null;
        }
    }

    private static string Format(Version version) => version.ToString(3);

    /// <summary>Opens the show given by the start arguments or a chosen folder (section 2).</summary>
    public async Task OpenAsync(IReadOnlyList<string> paths)
    {
        IsScanning = true;
        try
        {
            ShowFolderResult result;
            IReadOnlyList<EpisodeFile> files;
            try
            {
                (result, files) = await Task.Run(() =>
                {
                    var r = ShowFolderResolver.Resolve(paths);
                    return (r, r.Problem == ShowFolderProblem.None ? EpisodeScanner.Scan(r.Seasons) : []);
                });
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                FolderMessage = Strings.FolderReadFailed(e.Message);
                return;
            }
            catch (Exception e)
            {
                await ShowUnexpectedErrorAsync(e);
                return;
            }

            ShowPath = result.ShowPath;
            FolderMessage = FolderMessageFor(result);
            SetRows(files);

            if (result.Problem == ShowFolderProblem.None && result.ShowPath is { } showPath)
            {
                // T-1: search immediately with the folder name
                _searchDelay?.Cancel();
                _settingSearchTerm = true;
                SearchTerm = Path.GetFileName(showPath);
                _settingSearchTerm = false;
                await SearchNowAsync();
            }
        }
        finally
        {
            IsScanning = false;
        }
    }

    partial void OnSearchTermChanged(string value)
    {
        if (_settingSearchTerm)
        {
            return;
        }

        // T-1: search 1 second after the last input
        _searchDelay?.Cancel();
        var delay = _searchDelay = new CancellationTokenSource();
        PendingSearch = SearchDelayedAsync(delay.Token);
    }

    private async Task SearchDelayedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SearchDelay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await SearchNowAsync();
    }

    /// <summary>Searches the show and selects the first result (T-2, T-9).</summary>
    internal async Task SearchNowAsync()
    {
        var version = ++_searchVersion;
        var term = SearchTerm.Trim();
        if (term.Length == 0)
        {
            SearchResults.Clear();
            SelectedShow = null;
            IsSearching = false;
            return;
        }

        IsSearching = true;
        TmdbError = null;
        try
        {
            var shows = await _tmdb.SearchAsync(term);
            if (version != _searchVersion)
            {
                return; // T-19
            }

            SearchResults.Clear();
            foreach (var show in shows)
            {
                SearchResults.Add(new ShowItemViewModel(show, SelectedLanguage?.Code ?? _preferredLanguage));
            }

            SelectedShow = SearchResults.FirstOrDefault();
        }
        catch (TmdbException e)
        {
            if (version == _searchVersion)
            {
                SearchResults.Clear();
                SelectedShow = null;
                SetTmdbError(Strings.SearchFailed(e.Message), SearchNowAsync);
            }
        }
        catch (Exception e)
        {
            await ShowUnexpectedErrorAsync(e);
        }
        finally
        {
            if (version == _searchVersion)
            {
                IsSearching = false;
            }
        }
    }

    partial void OnSelectedShowChanged(ShowItemViewModel? value) => PendingShow = LoadShowAsync(loadLanguages: true);

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (_settingLanguages || value is null)
        {
            return;
        }

        // the user selected the language: keep it for other shows as well (T-25)
        _preferredLanguage = value.Code;
        foreach (var result in SearchResults)
        {
            result.SetLanguage(value.Code);
        }

        PendingShow = LoadShowAsync(loadLanguages: false);
    }

    /// <summary>
    /// Loads the languages (if <paramref name="loadLanguages"/>), the episode titles and the poster of the selected
    /// show (T-11 to T-16, T-24, T-25).
    /// </summary>
    private async Task LoadShowAsync(bool loadLanguages)
    {
        var version = ++_showVersion;
        var show = SelectedShow?.Show;
        if (show is null)
        {
            foreach (var row in Episodes)
            {
                row.Clear();
            }

            SetLanguages([]);
            Poster = null;
            IsLoadingShow = false;
            UpdateConflicts();
            return;
        }

        IsLoadingShow = true;
        TmdbError = null;
        foreach (var row in Episodes)
        {
            row.SetLoading();
        }

        try
        {
            var posterTask = _tmdb.GetPosterAsync(show.PosterPath);
            try
            {
                if (loadLanguages)
                {
                    var languages = await _tmdb.GetLanguagesAsync(show.Id);
                    if (version != _showVersion)
                    {
                        return; // T-19
                    }

                    SetLanguages(languages);
                }

                var language = SelectedLanguage?.Code ?? _preferredLanguage;
                var titles = await _tmdb.GetEpisodeTitlesAsync(show.Id, language);
                if (version != _showVersion)
                {
                    return; // T-19
                }

                foreach (var row in Episodes)
                {
                    row.SetTitles(titles);
                }
            }
            catch (TmdbException e)
            {
                if (version == _showVersion)
                {
                    foreach (var row in Episodes)
                    {
                        row.Clear();
                    }

                    SetTmdbError(Strings.EpisodesFailed(e.Message), () => LoadShowAsync(loadLanguages));
                }
            }

            try
            {
                var poster = await posterTask;
                if (version == _showVersion)
                {
                    Poster = poster;
                }
            }
            catch (TmdbException e)
            {
                if (version == _showVersion)
                {
                    Poster = null;
                    SetTmdbError(Strings.PosterFailed(e.Message), () => LoadShowAsync(loadLanguages));
                }
            }
        }
        catch (Exception e)
        {
            await ShowUnexpectedErrorAsync(e);
        }
        finally
        {
            if (version == _showVersion)
            {
                IsLoadingShow = false;
                UpdateConflicts();
            }
        }
    }

    /// <summary>
    /// Offers the languages of the show and selects the one that fits the preferred language best (T-25), without
    /// changing the preferred language.
    /// </summary>
    private void SetLanguages(IReadOnlyList<TmdbLanguage> languages)
    {
        _settingLanguages = true;
        try
        {
            var best = TmdbLanguages.BestMatch(languages, _preferredLanguage);
            Languages.Clear();
            foreach (var option in languages.Select(l => new LanguageOption(l.Code, LanguageDisplayName(l)))
                         .OrderBy(o => o.DisplayName, StringComparer.CurrentCulture))
            {
                Languages.Add(option);
            }

            SelectedLanguage = best is null ? null : Languages.First(o => o.Code == best.Code);
        }
        finally
        {
            _settingLanguages = false;
        }
    }

    /// <summary>Name of a language in the language of the UI, e.g. "German (Germany)".</summary>
    internal static string LanguageDisplayName(TmdbLanguage language)
    {
        try
        {
            return CultureInfo.GetCultureInfo(language.Code, predefinedOnly: true).DisplayName;
        }
        catch (CultureNotFoundException)
        {
            return language.Region is { } region ? $"{language.EnglishName} ({region})" : language.EnglishName;
        }
    }

    private bool CanRetry() => TmdbError is not null && _retry is not null;

    /// <summary>Repeats the failed TMDB request (T-20).</summary>
    [RelayCommand(CanExecute = nameof(CanRetry))]
    private Task RetryAsync() => _retry?.Invoke() ?? Task.CompletedTask;

    private bool CanRename() => !IsBusy && ConflictMessage is null && Episodes.Any(row => row.IsRenameCandidate);

    /// <summary>Renames the selected files (section 7).</summary>
    [RelayCommand(CanExecute = nameof(CanRename))]
    private async Task RenameAsync()
    {
        var rows = Episodes.Where(row => row.IsRenameCandidate).ToList();
        IsRenaming = true;
        try
        {
            var requests = rows.Select(row => new RenameRequest(row.File.Path, row.NewName + row.File.Extension)).ToList();
            var results = await Task.Run(() => FileRenamer.RenameAll(requests));

            var failures = new List<string>();
            for (var i = 0; i < rows.Count; i++)
            {
                var result = results[i];
                var oldFileName = Path.GetFileName(result.Request.SourcePath);
                if (result.Outcome == RenameOutcome.Renamed)
                {
                    Console.WriteLine($"rename '{oldFileName}' -> '{result.Request.NewFileName}'");
                    rows[i].MarkRenamed(result.NewPath!);
                }
                else
                {
                    var reason = result.Outcome == RenameOutcome.TargetExists ? Strings.TargetExists : result.Error;
                    Console.WriteLine($"rename '{oldFileName}' -> '{result.Request.NewFileName}' failed: {reason}");
                    failures.Add($"{oldFileName}: {reason}");
                }
            }

            if (failures.Count > 0)
            {
                await _dialogs.ShowErrorAsync(Strings.ErrorOccurred, Strings.RenameFailed(string.Join('\n', failures)));
            }
        }
        catch (Exception e)
        {
            await ShowUnexpectedErrorAsync(e);
        }
        finally
        {
            IsRenaming = false;
            UpdateConflicts();
        }
    }

    private void SetRows(IReadOnlyList<EpisodeFile> files)
    {
        foreach (var row in Episodes)
        {
            row.PropertyChanged -= OnRowChanged;
        }

        Episodes.Clear();
        foreach (var file in files)
        {
            // the names are set when the episode titles of the selected show are loaded
            var row = new EpisodeRowViewModel(file);
            row.PropertyChanged += OnRowChanged;
            Episodes.Add(row);
        }

        UpdateConflicts();
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EpisodeRowViewModel.IsSelected) or nameof(EpisodeRowViewModel.NewName))
        {
            UpdateConflicts();
        }
    }

    /// <summary>
    /// Finds episode numbers that occur more than once among the selected files (R-1). A conflict only matters if
    /// at least one of its files would be renamed.
    /// </summary>
    private void UpdateConflicts()
    {
        var rowsByPath = Episodes.ToDictionary(row => row.File.Path);
        var conflicts = DuplicateEpisodeCheck.FindConflicts(Episodes.Where(row => row.IsSelected).Select(row => row.File))
            .Where(conflict => conflict.Files.Any(file => rowsByPath[file.Path].IsRenameCandidate))
            .ToList();

        var conflictPaths = conflicts.SelectMany(c => c.Files).Select(f => f.Path).ToHashSet();
        foreach (var row in Episodes)
        {
            row.IsConflict = conflictPaths.Contains(row.File.Path);
        }

        ConflictMessage = conflicts.Count == 0
            ? null
            : Strings.Conflicts(string.Join("; ", conflicts.Select(c =>
                $"{EpisodeNameBuilder.EpisodeCode(c.Season, new EpisodeNumbers(c.Episode))}: "
                + string.Join(", ", c.Files.Select(f => f.Name + f.Extension)))));
        RenameCommand.NotifyCanExecuteChanged();
    }

    private void SetTmdbError(string message, Func<Task> retry)
    {
        _retry = retry;
        TmdbError = message;
        RetryCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Shows an unexpected error; the application continues (section 8).</summary>
    internal Task ShowUnexpectedErrorAsync(Exception e)
    {
        Console.Error.WriteLine(e);
        return _dialogs.ShowErrorAsync(Strings.UnexpectedError, ErrorDetails(e));
    }

    /// <summary>
    /// Message, type and stack trace of an unexpected error, so it can be reported (the message alone may be empty).
    /// </summary>
    internal static string ErrorDetails(Exception e) =>
        string.IsNullOrWhiteSpace(e.Message) ? e.ToString() : $"{e.Message}\n\n{e}";

    private static string? FolderMessageFor(ShowFolderResult result)
    {
        var problem = result.Problem switch
        {
            ShowFolderProblem.NoPath => Strings.ChooseShowFolder,
            ShowFolderProblem.NotAFolder => Strings.NotAFolder(result.InvalidPath ?? string.Empty),
            ShowFolderProblem.NoSeasonFolders => Strings.NoSeasonFolders(result.ShowPath ?? string.Empty),
            _ => null,
        };
        var ignored = result.Ignored.Count == 0
            ? null
            : Strings.IgnoredPaths(string.Join(", ", result.Ignored.Select(i => Path.GetFileName(i.Path))));
        return problem is null ? ignored
            : ignored is null ? problem
            : problem + "\n" + ignored;
    }
}
