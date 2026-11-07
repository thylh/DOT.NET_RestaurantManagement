using System.Security;
using System.Windows;
using System.Windows.Controls;

namespace RestaurantManagement.WPF.Behaviors;

public static class PasswordBoxPlaceholderBehavior
{
    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.RegisterAttached(
            "PlaceholderText",
            typeof(string),
            typeof(PasswordBoxPlaceholderBehavior),
            new PropertyMetadata(string.Empty, OnPlaceholderTextChanged));

    public static readonly DependencyProperty HasPasswordProperty =
        DependencyProperty.RegisterAttached(
            "HasPassword",
            typeof(bool),
            typeof(PasswordBoxPlaceholderBehavior),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsPlaceholderVisibleProperty =
        DependencyProperty.RegisterAttached(
            "IsPlaceholderVisible",
            typeof(bool),
            typeof(PasswordBoxPlaceholderBehavior),
            new PropertyMetadata(false));

    public static void SetPlaceholderText(DependencyObject element, string value) =>
        element.SetValue(PlaceholderTextProperty, value);

    public static string GetPlaceholderText(DependencyObject element) =>
        (string)element.GetValue(PlaceholderTextProperty);

    public static void SetHasPassword(DependencyObject element, bool value) =>
        element.SetValue(HasPasswordProperty, value);

    public static bool GetHasPassword(DependencyObject element) =>
        (bool)element.GetValue(HasPasswordProperty);

    public static void SetIsPlaceholderVisible(DependencyObject element, bool value) =>
        element.SetValue(IsPlaceholderVisibleProperty, value);

    public static bool GetIsPlaceholderVisible(DependencyObject element) =>
        (bool)element.GetValue(IsPlaceholderVisibleProperty);

    private static void OnPlaceholderTextChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not PasswordBox passwordBox)
        {
            return;
        }

        passwordBox.Loaded -= PasswordBox_Loaded;
        passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;
        passwordBox.GotKeyboardFocus -= PasswordBox_StateChanged;
        passwordBox.LostKeyboardFocus -= PasswordBox_StateChanged;

        if (!string.IsNullOrEmpty((string?)e.NewValue))
        {
            passwordBox.Loaded += PasswordBox_Loaded;
            passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
            passwordBox.GotKeyboardFocus += PasswordBox_StateChanged;
            passwordBox.LostKeyboardFocus += PasswordBox_StateChanged;
            UpdatePlaceholderState(passwordBox);
        }
        else
        {
            SetHasPassword(passwordBox, false);
            SetIsPlaceholderVisible(passwordBox, false);
        }
    }

    private static void PasswordBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox
            && !string.IsNullOrEmpty(GetPlaceholderText(passwordBox)))
        {
            passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;
            passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
            UpdatePlaceholderState(passwordBox);
        }
    }

    private static void PasswordBox_StateChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            UpdatePlaceholderVisibility(passwordBox);
        }
    }

    private static void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            UpdatePlaceholderState(passwordBox);
        }
    }

    private static void UpdateHasPassword(PasswordBox passwordBox)
    {
        using SecureString password = passwordBox.SecurePassword;
        SetHasPassword(passwordBox, password.Length > 0);
    }

    private static void UpdatePlaceholderState(PasswordBox passwordBox)
    {
        UpdateHasPassword(passwordBox);
        UpdatePlaceholderVisibility(passwordBox);
    }

    private static void UpdatePlaceholderVisibility(PasswordBox passwordBox) =>
        SetIsPlaceholderVisible(
            passwordBox,
            !GetHasPassword(passwordBox)
            && !passwordBox.IsKeyboardFocused
            && !string.IsNullOrEmpty(GetPlaceholderText(passwordBox)));
}

public static class ComboBoxPlaceholderBehavior
{
    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.RegisterAttached(
            "PlaceholderText",
            typeof(string),
            typeof(ComboBoxPlaceholderBehavior),
            new PropertyMetadata(string.Empty));

    public static void SetPlaceholderText(DependencyObject element, string value) =>
        element.SetValue(PlaceholderTextProperty, value);

    public static string GetPlaceholderText(DependencyObject element) =>
        (string)element.GetValue(PlaceholderTextProperty);
}
