using System.Windows;

namespace RestaurantManagement.WPF;

public partial class MessageDialogWindow : Window
{
    public MessageDialogWindow(string title, string message, bool isConfirmation, MessageDialogKind kind)
    {
        InitializeComponent();
        DataContext = new MessageDialogContent(title, message);
        ConfirmationButtons.Visibility = isConfirmation ? Visibility.Visible : Visibility.Collapsed;
        OkButton.Visibility = isConfirmation ? Visibility.Collapsed : Visibility.Visible;

        if (kind == MessageDialogKind.Warning)
        {
            KindBadge.Background = (System.Windows.Media.Brush)FindResource("DangerTintBrush");
            KindGlyph.Text = "!";
            KindGlyph.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
        }
        else if (kind == MessageDialogKind.Error)
        {
            KindBadge.Background = (System.Windows.Media.Brush)FindResource("DangerTintBrush");
            KindGlyph.Text = "×";
            KindGlyph.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
        }
    }

    public static bool ShowConfirmation(Window? owner, string title, string message)
    {
        var dialog = new MessageDialogWindow(title, message, true, MessageDialogKind.Warning);
        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        return dialog.ShowDialog() == true;
    }

    public static void ShowMessage(
        Window? owner,
        string title,
        string message,
        MessageDialogKind kind = MessageDialogKind.Information)
    {
        var dialog = new MessageDialogWindow(title, message, false, kind);
        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        dialog.ShowDialog();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private sealed record MessageDialogContent(string Title, string Message);
}

public enum MessageDialogKind
{
    Information,
    Warning,
    Error
}
