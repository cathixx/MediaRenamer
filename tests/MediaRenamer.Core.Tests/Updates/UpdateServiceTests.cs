using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using MediaRenamer.Core.Updates;

namespace MediaRenamer.Core.Tests.Updates;

public sealed class UpdateServiceTests : IDisposable
{
    private const string ReleaseUrl = "https://github.com/cathixx/MediaRenamer/releases/tag/v1.2.0";
    private const string DownloadUrl = "https://github.com/download/asset";

    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    /// <summary>Answers the release request with JSON and the download request with the archive.</summary>
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static string ReleaseJson(string tag, params (string Name, long Size, string? Digest)[] assets) =>
        $$"""
        {"tag_name":"{{tag}}","html_url":"{{ReleaseUrl}}","assets":[{{string.Join(",", assets.Select(a =>
            $$"""{"name":"{{a.Name}}","size":{{a.Size}},"digest":{{(a.Digest is null ? "null" : $"\"{a.Digest}\"")}},"browser_download_url":"{{DownloadUrl}}"}"""))}}]}
        """;

    private static UpdateService Service(Func<HttpRequestMessage, HttpResponseMessage> respond, string runtime,
        out FakeHandler handler)
    {
        handler = new FakeHandler(respond);
        return new UpdateService(new HttpClient(handler), UpdateService.AssetNameFor(runtime),
            UpdateService.ExecutableNameFor(runtime));
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Bytes(byte[] data) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };

    private static byte[] Zip(string entryName, string content)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(zip.CreateEntry(entryName).Open());
            writer.Write(content);
        }

        return stream.ToArray();
    }

    private static byte[] TarGz(string entryName, string content)
    {
        using var stream = new MemoryStream();
        using (var gzip = new GZipStream(stream, CompressionLevel.Fastest, leaveOpen: true))
        using (var tar = new TarWriter(gzip))
        {
            var entry = new PaxTarEntry(TarEntryType.RegularFile, entryName)
            {
                DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content)),
            };
            tar.WriteEntry(entry);
        }

        return stream.ToArray();
    }

    private static string Sha256(byte[] data) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(data));

    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3+abc123", "1.2.3")]
    [InlineData("v1.0", "1.0.0")]
    [InlineData("0.0.0-snapshot", null)]
    [InlineData("snapshot", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ParseVersion(string? text, string? expected)
    {
        Assert.Equal(expected is null ? null : Version.Parse(expected), UpdateService.ParseVersion(text));
    }

    [Theory]
    [InlineData("win-x64", "MediaRenamer-win-x64.zip", "MediaRenamer.exe")]
    [InlineData("linux-x64", "MediaRenamer-linux-x64.tar.gz", "MediaRenamer")]
    [InlineData("osx-arm64", "MediaRenamer-osx-arm64.tar.gz", "MediaRenamer")]
    public void AssetAndExecutableNames(string runtime, string asset, string executable)
    {
        Assert.Equal(asset, UpdateService.AssetNameFor(runtime));
        Assert.Equal(executable, UpdateService.ExecutableNameFor(runtime));
    }

    [Fact]
    public void CurrentRuntime_IsOneOfTheReleasedPlatforms()
    {
        Assert.Contains(UpdateService.CurrentRuntime(), new[] { "win-x64", "linux-x64", "osx-x64", "osx-arm64" });
    }

    [Fact]
    public async Task Check_NewerRelease()
    {
        var service = Service(_ => Json(ReleaseJson("v1.2.0",
            ("MediaRenamer-win-x64.zip", 10, null), ("MediaRenamer-linux-x64.tar.gz", 20, "sha256:ab"))), "linux-x64",
            out var handler);

        var update = await service.CheckAsync(new Version(1, 1, 5), TestContext.Current.CancellationToken);

        Assert.NotNull(update);
        Assert.Equal(new Version(1, 2, 0), update.Version);
        Assert.Equal(ReleaseUrl, update.ReleaseUrl);
        Assert.Equal("MediaRenamer-linux-x64.tar.gz", update.Asset.Name);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.github.com/repos/cathixx/MediaRenamer/releases/latest", request.RequestUri!.ToString());
        Assert.NotEmpty(request.Headers.UserAgent);
    }

    [Theory]
    [InlineData("v1.2.0", "1.2.0")] // same version
    [InlineData("v1.2.0", "1.3.0")] // older release
    [InlineData("v1.2.0", "1.2")]
    [InlineData("nightly", "1.0.0")] // no version
    public async Task Check_NoUpdate(string tag, string current)
    {
        var service = Service(_ => Json(ReleaseJson(tag, ("MediaRenamer-win-x64.zip", 10, null))), "win-x64", out _);

        Assert.Null(await service.CheckAsync(Version.Parse(current), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Check_NoFileForThePlatform()
    {
        var service = Service(_ => Json(ReleaseJson("v2.0.0", ("MediaRenamer-win-x64.zip", 10, null))), "osx-arm64",
            out _);

        Assert.Null(await service.CheckAsync(new Version(1, 0, 0), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Check_Error()
    {
        var service = Service(_ => new HttpResponseMessage(HttpStatusCode.Forbidden), "win-x64", out _);

        await Assert.ThrowsAsync<UpdateException>(
            () => service.CheckAsync(new Version(1, 0, 0), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Download_Windows_PutsTheNewExecutableNextToTheRunningOne()
    {
        var executable = _temp.File("app", "MediaRenamer.exe");
        File.WriteAllText(executable, "old version");
        var zip = Zip("MediaRenamer.exe", "new version");
        var service = Service(_ => Bytes(zip), "win-x64", out var handler);
        var update = new AvailableUpdate(new Version(1, 2, 0), ReleaseUrl,
            new GitHubAsset("MediaRenamer-win-x64.zip", zip.Length, Sha256(zip), DownloadUrl));

        var newExecutable = await service.DownloadAsync(update, executable, TestContext.Current.CancellationToken);

        // the running executable is not touched
        Assert.Equal(executable + ".new", newExecutable);
        Assert.Equal("old version", File.ReadAllText(executable));
        Assert.Equal("new version", File.ReadAllText(newExecutable));
        Assert.Equal(DownloadUrl, Assert.Single(handler.Requests).RequestUri!.ToString());
        Assert.Equal(["MediaRenamer.exe", "MediaRenamer.exe.new"], _temp.Entries("app"));
    }

    [Fact]
    public async Task Download_LinuxOrMac_TarGzAndExecutablePermission()
    {
        var executable = _temp.File("app", "MediaRenamer");
        File.WriteAllText(executable, "old version");
        var tarGz = TarGz("MediaRenamer", "new version");
        var service = Service(_ => Bytes(tarGz), "linux-x64", out _);
        var update = new AvailableUpdate(new Version(1, 2, 0), ReleaseUrl,
            new GitHubAsset("MediaRenamer-linux-x64.tar.gz", tarGz.Length, Sha256(tarGz), DownloadUrl));

        var newExecutable = await service.DownloadAsync(update, executable, TestContext.Current.CancellationToken);

        Assert.Equal("new version", File.ReadAllText(newExecutable));
        Assert.Equal(["MediaRenamer", "MediaRenamer.new"], _temp.Entries("app"));
        if (!OperatingSystem.IsWindows())
        {
            Assert.True(File.GetUnixFileMode(newExecutable).HasFlag(UnixFileMode.UserExecute));
        }
    }

    [Fact]
    public async Task Download_WrongChecksum_LeavesNothing()
    {
        var executable = _temp.File("app", "MediaRenamer.exe");
        File.WriteAllText(executable, "old version");
        var zip = Zip("MediaRenamer.exe", "manipulated");
        var service = Service(_ => Bytes(zip), "win-x64", out _);
        var update = new AvailableUpdate(new Version(1, 2, 0), ReleaseUrl,
            new GitHubAsset("MediaRenamer-win-x64.zip", zip.Length, Sha256([1, 2, 3]), DownloadUrl));

        await Assert.ThrowsAsync<UpdateException>(
            () => service.DownloadAsync(update, executable, TestContext.Current.CancellationToken));

        Assert.Equal("old version", File.ReadAllText(executable));
        Assert.Equal(["MediaRenamer.exe"], _temp.Entries("app"));
    }

    [Fact]
    public async Task Download_Incomplete_LeavesNothing()
    {
        var executable = _temp.File("app", "MediaRenamer.exe");
        var zip = Zip("MediaRenamer.exe", "new version");
        var service = Service(_ => Bytes(zip), "win-x64", out _);
        var update = new AvailableUpdate(new Version(1, 2, 0), ReleaseUrl,
            new GitHubAsset("MediaRenamer-win-x64.zip", zip.Length + 100, null, DownloadUrl));

        await Assert.ThrowsAsync<UpdateException>(
            () => service.DownloadAsync(update, executable, TestContext.Current.CancellationToken));

        Assert.Equal(["MediaRenamer.exe"], _temp.Entries("app"));
    }

    [Fact]
    public async Task Download_ArchiveWithoutExecutable_LeavesNothing()
    {
        var executable = _temp.File("app", "MediaRenamer.exe");
        var zip = Zip("other.exe", "new version");
        var service = Service(_ => Bytes(zip), "win-x64", out _);
        var update = new AvailableUpdate(new Version(1, 2, 0), ReleaseUrl,
            new GitHubAsset("MediaRenamer-win-x64.zip", zip.Length, Sha256(zip), DownloadUrl));

        var e = await Assert.ThrowsAsync<UpdateException>(
            () => service.DownloadAsync(update, executable, TestContext.Current.CancellationToken));

        Assert.Contains("MediaRenamer.exe", e.Message, StringComparison.Ordinal);
        Assert.Equal(["MediaRenamer.exe"], _temp.Entries("app"));
    }

    [Fact]
    public async Task Download_Fails_LeavesNothing()
    {
        var executable = _temp.File("app", "MediaRenamer.exe");
        var service = Service(_ => new HttpResponseMessage(HttpStatusCode.NotFound), "win-x64", out _);
        var update = new AvailableUpdate(new Version(1, 2, 0), ReleaseUrl,
            new GitHubAsset("MediaRenamer-win-x64.zip", 10, null, DownloadUrl));

        await Assert.ThrowsAsync<UpdateException>(
            () => service.DownloadAsync(update, executable, TestContext.Current.CancellationToken));

        Assert.Equal(["MediaRenamer.exe"], _temp.Entries("app"));
    }

    [Fact]
    public void CompleteUpdate_CopiesTheNewExecutableOverTheTarget()
    {
        var target = _temp.File("app", "MediaRenamer.exe");
        File.WriteAllText(target, "old version");
        var newExecutable = _temp.File("app", "MediaRenamer.exe.new");
        File.WriteAllText(newExecutable, "new version");

        UpdateService.CompleteUpdate(newExecutable, target, previousProcessId: int.MaxValue, TimeSpan.FromSeconds(5));

        Assert.Equal("new version", File.ReadAllText(target));
        Assert.True(File.Exists(newExecutable)); // removed by the new version at its start
    }

    [Fact]
    public async Task CompleteUpdate_RetriesWhileTheTargetIsLocked()
    {
        var target = _temp.File("app", "MediaRenamer.exe");
        var newExecutable = _temp.File("app", "MediaRenamer.exe.new");
        File.WriteAllText(newExecutable, "new version");

        var locked = new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var unlock = Task.Run(async () =>
        {
            await Task.Delay(700);
            await locked.DisposeAsync();
        }, TestContext.Current.CancellationToken);

        UpdateService.CompleteUpdate(newExecutable, target, previousProcessId: null, TimeSpan.FromSeconds(10));
        await unlock;

        Assert.Equal("new version", File.ReadAllText(target));
    }

    [Fact]
    public void CompleteUpdate_GivesUpAfterTheTimeout()
    {
        var target = _temp.File("app", "MediaRenamer.exe");
        var newExecutable = _temp.File("app", "MediaRenamer.exe.new");
        using var locked = new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.ThrowsAny<IOException>(() =>
            UpdateService.CompleteUpdate(newExecutable, target, previousProcessId: null, TimeSpan.FromMilliseconds(600)));
    }

    [Fact]
    public async Task CleanUp_RemovesTheFilesOfAnUpdate()
    {
        var executable = _temp.File("app", "MediaRenamer.exe");
        _temp.File("app", "MediaRenamer.exe.new");
        _temp.File("app", "MediaRenamer.exe.old");

        await UpdateService.CleanUpAsync(executable);

        Assert.Equal(["MediaRenamer.exe"], _temp.Entries("app"));
    }

    [Fact]
    public async Task CleanUp_WithoutFiles_DoesNothing()
    {
        var executable = _temp.File("app", "MediaRenamer.exe");

        await UpdateService.CleanUpAsync(executable);

        Assert.Equal(["MediaRenamer.exe"], _temp.Entries("app"));
    }

    [Fact]
    public void UpdateArguments_RoundTrip()
    {
        var arguments = UpdateArguments.Create("/apps/MediaRenamer", 1234, ["--lang=de", "/shows/Eureka"]);

        Assert.True(UpdateArguments.TryParse(arguments, out var target, out var process, out var original));
        Assert.Equal("/apps/MediaRenamer", target);
        Assert.Equal(1234, process);
        Assert.Equal(["--lang=de", "/shows/Eureka"], original);
    }

    [Fact]
    public void UpdateArguments_NormalStart()
    {
        Assert.False(UpdateArguments.TryParse(["/shows/Eureka"], out _, out var process, out var original));
        Assert.Null(process);
        Assert.Equal(["/shows/Eureka"], original);
    }
}
