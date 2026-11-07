using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RestaurantManagement.WPF.Behaviors;

public static class OpenBottomFrameBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(OpenBottomFrameBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly ConditionalWeakTable<Border, FrameState> States = new();

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not Border frame)
        {
            throw new InvalidOperationException($"{nameof(OpenBottomFrameBehavior)} can only be used on a Border.");
        }

        if ((bool)args.NewValue)
        {
            frame.Loaded += OnFrameLoaded;
            frame.Unloaded += OnFrameUnloaded;
            frame.SizeChanged += OnFrameSizeChanged;
            frame.IsVisibleChanged += OnFrameIsVisibleChanged;

            if (frame.IsLoaded)
            {
                UpdateFrame(frame);
            }

            return;
        }

        frame.Loaded -= OnFrameLoaded;
        frame.Unloaded -= OnFrameUnloaded;
        frame.SizeChanged -= OnFrameSizeChanged;
        frame.IsVisibleChanged -= OnFrameIsVisibleChanged;
        RemoveAdorner(frame);
    }

    private static void OnFrameLoaded(object sender, RoutedEventArgs args) =>
        UpdateFrame((Border)sender);

    private static void OnFrameUnloaded(object sender, RoutedEventArgs args) =>
        RemoveAdorner((Border)sender);

    private static void OnFrameSizeChanged(object sender, SizeChangedEventArgs args) =>
        UpdateFrame((Border)sender);

    private static void OnFrameIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        var frame = (Border)sender;
        if ((bool)args.NewValue)
        {
            UpdateFrame(frame);
        }
        else
        {
            RemoveAdorner(frame);
        }
    }

    private static void UpdateFrame(Border frame)
    {
        if (!frame.IsLoaded || !frame.IsVisible || VisualTreeHelper.GetParent(frame) is not FrameworkElement parent)
        {
            RemoveAdorner(frame);
            return;
        }

        var state = States.GetValue(frame, static _ => new FrameState());
        if (!ReferenceEquals(state.Parent, parent))
        {
            RemoveDrawing(state);
            DetachParent(state);
            state.Parent = parent;
            state.LayoutUpdatedHandler = (_, _) =>
            {
                if (frame.IsLoaded)
                {
                    UpdateFrame(frame);
                }
            };
            parent.LayoutUpdated += state.LayoutUpdatedHandler;
        }

        var transform = frame.TransformToVisual(parent);
        var frameBounds = transform.TransformBounds(new Rect(new Point(), frame.RenderSize));
        if (frameBounds.Bottom >= parent.ActualHeight - 1 || frameBounds.Width <= 0)
        {
            RemoveDrawing(state);
            return;
        }

        var layer = AdornerLayer.GetAdornerLayer(parent);
        if (layer is null)
        {
            throw new InvalidOperationException(
                $"An AdornerLayer is required to extend the open frame edges for {frame.Name}.");
        }

        if (state.Adorner is null)
        {
            state.Adorner = new OpenBottomFrameAdorner(parent, frame);
            state.Layer = layer;
            layer.Add(state.Adorner);
        }
        else
        {
            state.Adorner.InvalidateVisual();
        }
    }

    private static void RemoveAdorner(Border frame)
    {
        if (!States.TryGetValue(frame, out var state))
        {
            return;
        }

        RemoveDrawing(state);
        DetachParent(state);
    }

    private static void RemoveDrawing(FrameState state)
    {
        if (state.Adorner is not null && state.Layer is not null)
        {
            state.Layer.Remove(state.Adorner);
            state.Adorner = null;
            state.Layer = null;
        }
    }

    private static void DetachParent(FrameState state)
    {
        if (state.Parent is not null)
        {
            if (state.LayoutUpdatedHandler is not null)
            {
                state.Parent.LayoutUpdated -= state.LayoutUpdatedHandler;
                state.LayoutUpdatedHandler = null;
            }

            state.Parent = null;
        }
    }

    private sealed class FrameState
    {
        public FrameworkElement? Parent { get; set; }

        public AdornerLayer? Layer { get; set; }

        public OpenBottomFrameAdorner? Adorner { get; set; }

        public EventHandler? LayoutUpdatedHandler { get; set; }
    }

    private sealed class OpenBottomFrameAdorner : Adorner
    {
        private readonly Border _frame;

        public OpenBottomFrameAdorner(UIElement adornedElement, Border frame)
            : base(adornedElement)
        {
            _frame = frame;
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (!_frame.IsVisible || AdornedElement is not FrameworkElement parent)
            {
                return;
            }

            var frameBounds = _frame.TransformToVisual(parent)
                .TransformBounds(new Rect(new Point(), _frame.RenderSize));
            var borderThickness = _frame.BorderThickness;
            var penThickness = Math.Max(borderThickness.Left, borderThickness.Right);
            if (penThickness <= 0 || frameBounds.Bottom >= parent.ActualHeight - 1)
            {
                return;
            }

            var brush = _frame.BorderBrush;
            if (brush is null)
            {
                return;
            }

            var pen = new Pen(brush, penThickness);
            if (pen.CanFreeze)
            {
                pen.Freeze();
            }

            DrawEdge(drawingContext, parent, frameBounds.Left + borderThickness.Left / 2, frameBounds.Bottom, pen);
            DrawEdge(drawingContext, parent, frameBounds.Right - borderThickness.Right / 2, frameBounds.Bottom, pen);
        }

        private void DrawEdge(DrawingContext drawingContext, FrameworkElement parent, double x, double startY, Pen pen)
        {
            var endY = parent.ActualHeight;
            var stopY = endY;
            VisitVisuals(parent, element =>
            {
                if (ReferenceEquals(element, _frame)
                    || IsDescendantOf(element, _frame)
                    || !BlocksFrameEdge(element))
                {
                    return;
                }

                var bounds = GetBounds(element, parent);
                if (bounds.Right < x - pen.Thickness / 2
                    || bounds.Left > x + pen.Thickness / 2
                    || bounds.Bottom <= startY
                    || bounds.Top >= stopY)
                {
                    return;
                }

                stopY = Math.Max(startY, Math.Min(stopY, bounds.Top));
            });

            if (stopY > startY)
            {
                drawingContext.DrawLine(pen, new Point(x, startY), new Point(x, stopY));
            }
        }

        private static bool BlocksFrameEdge(Visual visual)
        {
            if (visual is not UIElement element || !element.IsVisible || element.Opacity <= 0.01)
            {
                return false;
            }

            return element is TextBlock
                or Control
                or Border { Background: not null }
                or Border { BorderBrush: not null }
                or Image
                or Shape;
        }

        private static Rect GetBounds(Visual visual, Visual relativeTo)
        {
            var size = visual is UIElement element ? element.RenderSize : Size.Empty;
            return visual.TransformToVisual(relativeTo).TransformBounds(new Rect(new Point(), size));
        }

        private static bool IsDescendantOf(Visual visual, Visual ancestor)
        {
            for (DependencyObject? current = visual; current is Visual; current = VisualTreeHelper.GetParent(current))
            {
                if (ReferenceEquals(current, ancestor))
                {
                    return true;
                }
            }

            return false;
        }

        private static void VisitVisuals(Visual parent, Action<Visual> visitor)
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                if (VisualTreeHelper.GetChild(parent, index) is not Visual child)
                {
                    continue;
                }

                visitor(child);
                VisitVisuals(child, visitor);
            }
        }
    }
}
