using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace RestaurantManagement.WPF.Behaviors;

public static class AutoHideScrollBarBehavior
{
    private static readonly ConditionalWeakTable<ScrollViewer, ScrollViewerState> States = new();

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(AutoHideScrollBarBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ScrollViewer scrollViewer)
        {
            return;
        }

        if ((bool)args.NewValue)
        {
            scrollViewer.Loaded += ScrollViewer_Loaded;
            scrollViewer.Unloaded += ScrollViewer_Unloaded;
        }
        else
        {
            scrollViewer.Loaded -= ScrollViewer_Loaded;
            scrollViewer.Unloaded -= ScrollViewer_Unloaded;
            StopTracking(scrollViewer);
        }
    }

    private static void ScrollViewer_Loaded(object sender, RoutedEventArgs args)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        var state = States.GetValue(scrollViewer, static viewer => new ScrollViewerState(viewer));
        state.StartTracking();
    }

    private static void ScrollViewer_Unloaded(object sender, RoutedEventArgs args)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            StopTracking(scrollViewer);
        }
    }

    private static void StopTracking(ScrollViewer scrollViewer)
    {
        if (States.TryGetValue(scrollViewer, out var state))
        {
            state.StopTracking();
        }
    }

    private sealed class ScrollViewerState(ScrollViewer scrollViewer)
    {
        private readonly DispatcherTimer _hideTimer = new()
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        private bool _isTracking;

        public void StartTracking()
        {
            if (_isTracking)
            {
                return;
            }

            _isTracking = true;
            scrollViewer.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnScrollChanged));
            scrollViewer.PreviewMouseMove += OnUserInteraction;
            scrollViewer.PreviewMouseWheel += OnUserInteraction;
            scrollViewer.PreviewMouseDown += OnUserInteraction;
            scrollViewer.PreviewKeyDown += OnUserInteraction;
            _hideTimer.Tick += HideTimer_Tick;
            scrollViewer.Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() => ApplyToScrollBars(scrollBar =>
                {
                    scrollBar.Opacity = 0;
                    scrollBar.IsHitTestVisible = false;
                })));
        }

        public void StopTracking()
        {
            if (!_isTracking)
            {
                return;
            }

            _isTracking = false;
            _hideTimer.Stop();
            scrollViewer.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnScrollChanged));
            scrollViewer.PreviewMouseMove -= OnUserInteraction;
            scrollViewer.PreviewMouseWheel -= OnUserInteraction;
            scrollViewer.PreviewMouseDown -= OnUserInteraction;
            scrollViewer.PreviewKeyDown -= OnUserInteraction;
            _hideTimer.Tick -= HideTimer_Tick;
            ApplyToScrollBars(scrollBar =>
            {
                scrollBar.Opacity = 1;
                scrollBar.IsHitTestVisible = true;
            });
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs args)
        {
            if (args.VerticalChange != 0
                || args.HorizontalChange != 0
                || args.ExtentHeightChange != 0
                || args.ExtentWidthChange != 0)
            {
                ShowAndRestartTimer();
            }
        }

        private void OnUserInteraction(object sender, RoutedEventArgs args) =>
            ShowAndRestartTimer();

        private void ShowAndRestartTimer()
        {
            ApplyToScrollBars(scrollBar =>
            {
                scrollBar.BeginAnimation(UIElement.OpacityProperty, null);
                scrollBar.Opacity = 1;
                scrollBar.IsHitTestVisible = true;
            });
            _hideTimer.Stop();
            _hideTimer.Start();
        }

        private void HideTimer_Tick(object? sender, EventArgs args)
        {
            _hideTimer.Stop();
            ApplyToScrollBars(scrollBar =>
            {
                var animation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(220))
                {
                    FillBehavior = FillBehavior.Stop
                };
                animation.Completed += (_, _) =>
                {
                    scrollBar.Opacity = 0;
                    scrollBar.IsHitTestVisible = false;
                };
                scrollBar.BeginAnimation(UIElement.OpacityProperty, animation);
            });
        }

        private void ApplyToScrollBars(Action<ScrollBar> action)
        {
            foreach (var scrollBar in FindVisualChildren<ScrollBar>(scrollViewer))
            {
                action(scrollBar);
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
            where T : DependencyObject
        {
            if (parent is not Visual && parent is not System.Windows.Media.Media3D.Visual3D)
            {
                yield break;
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (var descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }
    }
}
