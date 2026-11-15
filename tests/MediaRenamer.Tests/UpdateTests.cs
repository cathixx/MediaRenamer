using MediaRenamer.Core.Tmdb;
using MediaRenamer.Core.Updates;
using MediaRenamer.ViewModels;

namespace MediaRenamer.Tests;

public class UpdateTests
{
    private const string Executable = "/apps/MediaRenamer";

    private static readonly AvailableUpdate Update = new(new Version(1, 2, 0),
        "https://github.com/cathixx/MediaRenamer/releases/tag/v1.2.0",
        new GitHubAsset("MediaRenamer-linux-x64.tar.gz", 10, null, "https://example.invalid/download"));

    private readonly FakeDialogs _dialogs = new();
    private readonly FakeUpdates _updates = new() { Update = Update };

    private MainWindowViewModel CreateViewModel() =>
        new(new TmdbService(new FakeTmdbClient(), ["en-US"]), _dialogs, "en-US", _updates);

    [Fact]
    public async Task NewerVersion_Confirmed_InstallsAndRequestsRestart()
    {
        _dialogs.Confirm = true;
        var vm = CreateViewModel();

        var restart = await vm.CheckForUpdateAsync(new Version(1, 1, 0), Executable);

        Assert.True(restart);
        var question = Assert.Single(_dialogs.Questions);
        Assert.Contains("1.2.0", question, StringComparison.Ordinal);
        Assert.Contains("1.1.0", question, StringComparison.Ordinal);
        Assert.Equal([(Update, Executable)], _updates.Installed);
        Assert.False(vm.IsUpdating);
        Assert.Null(vm.UpdateMessage);
        Assert.Empty(_dialogs.Errors);
    }

    [Fact]
    public async Task NewerVersion_Declined_NothingHappens()
    {
        _dialogs.Confirm = false;

        var restart = await CreateViewModel().CheckForUpdateAsync(new Version(1, 1, 0), Executable);

        Assert.False(restart);
        Assert.Single(_dialogs.Questions);
        Assert.Empty(_updates.Installed);
    }

    [Fact]
    public async Task NoNewerVersion_NoQuestion()
    {
        var restart = await CreateViewModel().CheckForUpdateAsync(new Version(1, 2, 0), Executable);

        Assert.False(restart);
        Assert.Empty(_dialogs.Questions);
    }

    [Fact]
    public async Task LocalBuild_NeverChecks()
    {
        var restart = await CreateViewModel().CheckForUpdateAsync(currentVersion: null, Executable);

        Assert.False(restart);
        Assert.Equal(0, _updates.Checks);
    }

    [Fact]
    public async Task CheckFails_Silently()
    {
        _updates.FailCheck = true;

        var restart = await CreateViewModel().CheckForUpdateAsync(new Version(1, 1, 0), Executable);

        Assert.False(restart);
        Assert.Empty(_dialogs.Questions);
        Assert.Empty(_dialogs.Errors);
    }

    [Fact]
    public async Task UnexpectedErrorWhileInstalling_ShowsDetailsAndContinues()
    {
        _dialogs.Confirm = true;
        _updates.InstallException = new InvalidOperationException("");
        var vm = CreateViewModel();

        var restart = await vm.CheckForUpdateAsync(new Version(1, 1, 0), Executable);

        Assert.False(restart);
        var error = Assert.Single(_dialogs.Errors);
        Assert.Contains(nameof(InvalidOperationException), error.Text, StringComparison.Ordinal);
        Assert.False(vm.IsUpdating);
    }

    [Fact]
    public void ErrorDetails_ContainTypeEvenWithoutMessage()
    {
        Assert.Contains("System.InvalidOperationException", MainWindowViewModel.ErrorDetails(new InvalidOperationException("")),
            StringComparison.Ordinal);
        Assert.StartsWith("broken", MainWindowViewModel.ErrorDetails(new InvalidOperationException("broken")),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task InstallFails_ShowsErrorWithReleasePageAndContinues()
    {
        _dialogs.Confirm = true;
        _updates.FailInstall = true;
        var vm = CreateViewModel();

        var restart = await vm.CheckForUpdateAsync(new Version(1, 1, 0), Executable);

        Assert.False(restart);
        var error = Assert.Single(_dialogs.Errors);
        Assert.Contains("no permission", error.Text, StringComparison.Ordinal);
        Assert.Contains(Update.ReleaseUrl, error.Text, StringComparison.Ordinal);
        Assert.False(vm.IsUpdating);
    }
}
