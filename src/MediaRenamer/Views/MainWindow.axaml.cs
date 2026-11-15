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
        Title = $"Media Renamer   © cathixx   {AppInfo.DisplayVersion}";
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
