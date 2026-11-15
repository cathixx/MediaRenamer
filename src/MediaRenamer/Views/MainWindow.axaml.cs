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
        VersionText.Text = $"© cathixx   {AppInfo.DisplayVersion}";
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private async void OnChooseFolder(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        // opens in the current show folder, otherwise e.g. after "Send to" somewhere else (E-8)
        var start = viewModel.FolderSelectionStart is { } startPath
            ? await StorageProvider.TryGetFolderFromPathAsync(startPath)
            : null;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Strings.ChooseFolderTitle,
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            await viewModel.OpenAsync([path]);
        }
    }
}
