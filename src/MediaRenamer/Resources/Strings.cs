using System.Globalization;
using System.Resources;

[assembly: NeutralResourcesLanguage("en")]

namespace MediaRenamer.Resources;

/// <summary>
/// Typed access to the localized UI texts in <c>Strings.resx</c> (English, neutral) and
/// <c>Strings.&lt;culture&gt;.resx</c> (translations). The language is taken from
/// <see cref="CultureInfo.CurrentUICulture"/> at the time of the lookup.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager = new("MediaRenamer.Resources.Strings", typeof(Strings).Assembly);

    private static string Get(string name) => Manager.GetString(name, CultureInfo.CurrentUICulture) ?? $"!{name}!";

    private static string Format(string name, string value) =>
        string.Format(CultureInfo.CurrentUICulture, Get(name), value);

    public static string SearchPlaceholder => Get(nameof(SearchPlaceholder));
    public static string Language => Get(nameof(Language));
    public static string Rename => Get(nameof(Rename));
    public static string ChooseFolder => Get(nameof(ChooseFolder));
    public static string ChooseFolderTitle => Get(nameof(ChooseFolderTitle));
    public static string ColumnNumber => Get(nameof(ColumnNumber));
    public static string ColumnOldName => Get(nameof(ColumnOldName));
    public static string ColumnNewName => Get(nameof(ColumnNewName));
    public static string Loading => Get(nameof(Loading));
    public static string NotFound => Get(nameof(NotFound));
    public static string Retry => Get(nameof(Retry));
    public static string Ok => Get(nameof(Ok));
    public static string ChooseShowFolder => Get(nameof(ChooseShowFolder));
    public static string NotAFolder(string value) => Format("NotAFolderFormat", value);
    public static string NoSeasonFolders(string value) => Format("NoSeasonFoldersFormat", value);
    public static string IgnoredPaths(string value) => Format("IgnoredPathsFormat", value);
    public static string FolderReadFailed(string value) => Format("FolderReadFailedFormat", value);
    public static string SearchFailed(string value) => Format("SearchFailedFormat", value);
    public static string EpisodesFailed(string value) => Format("EpisodesFailedFormat", value);
    public static string PosterFailed(string value) => Format("PosterFailedFormat", value);
    public static string Conflicts(string value) => Format("ConflictsFormat", value);
    public static string Error => Get(nameof(Error));
    public static string ErrorOccurred => Get(nameof(ErrorOccurred));
    public static string UnexpectedError => Get(nameof(UnexpectedError));
    public static string RenameFailed(string value) => Format("RenameFailedFormat", value);
    public static string TargetExists => Get(nameof(TargetExists));
    public static string MissingApiKeyHeader => Get(nameof(MissingApiKeyHeader));
    public static string MissingApiKeyText => Get(nameof(MissingApiKeyText));
}
