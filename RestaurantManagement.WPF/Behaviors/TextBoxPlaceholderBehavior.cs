using System.Windows;
using System.Windows.Controls;

namespace RestaurantManagement.WPF.Behaviors;

public static class TextBoxPlaceholderBehavior
{
    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.RegisterAttached(
            "PlaceholderText",
            typeof(string),
            typeof(TextBoxPlaceholderBehavior),
            new PropertyMetadata(string.Empty, OnPlaceholderTextChanged));

    public static readonly DependencyProperty IsPlaceholderVisibleProperty =
        DependencyProperty.RegisterAttached(
            "IsPlaceholderVisible",
            typeof(bool),
            typeof(TextBoxPlaceholderBehavior),
            new PropertyMetadata(false));

    public static void SetPlaceholderText(DependencyObject element, string value) =>
        element.SetValue(PlaceholderTextProperty, value);

    public static string GetPlaceholderText(DependencyObject element) =>
        (string)element.GetValue(PlaceholderTextProperty);

    public static void SetIsPlaceholderVisible(DependencyObject element, bool value) =>
        element.SetValue(IsPlaceholderVisibleProperty, value);

    public static bool GetIsPlaceholderVisible(DependencyObject element) =>
        (bool)element.GetValue(IsPlaceholderVisibleProperty);

    private static void OnPlaceholderTextChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not TextBox textBox)
        {
            return;
        }

        textBox.TextChanged -= TextBox_StateChanged;
        textBox.GotKeyboardFocus -= TextBox_StateChanged;
        textBox.LostKeyboardFocus -= TextBox_StateChanged;

        if (!string.IsNullOrEmpty((string?)e.NewValue))
        {
            textBox.TextChanged += TextBox_StateChanged;
            textBox.GotKeyboardFocus += TextBox_StateChanged;
            textBox.LostKeyboardFocus += TextBox_StateChanged;
            UpdatePlaceholderVisibility(textBox);
        }
        else
        {
            SetIsPlaceholderVisible(textBox, false);
        }
    }

    private static void TextBox_StateChanged(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            UpdatePlaceholderVisibility(textBox);
        }
    }

    private static void UpdatePlaceholderVisibility(TextBox textBox) =>
        SetIsPlaceholderVisible(
            textBox,
            string.IsNullOrEmpty(textBox.Text)
            && !textBox.IsKeyboardFocused
            && !string.IsNullOrEmpty(GetPlaceholderText(textBox)));
}
