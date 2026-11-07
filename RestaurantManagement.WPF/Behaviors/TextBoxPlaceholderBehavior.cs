using System.Windows;

namespace RestaurantManagement.WPF.Behaviors;

public static class TextBoxPlaceholderBehavior
{
    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.RegisterAttached(
            "PlaceholderText",
            typeof(string),
            typeof(TextBoxPlaceholderBehavior),
            new PropertyMetadata(string.Empty));

    public static void SetPlaceholderText(DependencyObject element, string value) =>
        element.SetValue(PlaceholderTextProperty, value);

    public static string GetPlaceholderText(DependencyObject element) =>
        (string)element.GetValue(PlaceholderTextProperty);
}
