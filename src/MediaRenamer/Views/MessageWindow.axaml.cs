using Avalonia.Controls;
using Avalonia.Interactivity;
using MediaRenamer.ViewModels;

namespace MediaRenamer.Views;

/// <summary>A simple error dialog with a header, a text and an OK button.</summary>
public partial class MessageWindow : Window
{
    public MessageWindow()
    {
        InitializeComponent();
    }

    public MessageWindow(string header, string text) : this()
    {
        HeaderText.Text = header;
        MessageText.Text = text;
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close();
}

/// <summary>Shows dialogs on top of the given window.</summary>
public sealed class DialogService(Window owner) : IDialogService
{
    public Task ShowErrorAsync(string header, string text) => new MessageWindow(header, text).ShowDialog(owner);
}
