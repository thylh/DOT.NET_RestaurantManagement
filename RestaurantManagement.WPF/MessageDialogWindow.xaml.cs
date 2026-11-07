using System.Windows;

namespace RestaurantManagement.WPF;

public partial class MessageDialogWindow : Window
{
    private readonly bool _isConfirmation;

    public MessageDialogWindow(
        string title,
        string message,
        bool isConfirmation,
        MessageDialogKind kind,
        string confirmationText = "Xác nhận")
    {
        InitializeComponent();
        _isConfirmation = isConfirmation;
        DataContext = new MessageDialogContent(title, message);
        ConfirmationButtons.Visibility = isConfirmation ? Visibility.Visible : Visibility.Collapsed;
        OkButton.Visibility = isConfirmation ? Visibility.Collapsed : Visibility.Visible;
        ConfirmButton.Content = confirmationText;

        var (backgroundKey, foregroundKey, glyph) = kind switch
        {
            MessageDialogKind.Warning => ("WarningTintBrush", "WarningBrush", "!"),
            MessageDialogKind.Error => ("DangerTintBrush", "DangerBrush", "×"),
            MessageDialogKind.Success => ("SuccessTintBrush", "SuccessBrush", "✓"),
            _ => ("PrimaryTintBrush", "PrimaryBrush", "i")
        };
        KindBadge.Background = (System.Windows.Media.Brush)FindResource(backgroundKey);
        KindGlyph.Text = glyph;
        KindGlyph.Foreground = (System.Windows.Media.Brush)FindResource(foregroundKey);
    }

    public static bool ShowConfirmation(
        Window? owner,
        string title,
        string message,
        string confirmationText = "Xác nhận")
    {
        var dialog = new MessageDialogWindow(
            title,
            message,
            true,
            MessageDialogKind.Warning,
            confirmationText);
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

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (_isConfirmation)
        {
            DialogResult = false;
        }
        else
        {
            Close();
        }
    }

    private sealed record MessageDialogContent(string Title, string Message);
}

public enum MessageDialogKind
{
    Success,
    Information,
    Warning,
    Error
}
