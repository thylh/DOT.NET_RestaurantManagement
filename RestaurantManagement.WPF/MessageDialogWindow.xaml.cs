using System.Windows;
using RestaurantManagement.WPF.Behaviors;

namespace RestaurantManagement.WPF;

public partial class MessageDialogWindow : Window
{
    private readonly bool _isConfirmation;
    private readonly bool _isInput;

    public MessageDialogWindow(
        string title,
        string message,
        bool isConfirmation,
        MessageDialogKind kind,
        string confirmationText = "Xác nhận",
        bool isInput = false)
    {
        InitializeComponent();
        _isConfirmation = isConfirmation;
        _isInput = isInput;
        DataContext = new MessageDialogContent(title, message);
        ConfirmationButtons.Visibility = isConfirmation || isInput ? Visibility.Visible : Visibility.Collapsed;
        InputPanel.Visibility = isInput ? Visibility.Visible : Visibility.Collapsed;
        OkButton.Visibility = isConfirmation || isInput ? Visibility.Collapsed : Visibility.Visible;
        ConfirmButton.Content = confirmationText;
        if (isInput)
        {
            Loaded += (_, _) => DialogInputTextBox.Focus();
        }

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

    public static string? ShowInput(
        Window? owner,
        string title,
        string message,
        string placeholder)
    {
        var dialog = new MessageDialogWindow(
            title,
            message,
            false,
            MessageDialogKind.Warning,
            "Hủy order",
            true);
        TextBoxPlaceholderBehavior.SetPlaceholderText(dialog.DialogInputTextBox, placeholder);
        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        return dialog.ShowDialog() == true
            ? dialog.DialogInputTextBox.Text.Trim()
            : null;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (_isInput && string.IsNullOrWhiteSpace(DialogInputTextBox.Text))
        {
            InputValidationMessage.Text = "Vui lòng nhập lý do hủy order.";
            InputValidationMessage.Visibility = Visibility.Visible;
            DialogInputTextBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void DialogInputTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(DialogInputTextBox.Text))
        {
            InputValidationMessage.Visibility = Visibility.Collapsed;
        }
    }

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
