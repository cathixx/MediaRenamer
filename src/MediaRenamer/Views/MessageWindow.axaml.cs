using Avalonia.Controls;
using Avalonia.Interactivity;
using MediaRenamer.ViewModels;

namespace MediaRenamer.Views;

/// <summary>
/// A simple dialog with a header and a text: an error message with an OK button, or a question with two buttons.
/// The result of <see cref="Window.ShowDialog{TResult}"/> is whether the first button was chosen.
/// </summary>
public partial class MessageWindow : Window
{
    public MessageWindow()
    {
        InitializeComponent();
    }

    /// <summary>Error message with an OK button.</summary>
    public MessageWindow(string header, string text) : this()
    {
        HeaderText.Text = header;
        MessageText.Text = text;
    }

    /// <summary>Question with the buttons <paramref name="yes"/> and <paramref name="no"/>.</summary>
    public MessageWindow(string title, string header, string text, string yes, string no) : this(header, text)
    {
        Title = title;
        YesButton.Content = yes;
        NoButton.Content = no;
        NoButton.IsVisible = true;
    }

    private void OnYes(object? sender, RoutedEventArgs e) => Close(true);

    private void OnNo(object? sender, RoutedEventArgs e) => Close(false);
}

/// <summary>Shows dialogs on top of the given window.</summary>
public sealed class DialogService(Window owner) : IDialogService
{
    public Task ShowErrorAsync(string header, string text) => new MessageWindow(header, text).ShowDialog(owner);

    public async Task<bool> ConfirmAsync(string title, string header, string text, string yes, string no) =>
        await new MessageWindow(title, header, text, yes, no).ShowDialog<bool?>(owner) == true;
}
