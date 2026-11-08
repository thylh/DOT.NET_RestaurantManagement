using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RestaurantManagement.WPF.Behaviors;

public static class ReportBarAnimationBehavior
{
    public static readonly DependencyProperty TargetScaleProperty =
        DependencyProperty.RegisterAttached(
            "TargetScale",
            typeof(double),
            typeof(ReportBarAnimationBehavior),
            new PropertyMetadata(double.NaN, OnTargetScaleChanged));

    private static readonly TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(700);
    public static double GetTargetScale(DependencyObject element) =>
        (double)element.GetValue(TargetScaleProperty);

    public static void SetTargetScale(DependencyObject element, double value) =>
        element.SetValue(TargetScaleProperty, value);

    private static void OnTargetScaleChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        var target = (double)e.NewValue;
        if (double.IsFinite(target))
        {
            element.Loaded -= Element_Loaded;
            element.Unloaded -= Element_Unloaded;
            element.IsVisibleChanged -= Element_IsVisibleChanged;
            element.Loaded += Element_Loaded;
            element.Unloaded += Element_Unloaded;
            element.IsVisibleChanged += Element_IsVisibleChanged;
            if (element.IsLoaded && element.IsVisible)
            {
                Animate(element, target, fromZero: false);
            }
        }
        else
        {
            element.Loaded -= Element_Loaded;
            element.Unloaded -= Element_Unloaded;
            element.IsVisibleChanged -= Element_IsVisibleChanged;
        }
    }

    private static void Element_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            Animate(element, GetTargetScale(element), fromZero: true);
        }
    }

    private static void Element_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            Reset(element);
        }
    }

    private static void Element_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        if (element.IsVisible)
        {
            Animate(element, GetTargetScale(element), fromZero: true);
        }
        else
        {
            Reset(element);
        }
    }

    private static void Reset(FrameworkElement element)
    {
        if (FindScaleTransform(element.RenderTransform) is { } scaleTransform)
        {
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scaleTransform.ScaleX = 0;
        }
    }

    private static void Animate(FrameworkElement element, double target, bool fromZero)
    {
        var scaleTransform = GetScaleTransform(element);
        var current = fromZero ? 0 : scaleTransform.ScaleX;
        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        if (!SystemParameters.ClientAreaAnimation)
        {
            scaleTransform.ScaleX = target;
            return;
        }

        scaleTransform.ScaleX = current;
        scaleTransform.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(current, target, AnimationDuration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            },
            HandoffBehavior.SnapshotAndReplace);
    }

    private static ScaleTransform GetScaleTransform(FrameworkElement element)
    {
        if (FindScaleTransform(element.RenderTransform) is { } existing)
        {
            return existing;
        }

        var transform = new ScaleTransform(0, 1);
        if (element.RenderTransform.Value.IsIdentity)
        {
            element.RenderTransform = transform;
        }
        else if (element.RenderTransform is TransformGroup group)
        {
            group.Children.Add(transform);
        }
        else
        {
            element.RenderTransform = new TransformGroup
            {
                Children = { element.RenderTransform, transform }
            };
        }

        return transform;
    }

    private static ScaleTransform? FindScaleTransform(Transform transform) =>
        transform as ScaleTransform
        ?? (transform as TransformGroup)?.Children
            .OfType<ScaleTransform>()
            .FirstOrDefault();
}
