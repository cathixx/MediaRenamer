using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MediaRenamer.Core.Episodes;
using MediaRenamer.Core.Naming;
using MediaRenamer.Core.Tmdb;
using MediaRenamer.Resources;

namespace MediaRenamer.ViewModels;

/// <summary>State of the new file name of a row (section 6.2 of the requirements).</summary>
public enum NewNameState
{
    /// <summary>No show selected: nothing is shown.</summary>
    None,

    /// <summary>The episode titles are being loaded.</summary>
    Loading,

    /// <summary>TMDB does not know the episode (T-14).</summary>
    NotFound,

    /// <summary>The new name equals the old one.</summary>
    Same,

    /// <summary>The new name differs from the old one only in case.</summary>
    CaseOnly,

    /// <summary>The new name differs from the old one.</summary>
    Changed,
}

/// <summary>A row of the table: a video file with its old and new name.</summary>
public sealed partial class EpisodeRowViewModel(EpisodeFile file) : ObservableObject
{
    private bool _notFound;

    /// <summary>The file; path and name change after renaming.</summary>
    public EpisodeFile File { get; private set; } = file;

    /// <summary>Consecutive number, three digits (D-8).</summary>
    public string NumberText => File.Number.ToString("000", CultureInfo.InvariantCulture);

    /// <summary>Whether a separator is shown before this row (first row of a further season).</summary>
    public bool StartsNewSeason => File.StartsNewSeason;

    /// <summary>Current file name without extension.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(State), nameof(NewNameText), nameof(IsCheckboxVisible), nameof(IsRenameCandidate))]
    public partial string OldName { get; private set; } = file.Name;

    /// <summary>Proposed file name without extension, or <c>null</c> if there is none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(State), nameof(NewNameText), nameof(IsCheckboxVisible), nameof(IsRenameCandidate))]
    public partial string? NewName { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(State), nameof(NewNameText), nameof(IsCheckboxVisible))]
    public partial bool IsLoading { get; private set; }

    /// <summary>Whether the file is selected for renaming (initially selected, D-8).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRenameCandidate))]
    public partial bool IsSelected { get; set; } = true;

    /// <summary>Whether the file is part of an episode number conflict (R-1).</summary>
    [ObservableProperty]
    public partial bool IsConflict { get; set; }

    public NewNameState State =>
        IsLoading ? NewNameState.Loading
        : _notFound ? NewNameState.NotFound
        : NewName is null ? NewNameState.None
        : string.Equals(NewName, OldName, StringComparison.Ordinal) ? NewNameState.Same
        : string.Equals(NewName, OldName, StringComparison.OrdinalIgnoreCase) ? NewNameState.CaseOnly
        : NewNameState.Changed;

    public string NewNameText => State switch
    {
        NewNameState.Loading => Strings.Loading,
        NewNameState.NotFound => Strings.NotFound,
        _ => NewName ?? string.Empty,
    };

    /// <summary>The checkbox is only shown if the file can be renamed (6.2).</summary>
    public bool IsCheckboxVisible => State is NewNameState.CaseOnly or NewNameState.Changed;

    /// <summary>Whether the file will be renamed (R-3).</summary>
    public bool IsRenameCandidate =>
        IsSelected && NewName is not null && !string.Equals(NewName, OldName, StringComparison.Ordinal);

    /// <summary>No show selected: no new name (T-15).</summary>
    public void Clear() => Update(loading: false, newName: null, notFound: false);

    /// <summary>The episode titles are being loaded.</summary>
    public void SetLoading() => Update(loading: true, newName: null, notFound: false);

    /// <summary>Sets the new name from the episode titles (T-13, T-14).</summary>
    public void SetTitles(EpisodeTitles titles)
    {
        if (titles.TryGetTitle(File.Season, File.Episodes.First, out var title))
        {
            Update(loading: false, EpisodeNameBuilder.Build(File.Season, File.Episodes, title), notFound: false);
        }
        else
        {
            Update(loading: false, newName: null, notFound: true);
        }
    }

    /// <summary>The file was renamed to <see cref="NewName"/> (R-6).</summary>
    public void MarkRenamed(string newPath)
    {
        var newName = NewName ?? throw new InvalidOperationException("The row has no new name.");
        File = File with { Path = newPath, Name = newName };
        OldName = newName;
    }

    private void Update(bool loading, string? newName, bool notFound)
    {
        _notFound = notFound;
        NewName = newName;
        IsLoading = loading;
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(NewNameText));
        OnPropertyChanged(nameof(IsCheckboxVisible));
    }
}

/// <summary>A language offered for episode titles (6.1).</summary>
public sealed record LanguageOption(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>A show in the result list (T-10).</summary>
public sealed partial class ShowItemViewModel(Show show, string language) : ObservableObject
{
    public Show Show { get; } = show;

    [ObservableProperty]
    public partial string DisplayName { get; private set; } = show.DisplayName(language);

    public void SetLanguage(string language) => DisplayName = Show.DisplayName(language);
}
