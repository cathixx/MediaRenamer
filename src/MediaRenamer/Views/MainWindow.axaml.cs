using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MediaRenamer.Resources;
using MediaRenamer.ViewModels;

namespace MediaRenamer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = $"Media Renamer   © cathixx   {AppVersion}";
    }

    /// <summary>Version of the application, e.g. <c>1.2.3</c> (without the commit hash).</summary>
    public static string AppVersion
    {
        get
        {
            var version = typeof(MainWindow).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "snapshot";
            var plus = version.IndexOf('+', StringComparison.Ordinal);
            return plus >= 0 ? version[..plus] : version;
        }
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private async void OnChooseFolder(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Strings.ChooseFolderTitle,
            AllowMultiple = false,
        });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            await viewModel.OpenAsync([path]);
        }
    }
}
