using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace MediaRenamer.Core.Updates;

/// <summary>Checking for or installing an update failed.</summary>
public sealed class UpdateException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>A newer release that can be installed.</summary>
/// <param name="Version">Version of the release, e.g. 1.2.0.</param>
/// <param name="ReleaseUrl">Web page of the release, for a manual download.</param>
/// <param name="Asset">The file of the release for the current platform.</param>
public sealed record AvailableUpdate(Version Version, string ReleaseUrl, GitHubAsset Asset);

/// <summary>Finds and installs updates of the application (section 10 of the requirements).</summary>
public interface IUpdateService
{
    /// <summary>
    /// Returns the latest release if it is newer than <paramref name="currentVersion"/> and has a file for the current
    /// platform, otherwise <c>null</c> (UP-1, UP-2).
    /// </summary>
    Task<AvailableUpdate?> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads and verifies the update and puts the new executable next to <paramref name="executablePath"/>
    /// (<c>&lt;name&gt;.new</c>, UP-4). The running executable is not changed: a single-file application loads parts of
    /// itself from its file while it runs. The new executable completes the update, see
    /// <see cref="UpdateService.CompleteUpdate"/>. Returns the path of the new executable.
    /// </summary>
    Task<string> DownloadAsync(AvailableUpdate update, string executablePath, CancellationToken cancellationToken = default);
}

/// <summary>Updates from the GitHub releases of the project.</summary>
/// <param name="httpClient">HTTP client.</param>
/// <param name="assetName">Name of the release file for the current platform, see <see cref="AssetNameFor"/>.</param>
/// <param name="executableName">Name of the executable in the release file, see <see cref="ExecutableNameFor"/>.</param>
public sealed class UpdateService(HttpClient httpClient, string assetName, string executableName) : IUpdateService
{
    public const string Repository = "cathixx/MediaRenamer";
    public const string ReleasesUrl = $"https://github.com/{Repository}/releases/latest";

    private static readonly Uri LatestReleaseUri = new($"https://api.github.com/repos/{Repository}/releases/latest");

    /// <summary>Suffix of the downloaded new executable, which completes the update.</summary>
    public const string NewSuffix = ".new";

    /// <summary>Suffix of the previous executable left by versions up to 1.0.3, which replaced the running file.</summary>
    private const string OldSuffix = ".old";

    private const string DownloadSuffix = ".download";

    /// <summary>Update service for the platform the application runs on.</summary>
    public static UpdateService ForCurrentPlatform(HttpClient httpClient) =>
        new(httpClient, AssetNameFor(CurrentRuntime()), ExecutableNameFor(CurrentRuntime()));

    /// <summary>Runtime identifier of the current platform, e.g. <c>win-x64</c>, <c>osx-arm64</c>.</summary>
    public static string CurrentRuntime()
    {
        var os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
        var architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        return $"{os}-{architecture}";
    }

    /// <summary>Name of the release file, e.g. <c>MediaRenamer-win-x64.zip</c>, <c>MediaRenamer-linux-x64.tar.gz</c>.</summary>
    public static string AssetNameFor(string runtime) =>
        runtime.StartsWith("win-", StringComparison.Ordinal)
            ? $"MediaRenamer-{runtime}.zip"
            : $"MediaRenamer-{runtime}.tar.gz";

    /// <summary>Name of the executable in the release file.</summary>
    public static string ExecutableNameFor(string runtime) =>
        runtime.StartsWith("win-", StringComparison.Ordinal) ? "MediaRenamer.exe" : "MediaRenamer";

    /// <summary>
    /// Parses a release version: <c>v1.2.3</c> or <c>1.2.3+commit</c>. Pre-release versions like
    /// <c>0.0.0-snapshot</c> (local builds) are not valid release versions.
    /// </summary>
    public static Version? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var version = text.Trim().TrimStart('v', 'V');
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
        {
            version = version[..plus];
        }

        return Version.TryParse(version, out var parsed) ? Normalize(parsed) : null;
    }

    /// <summary>
    /// Removes the files left by an update (UP-6): the new executable that completed the update (it may still be
    /// exiting, so this is retried for a few seconds) and the previous executable of older versions. Errors are ignored.
    /// </summary>
    public static async Task CleanUpAsync(string executablePath)
    {
        foreach (var file in new[] { executablePath + NewSuffix, executablePath + OldSuffix })
        {
            for (var attempt = 0; attempt < 20 && File.Exists(file); attempt++)
            {
                if (TryDelete(file))
                {
                    break;
                }

                await Task.Delay(250).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Completes an update, called in the new executable <paramref name="newExecutable"/> (UP-4): waits until the
    /// previous version (process <paramref name="previousProcessId"/>) has exited, then copies the new executable over
    /// <paramref name="target"/>. Copying is retried for a while, e.g. while a virus scanner checks the file.
    /// </summary>
    public static void CompleteUpdate(string newExecutable, string target, int? previousProcessId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        if (previousProcessId is { } processId)
        {
            WaitForExit(processId, timeout);
        }

        while (true)
        {
            try
            {
                File.Copy(newExecutable, target, overwrite: true);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(250);
            }
        }
    }

    private static void WaitForExit(int processId, TimeSpan timeout)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            process.WaitForExit(timeout);
        }
        catch (ArgumentException)
        {
            // already exited
        }
    }

    public async Task<AvailableUpdate?> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        GitHubRelease? release;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUri);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.UserAgent.ParseAdd("MediaRenamer"); // required by the GitHub API
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new UpdateException(
                    $"Checking for updates failed: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            release = await response.Content.ReadFromJsonAsync(GitHubJsonContext.Default.GitHubRelease,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException
                                  || (e is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new UpdateException($"Checking for updates failed: {e.Message}", e);
        }

        var version = ParseVersion(release?.TagName);
        var asset = release?.Assets?.FirstOrDefault(a => a.Name == assetName && a.DownloadUrl is not null);
        if (version is null || asset is null || version <= Normalize(currentVersion))
        {
            return null;
        }

        return new AvailableUpdate(version, release!.HtmlUrl ?? ReleasesUrl, asset);
    }

    public async Task<string> DownloadAsync(AvailableUpdate update, string executablePath,
        CancellationToken cancellationToken = default)
    {
        var download = executablePath + DownloadSuffix;
        var newExecutable = executablePath + NewSuffix;
        try
        {
            await DownloadFileAsync(update.Asset, download, cancellationToken).ConfigureAwait(false);
            await VerifyAsync(update.Asset, download, cancellationToken).ConfigureAwait(false);
            await ExtractAsync(download, newExecutable, cancellationToken).ConfigureAwait(false);
            MakeExecutable(executablePath, newExecutable);
            return newExecutable;
        }
        catch (Exception e) when (e is HttpRequestException or IOException or UnauthorizedAccessException
                                      or InvalidDataException
                                  || (e is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            TryDelete(newExecutable);
            throw new UpdateException(e.Message, e);
        }
        finally
        {
            TryDelete(download);
        }
    }

    private async Task DownloadFileAsync(GitHubAsset asset, string path, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var target = File.Create(path);
        await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Checks size and SHA-256 checksum of the download (if GitHub provides one).</summary>
    private static async Task VerifyAsync(GitHubAsset asset, string path, CancellationToken cancellationToken)
    {
        var size = new FileInfo(path).Length;
        if (asset.Size > 0 && size != asset.Size)
        {
            throw new InvalidDataException($"The download is incomplete ({size} of {asset.Size} bytes).");
        }

        const string prefix = "sha256:";
        if (asset.Digest is { } digest && digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            await using var stream = File.OpenRead(path);
            var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
            if (!string.Equals(hash, digest[prefix.Length..], StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The checksum of the download does not match.");
            }
        }
    }

    /// <summary>Extracts the executable from the release file (zip on Windows, tar.gz otherwise).</summary>
    private async Task ExtractAsync(string archive, string target, CancellationToken cancellationToken)
    {
        if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = ZipFile.OpenRead(archive);
            var entry = zip.Entries.FirstOrDefault(e => e.FullName == executableName)
                        ?? throw new InvalidDataException($"The download does not contain {executableName}.");
            entry.ExtractToFile(target, overwrite: true);
            return;
        }

        await using var file = File.OpenRead(archive);
        await using var gzip = new GZipStream(file, CompressionMode.Decompress);
        await using var tar = new TarReader(gzip);
        while (await tar.GetNextEntryAsync(cancellationToken: cancellationToken).ConfigureAwait(false) is { } entry)
        {
            if (entry.Name.TrimStart('.', '/') == executableName && entry.DataStream is not null)
            {
                await using var output = File.Create(target);
                await entry.DataStream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        throw new InvalidDataException($"The download does not contain {executableName}.");
    }

    /// <summary>The new executable gets the permissions of the current one, at least executable for the user.</summary>
    private static void MakeExecutable(string from, string to)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(to, File.Exists(from)
                ? File.GetUnixFileMode(from) | UnixFileMode.UserExecute
                : (UnixFileMode)Convert.ToInt32("755", 8));
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>1.2 and 1.2.0 are the same version.</summary>
    private static Version Normalize(Version version) =>
        new(version.Major, version.Minor, Math.Max(version.Build, 0));
}
