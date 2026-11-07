using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace RestaurantManagement.WPF.Behaviors;

public static class MotionTransitionBehavior
{
    private static readonly TimeSpan PageTransitionDuration = TimeSpan.FromMilliseconds(1000);
    private static readonly TimeSpan DecorationTransitionDuration = TimeSpan.FromMilliseconds(1100);

    private static readonly DependencyPropertyDescriptor? IsMouseOverDescriptor =
        DependencyPropertyDescriptor.FromProperty(UIElement.IsMouseOverProperty, typeof(Button));

    private static readonly DependencyPropertyDescriptor? IsPressedDescriptor =
        DependencyPropertyDescriptor.FromProperty(ButtonBase.IsPressedProperty, typeof(ButtonBase));

    private static readonly DependencyPropertyDescriptor? IsButtonEnabledDescriptor =
        DependencyPropertyDescriptor.FromProperty(UIElement.IsEnabledProperty, typeof(Button));

    private static readonly DependencyPropertyDescriptor? IsCheckedDescriptor =
        DependencyPropertyDescriptor.FromProperty(ToggleButton.IsCheckedProperty, typeof(ToggleButton));

    public static readonly DependencyProperty AnimateButtonInteractionProperty =
        DependencyProperty.RegisterAttached(
            "AnimateButtonInteraction",
            typeof(bool),
            typeof(MotionTransitionBehavior),
            new PropertyMetadata(false, OnAnimateButtonInteractionChanged));

    public static readonly DependencyProperty AnimateToggleStateProperty =
        DependencyProperty.RegisterAttached(
            "AnimateToggleState",
            typeof(bool),
            typeof(MotionTransitionBehavior),
            new PropertyMetadata(false, OnAnimateToggleStateChanged));

    public static readonly DependencyProperty AnimateOnVisibleProperty =
        DependencyProperty.RegisterAttached(
            "AnimateOnVisible",
            typeof(bool),
            typeof(MotionTransitionBehavior),
            new PropertyMetadata(false, OnAnimateOnVisibleChanged));

    public static readonly DependencyProperty FlyInOnVisibleProperty =
        DependencyProperty.RegisterAttached(
            "FlyInOnVisible",
            typeof(bool),
            typeof(MotionTransitionBehavior),
            new PropertyMetadata(false, OnFlyInOnVisibleChanged));

    public static readonly DependencyProperty FlyInFromXProperty =
        DependencyProperty.RegisterAttached(
            "FlyInFromX",
            typeof(double),
            typeof(MotionTransitionBehavior),
            new PropertyMetadata(0d));

    public static readonly DependencyProperty FlyInFromYProperty =
        DependencyProperty.RegisterAttached(
            "FlyInFromY",
            typeof(double),
            typeof(MotionTransitionBehavior),
            new PropertyMetadata(0d));

    public static readonly DependencyProperty FlyInDelayProperty =
        DependencyProperty.RegisterAttached(
            "FlyInDelay",
            typeof(double),
            typeof(MotionTransitionBehavior),
            new PropertyMetadata(0d));

    public static readonly DependencyProperty BreatheOnVisibleProperty =
        DependencyProperty.RegisterAttached(
            "BreatheOnVisible",
            typeof(bool),
            typeof(MotionTransitionBehavior),
            new PropertyMetadata(false, OnBreatheOnVisibleChanged));

    public static readonly DependencyProperty BreatheDelayProperty =
        DependencyProperty.RegisterAttached(
            "BreatheDelay",
            typeof(double),
            typeof(MotionTransitionBehavior),
            new PropertyMetadata(0d));

    public static readonly DependencyProperty AnimateTabSelectionProperty =
        DependencyProperty.RegisterAttached(
            "AnimateTabSelection",
            typeof(bool),
            typeof(MotionTransitionBehavior),
            new PropertyMetadata(false, OnAnimateTabSelectionChanged));

    public static bool GetAnimateButtonInteraction(DependencyObject element) =>
        (bool)element.GetValue(AnimateButtonInteractionProperty);

    public static void SetAnimateButtonInteraction(DependencyObject element, bool value) =>
        element.SetValue(AnimateButtonInteractionProperty, value);

    public static bool GetAnimateToggleState(DependencyObject element) =>
        (bool)element.GetValue(AnimateToggleStateProperty);

    public static void SetAnimateToggleState(DependencyObject element, bool value) =>
        element.SetValue(AnimateToggleStateProperty, value);

    public static bool GetAnimateOnVisible(DependencyObject element) =>
        (bool)element.GetValue(AnimateOnVisibleProperty);

    public static void SetAnimateOnVisible(DependencyObject element, bool value) =>
        element.SetValue(AnimateOnVisibleProperty, value);

    public static bool GetFlyInOnVisible(DependencyObject element) =>
        (bool)element.GetValue(FlyInOnVisibleProperty);

    public static void SetFlyInOnVisible(DependencyObject element, bool value) =>
        element.SetValue(FlyInOnVisibleProperty, value);

    public static double GetFlyInFromX(DependencyObject element) =>
        (double)element.GetValue(FlyInFromXProperty);

    public static void SetFlyInFromX(DependencyObject element, double value) =>
        element.SetValue(FlyInFromXProperty, value);

    public static double GetFlyInFromY(DependencyObject element) =>
        (double)element.GetValue(FlyInFromYProperty);

    public static void SetFlyInFromY(DependencyObject element, double value) =>
        element.SetValue(FlyInFromYProperty, value);

    public static double GetFlyInDelay(DependencyObject element) =>
        (double)element.GetValue(FlyInDelayProperty);

    public static void SetFlyInDelay(DependencyObject element, double value) =>
        element.SetValue(FlyInDelayProperty, value);

    public static bool GetBreatheOnVisible(DependencyObject element) =>
        (bool)element.GetValue(BreatheOnVisibleProperty);

    public static void SetBreatheOnVisible(DependencyObject element, bool value) =>
        element.SetValue(BreatheOnVisibleProperty, value);

    public static double GetBreatheDelay(DependencyObject element) =>
        (double)element.GetValue(BreatheDelayProperty);

    public static void SetBreatheDelay(DependencyObject element, double value) =>
        element.SetValue(BreatheDelayProperty, value);

    public static bool GetAnimateTabSelection(DependencyObject element) =>
        (bool)element.GetValue(AnimateTabSelectionProperty);

    public static void SetAnimateTabSelection(DependencyObject element, bool value) =>
        element.SetValue(AnimateTabSelectionProperty, value);

    private static void OnAnimateButtonInteractionChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not Button button)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            button.Loaded += Button_Loaded;
            button.Unloaded += Button_Unloaded;
            if (button.IsLoaded)
            {
                AttachButtonStateHandlers(button);
            }
        }
        else
        {
            button.Loaded -= Button_Loaded;
            button.Unloaded -= Button_Unloaded;
            DetachButtonStateHandlers(button);
        }
    }

    private static void OnAnimateToggleStateChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ToggleButton toggleButton)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            toggleButton.Loaded += ToggleButton_Loaded;
            toggleButton.Unloaded += ToggleButton_Unloaded;
            if (toggleButton.IsLoaded)
            {
                AttachToggleStateHandler(toggleButton);
            }
        }
        else
        {
            toggleButton.Loaded -= ToggleButton_Loaded;
            toggleButton.Unloaded -= ToggleButton_Unloaded;
            DetachToggleStateHandler(toggleButton);
        }
    }

    private static void OnAnimateOnVisibleChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            element.IsVisibleChanged += Element_IsVisibleChanged;
            if (element.IsVisible)
            {
                QueueEntranceAnimation(element);
            }
        }
        else
        {
            element.IsVisibleChanged -= Element_IsVisibleChanged;
            ResetAnimation(element);
        }
    }

    private static void OnFlyInOnVisibleChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            element.Loaded += Element_FlyInLoaded;
            element.Unloaded += Element_FlyInUnloaded;
            element.IsVisibleChanged += Element_FlyInIsVisibleChanged;
            if (element.IsVisible)
            {
                QueueFlyInAnimation(element);
            }
        }
        else
        {
            element.Loaded -= Element_FlyInLoaded;
            element.Unloaded -= Element_FlyInUnloaded;
            element.IsVisibleChanged -= Element_FlyInIsVisibleChanged;
            ResetFlyInAnimation(element);
        }
    }

    private static void OnBreatheOnVisibleChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            element.Loaded += Element_BreatheLoaded;
            element.Unloaded += Element_BreatheUnloaded;
            element.IsVisibleChanged += Element_BreatheIsVisibleChanged;
            if (element.IsVisible)
            {
                QueueBreatheAnimation(element);
            }
        }
        else
        {
            element.Loaded -= Element_BreatheLoaded;
            element.Unloaded -= Element_BreatheUnloaded;
            element.IsVisibleChanged -= Element_BreatheIsVisibleChanged;
            ResetBreatheAnimation(element);
        }
    }

    private static void OnAnimateTabSelectionChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not TabControl tabControl)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            tabControl.SelectionChanged += TabControl_SelectionChanged;
        }
        else
        {
            tabControl.SelectionChanged -= TabControl_SelectionChanged;
        }
    }

    private static void Button_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            AttachButtonStateHandlers(button);
        }
    }

    private static void Button_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            DetachButtonStateHandlers(button);
        }
    }

    private static void AttachButtonStateHandlers(Button button)
    {
        IsMouseOverDescriptor?.AddValueChanged(button, Button_StateChanged);
        IsPressedDescriptor?.AddValueChanged(button, Button_StateChanged);
        IsButtonEnabledDescriptor?.AddValueChanged(button, Button_StateChanged);
        UpdateButtonOverlay(button, animate: false);
    }

    private static void DetachButtonStateHandlers(Button button)
    {
        IsMouseOverDescriptor?.RemoveValueChanged(button, Button_StateChanged);
        IsPressedDescriptor?.RemoveValueChanged(button, Button_StateChanged);
        IsButtonEnabledDescriptor?.RemoveValueChanged(button, Button_StateChanged);
    }

    private static void Button_StateChanged(object? sender, EventArgs e)
    {
        if (sender is Button button)
        {
            UpdateButtonOverlay(button, animate: true);
        }
    }

    private static void UpdateButtonOverlay(Button button, bool animate)
    {
        button.ApplyTemplate();
        if (button.Template.FindName("InteractionOverlay", button) is not UIElement overlay)
        {
            return;
        }

        var targetOpacity = !button.IsEnabled
            ? 0
            : button.IsPressed
                ? 0.28
                : button.IsMouseOver
                    ? 0.16
                    : 0;
        SetOpacity(overlay, targetOpacity, animate);
    }

    private static void ToggleButton_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton toggleButton)
        {
            AttachToggleStateHandler(toggleButton);
        }
    }

    private static void ToggleButton_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton toggleButton)
        {
            DetachToggleStateHandler(toggleButton);
        }
    }

    private static void AttachToggleStateHandler(ToggleButton toggleButton)
    {
        IsCheckedDescriptor?.AddValueChanged(toggleButton, ToggleButton_StateChanged);
        UpdateTogglePresentation(toggleButton, animate: false);
    }

    private static void DetachToggleStateHandler(ToggleButton toggleButton) =>
        IsCheckedDescriptor?.RemoveValueChanged(toggleButton, ToggleButton_StateChanged);

    private static void ToggleButton_StateChanged(object? sender, EventArgs e)
    {
        if (sender is ToggleButton toggleButton)
        {
            UpdateTogglePresentation(toggleButton, animate: true);
        }
    }

    private static void UpdateTogglePresentation(ToggleButton toggleButton, bool animate)
    {
        toggleButton.ApplyTemplate();
        var overlay = toggleButton.Template.FindName("SwitchCheckedOverlay", toggleButton) as UIElement;
        var thumbTransform = toggleButton.Template.FindName("ThumbTransform", toggleButton) as TranslateTransform;
        if (overlay is null || thumbTransform is null)
        {
            return;
        }

        var isChecked = toggleButton.IsChecked == true;
        SetOpacity(overlay, isChecked ? 1 : 0, animate);
        SetTranslateX(thumbTransform, isChecked ? 14 : 0, animate);
    }

    private static void SetOpacity(UIElement element, double value, bool animate)
    {
        var currentValue = element.Opacity;
        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.Opacity = value;
        if (!animate || !SystemParameters.ClientAreaAnimation || currentValue == value)
        {
            return;
        }

        element.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(currentValue, value, TimeSpan.FromMilliseconds(130))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            },
            HandoffBehavior.SnapshotAndReplace);
    }

    private static void SetTranslateX(TranslateTransform transform, double value, bool animate)
    {
        var currentValue = transform.X;
        transform.BeginAnimation(TranslateTransform.XProperty, null);
        transform.X = value;
        if (!animate || !SystemParameters.ClientAreaAnimation || currentValue == value)
        {
            return;
        }

        transform.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(currentValue, value, TimeSpan.FromMilliseconds(140))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            },
            HandoffBehavior.SnapshotAndReplace);
    }

    private static void Element_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        if (element.IsVisible)
        {
            QueueEntranceAnimation(element);
        }
        else
        {
            ResetAnimation(element);
        }
    }

    private static void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not TabControl tabControl
            || !ReferenceEquals(e.Source, tabControl))
        {
            return;
        }

        tabControl.Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                if (!tabControl.IsVisible)
                {
                    return;
                }

                tabControl.ApplyTemplate();
                if (tabControl.Template.FindName("PART_SelectedContentHost", tabControl)
                    is FrameworkElement contentHost)
                {
                    StartEntranceAnimation(contentHost);
                }
            }));
    }

    private static void QueueEntranceAnimation(FrameworkElement element)
    {
        element.Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                if (element.IsVisible)
                {
                    StartEntranceAnimation(element);
                }
            }));
    }

    private static void Element_FlyInLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.IsVisible)
        {
            QueueFlyInAnimation(element);
        }
    }

    private static void Element_FlyInUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            ResetFlyInAnimation(element);
        }
    }

    private static void Element_FlyInIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        if (element.IsVisible)
        {
            QueueFlyInAnimation(element);
        }
        else
        {
            ResetFlyInAnimation(element);
        }
    }

    private static void Element_BreatheLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.IsVisible)
        {
            QueueBreatheAnimation(element);
        }
    }

    private static void Element_BreatheUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            ResetBreatheAnimation(element);
        }
    }

    private static void Element_BreatheIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        if (element.IsVisible)
        {
            QueueBreatheAnimation(element);
        }
        else
        {
            ResetBreatheAnimation(element);
        }
    }

    private static void QueueBreatheAnimation(FrameworkElement element)
    {
        element.Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                if (element.IsVisible && GetBreatheOnVisible(element))
                {
                    StartBreatheAnimation(element);
                }
            }));
    }

    private static void StartBreatheAnimation(FrameworkElement element)
    {
        var scaleTransform = GetScaleTransform(element);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        scaleTransform.ScaleX = 1;
        scaleTransform.ScaleY = 1;

        if (!SystemParameters.ClientAreaAnimation)
        {
            return;
        }

        var breatheAnimation = new DoubleAnimation(1, 1.035, TimeSpan.FromSeconds(2.4))
        {
            BeginTime = TimeSpan.FromMilliseconds(Math.Max(0, GetBreatheDelay(element))),
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            RepeatBehavior = RepeatBehavior.Forever
        };
        scaleTransform.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            breatheAnimation,
            HandoffBehavior.SnapshotAndReplace);
        scaleTransform.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            breatheAnimation,
            HandoffBehavior.SnapshotAndReplace);
    }

    private static void ResetBreatheAnimation(FrameworkElement element)
    {
        var scaleTransform = FindScaleTransform(element.RenderTransform);
        if (scaleTransform is null)
        {
            return;
        }

        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        scaleTransform.ScaleX = 1;
        scaleTransform.ScaleY = 1;
    }

    private static void QueueFlyInAnimation(FrameworkElement element)
    {
        element.Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                if (element.IsVisible && GetFlyInOnVisible(element))
                {
                    StartFlyInAnimation(element);
                }
            }));
    }

    private static void StartFlyInAnimation(FrameworkElement element)
    {
        var translateTransform = GetTranslateTransform(element);
        element.BeginAnimation(UIElement.OpacityProperty, null);
        var targetOpacity = element.Opacity;
        translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
        translateTransform.BeginAnimation(TranslateTransform.YProperty, null);

        if (!SystemParameters.ClientAreaAnimation)
        {
            translateTransform.X = 0;
            translateTransform.Y = 0;
            return;
        }

        var fromX = GetFlyInFromX(element);
        var fromY = GetFlyInFromY(element);
        var delay = TimeSpan.FromMilliseconds(Math.Max(0, GetFlyInDelay(element)));
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = new Duration(DecorationTransitionDuration);
        translateTransform.X = fromX;
        translateTransform.Y = fromY;

        var opacityAnimation = new DoubleAnimation(0, targetOpacity, duration)
        {
            BeginTime = delay,
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd
        };
        opacityAnimation.Completed += (_, _) =>
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
        };
        var xAnimation = new DoubleAnimation(fromX, 0, duration)
        {
            BeginTime = delay,
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd
        };
        xAnimation.Completed += (_, _) =>
        {
            translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
            translateTransform.X = 0;
        };
        var yAnimation = new DoubleAnimation(fromY, 0, duration)
        {
            BeginTime = delay,
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd
        };
        yAnimation.Completed += (_, _) =>
        {
            translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
            translateTransform.Y = 0;
        };

        element.BeginAnimation(
            UIElement.OpacityProperty,
            opacityAnimation,
            HandoffBehavior.SnapshotAndReplace);
        translateTransform.BeginAnimation(
            TranslateTransform.XProperty,
            xAnimation,
            HandoffBehavior.SnapshotAndReplace);
        translateTransform.BeginAnimation(
            TranslateTransform.YProperty,
            yAnimation,
            HandoffBehavior.SnapshotAndReplace);
    }

    private static void ResetFlyInAnimation(FrameworkElement element)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);

        var translateTransform = FindTranslateTransform(element.RenderTransform);
        if (translateTransform is not null)
        {
            translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
            translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
            translateTransform.X = 0;
            translateTransform.Y = 0;
        }
    }

    private static void StartEntranceAnimation(FrameworkElement element)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            ResetAnimation(element);
            return;
        }

        var translateTransform = GetTranslateTransform(element);
        element.BeginAnimation(UIElement.OpacityProperty, null);
        translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
        element.Opacity = 1;
        translateTransform.Y = 0;

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        element.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, PageTransitionDuration)
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.Stop
            },
            HandoffBehavior.SnapshotAndReplace);
        translateTransform.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(8, 0, PageTransitionDuration)
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.Stop
            },
            HandoffBehavior.SnapshotAndReplace);
    }

    private static void ResetAnimation(FrameworkElement element)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.Opacity = 1;

        var translateTransform = FindTranslateTransform(element.RenderTransform);
        if (translateTransform is not null)
        {
            translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
            translateTransform.Y = 0;
        }
    }

    private static TranslateTransform GetTranslateTransform(FrameworkElement element)
    {
        var existingTransform = FindTranslateTransform(element.RenderTransform);
        if (existingTransform is not null)
        {
            return existingTransform;
        }

        var translateTransform = new TranslateTransform();
        if (element.RenderTransform.Value.IsIdentity)
        {
            element.RenderTransform = translateTransform;
            return translateTransform;
        }

        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(element.RenderTransform);
        transformGroup.Children.Add(translateTransform);
        element.RenderTransform = transformGroup;
        return translateTransform;
    }

    private static TranslateTransform? FindTranslateTransform(Transform transform)
    {
        if (transform is TranslateTransform translateTransform && !translateTransform.IsFrozen)
        {
            return translateTransform;
        }

        if (transform is TransformGroup transformGroup)
        {
            foreach (var child in transformGroup.Children)
            {
                var match = FindTranslateTransform(child);
                if (match is not null)
                {
                    return match;
                }
            }
        }

        return null;
    }

    private static ScaleTransform GetScaleTransform(FrameworkElement element)
    {
        var existingTransform = FindScaleTransform(element.RenderTransform);
        if (existingTransform is not null)
        {
            return existingTransform;
        }

        var scaleTransform = new ScaleTransform();
        if (element.RenderTransform.Value.IsIdentity)
        {
            element.RenderTransform = scaleTransform;
            return scaleTransform;
        }

        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(element.RenderTransform);
        transformGroup.Children.Add(scaleTransform);
        element.RenderTransform = transformGroup;
        return scaleTransform;
    }

    private static ScaleTransform? FindScaleTransform(Transform transform)
    {
        if (transform is ScaleTransform scaleTransform && !scaleTransform.IsFrozen)
        {
            return scaleTransform;
        }

        if (transform is TransformGroup transformGroup)
        {
            foreach (var child in transformGroup.Children)
            {
                var match = FindScaleTransform(child);
                if (match is not null)
                {
                    return match;
                }
            }
        }

        return null;
    }
}
