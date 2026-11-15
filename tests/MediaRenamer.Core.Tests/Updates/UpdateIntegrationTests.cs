using MediaRenamer.Core.Updates;

namespace MediaRenamer.Core.Tests.Updates;

/// <summary>
/// Tests against the real GitHub releases. They need network access, so they only run on request:
/// <c>dotnet test -- --explicit only</c>.
/// </summary>
public sealed class UpdateIntegrationTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact(Explicit = true)]
    public async Task FindsAndInstallsTheLatestRelease()
    {
        var service = new UpdateService(new HttpClient(), UpdateService.AssetNameFor("linux-x64"),
            UpdateService.ExecutableNameFor("linux-x64"));

        var update = await service.CheckAsync(new Version(0, 0, 1), TestContext.Current.CancellationToken);

        Assert.NotNull(update);
        Assert.Equal("MediaRenamer-linux-x64.tar.gz", update.Asset.Name);
        Assert.StartsWith("sha256:", update.Asset.Digest, StringComparison.Ordinal);

        var executable = _temp.File("app", "MediaRenamer");
        await service.InstallAsync(update, executable, TestContext.Current.CancellationToken);

        // the installed file is the Linux executable (ELF) of the release
        var header = new byte[4];
        await using (var file = File.OpenRead(executable))
        {
            await file.ReadExactlyAsync(header, TestContext.Current.CancellationToken);
        }

        Assert.Equal([0x7F, (byte)'E', (byte)'L', (byte)'F'], header);
        Assert.Equal(["MediaRenamer"], _temp.Entries("app"));
    }
}
