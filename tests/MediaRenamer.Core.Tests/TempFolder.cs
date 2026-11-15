namespace MediaRenamer.Core.Tests;

/// <summary>A temporary folder for file system tests, deleted on dispose.</summary>
public sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MediaRenamerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    /// <summary>Creates a sub folder (and its parents) and returns its path.</summary>
    public string Folder(params string[] names)
    {
        var path = System.IO.Path.Combine([Path, .. names]);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Creates an empty file (and its folders) and returns its path.</summary>
    public string File(params string[] names)
    {
        var path = System.IO.Path.Combine([Path, .. names]);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, path);
        return path;
    }

    /// <summary>Names of all files and folders in the given folder, sorted.</summary>
    public IReadOnlyList<string> Entries(params string[] names) =>
        Directory.EnumerateFileSystemEntries(System.IO.Path.Combine([Path, .. names]))
            .Select(System.IO.Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToList()!;

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // best effort
        }
    }
}
