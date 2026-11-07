using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RestaurantManagement.WPF;

public partial class NotificationBanner : UserControl
{
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(
            nameof(Message),
            typeof(string),
            typeof(NotificationBanner),
            new PropertyMetadata(string.Empty, OnMessageChanged));

    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(
            nameof(Kind),
            typeof(NotificationKind),
            typeof(NotificationBanner),
            new PropertyMetadata(NotificationKind.Success, OnKindChanged));

    public static readonly DependencyProperty IsSuppressedProperty =
        DependencyProperty.Register(
            nameof(IsSuppressed),
            typeof(bool),
            typeof(NotificationBanner),
            new PropertyMetadata(false, OnVisibilityInputsChanged));

    public static readonly DependencyProperty UseOpenBottomFrameProperty =
        DependencyProperty.Register(
            nameof(UseOpenBottomFrame),
            typeof(bool),
            typeof(NotificationBanner),
            new PropertyMetadata(false, OnFrameStyleChanged));

    public NotificationBanner()
    {
        InitializeComponent();
        UpdatePresentation();
        UpdateVisibility(this);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public NotificationKind Kind
    {
        get => (NotificationKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public bool IsSuppressed
    {
        get => (bool)GetValue(IsSuppressedProperty);
        set => SetValue(IsSuppressedProperty, value);
    }

    public bool UseOpenBottomFrame
    {
        get => (bool)GetValue(UseOpenBottomFrameProperty);
        set => SetValue(UseOpenBottomFrameProperty, value);
    }

    private static void OnMessageChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        UpdateVisibility((NotificationBanner)dependencyObject);
    }

    private static void OnKindChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e) =>
        ((NotificationBanner)dependencyObject).UpdatePresentation();

    private static void OnVisibilityInputsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e) =>
        UpdateVisibility((NotificationBanner)dependencyObject);

    private static void OnFrameStyleChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var banner = (NotificationBanner)dependencyObject;
        if (banner.BannerBorder is null)
        {
            return;
        }

        var useOpenBottomFrame = (bool)e.NewValue;
        banner.BannerBorder.BorderThickness = useOpenBottomFrame
            ? new Thickness(1, 1, 1, 0)
            : new Thickness(1);
        banner.BannerBorder.CornerRadius = useOpenBottomFrame
            ? new CornerRadius(14, 14, 0, 0)
            : new CornerRadius(10);
        Behaviors.OpenBottomFrameBehavior.SetIsEnabled(banner.BannerBorder, useOpenBottomFrame);
    }

    private static void UpdateVisibility(NotificationBanner banner) =>
        banner.Visibility = !banner.IsSuppressed && !string.IsNullOrWhiteSpace(banner.Message)
            ? Visibility.Visible
            : Visibility.Collapsed;

    private void UpdatePresentation()
    {
        if (BannerBorder is null)
        {
            return;
        }

        var (backgroundKey, foregroundKey, glyph) = Kind switch
        {
            NotificationKind.Information => ("PrimaryTintBrush", "PrimaryBrush", "i"),
            NotificationKind.Warning => ("WarningTintBrush", "WarningBrush", "!"),
            NotificationKind.Error => ("DangerTintBrush", "DangerBrush", "×"),
            _ => ("SuccessTintBrush", "SuccessBrush", "✓")
        };

        BannerBorder.Background = (Brush)FindResource(backgroundKey);
        KindBadge.Background = (Brush)FindResource(backgroundKey);
        KindGlyph.Foreground = (Brush)FindResource(foregroundKey);
        KindGlyph.Text = glyph;
    }
}

public enum NotificationKind
{
    Success,
    Information,
    Warning,
    Error
}
