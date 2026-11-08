using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace RestaurantManagement.WPF.Controls;

public sealed class AnimatedMetricTextBlock : TextBlock
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(AnimatedMetricTextBlock),
            new PropertyMetadata(double.NaN, OnValueChanged));

    public static readonly DependencyProperty FormatStringProperty =
        DependencyProperty.Register(
            nameof(FormatString),
            typeof(string),
            typeof(AnimatedMetricTextBlock),
            new PropertyMetadata("N0", OnDisplayFormatChanged));

    public static readonly DependencyProperty SuffixProperty =
        DependencyProperty.Register(
            nameof(Suffix),
            typeof(string),
            typeof(AnimatedMetricTextBlock),
            new PropertyMetadata(string.Empty, OnDisplayFormatChanged));

    private static readonly DependencyProperty AnimatedValueProperty =
        DependencyProperty.Register(
            nameof(AnimatedValue),
            typeof(double),
            typeof(AnimatedMetricTextBlock),
            new PropertyMetadata(0d, OnAnimatedValueChanged));

    private static readonly TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(850);

    public AnimatedMetricTextBlock()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string FormatString
    {
        get => (string)GetValue(FormatStringProperty);
        set => SetValue(FormatStringProperty, value);
    }

    public string Suffix
    {
        get => (string)GetValue(SuffixProperty);
        set => SetValue(SuffixProperty, value);
    }

    private double AnimatedValue
    {
        get => (double)GetValue(AnimatedValueProperty);
        set => SetValue(AnimatedValueProperty, value);
    }

    private static void OnValueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var metric = (AnimatedMetricTextBlock)dependencyObject;
        var target = e.NewValue is double numericValue ? numericValue : double.NaN;
        if (!double.IsFinite(target))
        {
            metric.BeginAnimation(AnimatedValueProperty, null);
            metric.Text = string.Empty;
            return;
        }

        if (metric.IsLoaded)
        {
            metric.AnimateTo(target);
        }
        else
        {
            metric.SetCurrentValue(AnimatedValueProperty, target);
        }
    }

    private static void OnDisplayFormatChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e) =>
        ((AnimatedMetricTextBlock)dependencyObject).UpdateText();

    private static void OnAnimatedValueChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e) =>
        ((AnimatedMetricTextBlock)dependencyObject).UpdateText();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (double.IsFinite(Value))
        {
            AnimateTo(Value, startFromZero: true);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        BeginAnimation(AnimatedValueProperty, null);
        if (double.IsFinite(Value))
        {
            SetCurrentValue(AnimatedValueProperty, Value);
        }
    }

    private void AnimateTo(double target, bool startFromZero = false)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            BeginAnimation(AnimatedValueProperty, null);
            SetCurrentValue(AnimatedValueProperty, target);
            return;
        }

        var current = startFromZero ? 0 : AnimatedValue;
        BeginAnimation(AnimatedValueProperty, null);
        SetCurrentValue(AnimatedValueProperty, current);
        BeginAnimation(
            AnimatedValueProperty,
            new DoubleAnimation(current, target, AnimationDuration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            },
            HandoffBehavior.SnapshotAndReplace);
    }

    private void UpdateText()
    {
        if (!double.IsFinite(Value))
        {
            Text = string.Empty;
            return;
        }

        Text = $"{AnimatedValue.ToString(FormatString, CultureInfo.CurrentCulture)}{Suffix}";
    }
}
