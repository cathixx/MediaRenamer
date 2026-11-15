namespace MediaRenamer.Core.Renaming;

/// <summary>Request to rename a file within its folder.</summary>
/// <param name="SourcePath">Path of the file to rename.</param>
/// <param name="NewFileName">New file name including the extension, without a folder.</param>
public sealed record RenameRequest(string SourcePath, string NewFileName);

/// <summary>Outcome of renaming a single file.</summary>
public enum RenameOutcome
{
    /// <summary>The file was renamed.</summary>
    Renamed,

    /// <summary>A file or folder with the new name already exists; nothing was changed.</summary>
    TargetExists,

    /// <summary>Renaming failed, see <see cref="RenameResult.Error"/>; nothing was changed.</summary>
    Failed,
}

/// <summary>Result of renaming a single file.</summary>
/// <param name="Request">The request.</param>
/// <param name="Outcome">What happened.</param>
/// <param name="NewPath">Path of the file after renaming (<see cref="RenameOutcome.Renamed"/>).</param>
/// <param name="Error">Technical error message (<see cref="RenameOutcome.Failed"/>).</param>
public sealed record RenameResult(RenameRequest Request, RenameOutcome Outcome, string? NewPath = null,
    string? Error = null);

/// <summary>Renames files within their folders. An existing file is never overwritten.</summary>
public static class FileRenamer
{
    /// <summary>
    /// Renames the files one after another. A failure does not stop the others; the results tell which files failed.
    /// </summary>
    public static IReadOnlyList<RenameResult> RenameAll(IEnumerable<RenameRequest> requests) =>
        requests.Select(Rename).ToList();

    /// <summary>Renames a single file. Changing only the case of the name (<c>s01e02</c> → <c>S01E02</c>) is supported
    /// on case-insensitive file systems as well.</summary>
    public static RenameResult Rename(RenameRequest request)
    {
        try
        {
            var source = Path.GetFullPath(request.SourcePath);
            var folder = Path.GetDirectoryName(source)
                         ?? throw new ArgumentException("The file is not located in a folder.", nameof(request));
            if (request.NewFileName.Length == 0
                || request.NewFileName.IndexOfAny(['/', '\\', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0
                || request.NewFileName is "." or "..")
            {
                throw new ArgumentException($"Invalid file name '{request.NewFileName}'.", nameof(request));
            }

            var sourceName = Path.GetFileName(source);
            var target = Path.Combine(folder, request.NewFileName);

            var caseOnly = !string.Equals(sourceName, request.NewFileName, StringComparison.Ordinal)
                           && string.Equals(sourceName, request.NewFileName, StringComparison.OrdinalIgnoreCase);

            if (IsOccupied(folder, target, request.NewFileName, caseOnly))
            {
                return new RenameResult(request, RenameOutcome.TargetExists);
            }

            if (caseOnly)
            {
                MoveCaseOnly(source, target, folder);
            }
            else
            {
                File.Move(source, target, overwrite: false);
            }

            return new RenameResult(request, RenameOutcome.Renamed, NewPath: target);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException
                                      or NotSupportedException)
        {
            return new RenameResult(request, RenameOutcome.Failed, Error: e.Message);
        }
    }

    private static bool IsOccupied(string folder, string target, string newFileName, bool caseOnly)
    {
        if (!caseOnly)
        {
            // on case-insensitive file systems this also finds names differing in case only
            return File.Exists(target) || Directory.Exists(target);
        }

        // On case-insensitive file systems the target "exists" because it is the source file itself. On case-sensitive
        // file systems a different file can have exactly the new name, which is only visible in the folder listing.
        return Directory.EnumerateFileSystemEntries(folder)
            .Any(entry => string.Equals(Path.GetFileName(entry), newFileName, StringComparison.Ordinal));
    }

    /// <summary>Renames via a temporary name, because some file systems ignore a rename that only changes the case.</summary>
    private static void MoveCaseOnly(string source, string target, string folder)
    {
        var temporary = Path.Combine(folder, $".{Path.GetFileName(source)}.{Guid.NewGuid():N}.renaming");
        File.Move(source, temporary, overwrite: false);
        try
        {
            File.Move(temporary, target, overwrite: false);
        }
        catch
        {
            File.Move(temporary, source, overwrite: false);
            throw;
        }
    }
}
