using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using RestaurantManagement.Core.Models;

namespace RestaurantManagement.WPF.Controls;

public sealed class RevenueTrendChart : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IReadOnlyList<DailyRevenueSummary>),
            typeof(RevenueTrendChart),
            new FrameworkPropertyMetadata(
                Array.Empty<DailyRevenueSummary>(),
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnItemsSourceChanged));

    private Rect _plotBounds;
    private readonly List<Point> _pointLocations = [];
    private int _visiblePointCount;
    private static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(
            nameof(Progress),
            typeof(double),
            typeof(RevenueTrendChart),
            new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(900);

    public RevenueTrendChart()
    {
        Loaded += OnLoaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    public IReadOnlyList<DailyRevenueSummary> ItemsSource
    {
        get => (IReadOnlyList<DailyRevenueSummary>)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    private static void OnItemsSourceChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        var chart = (RevenueTrendChart)dependencyObject;
        if (chart.IsLoaded)
        {
            chart.AnimateChart();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => AnimateChart();

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            AnimateChart();
        }
        else
        {
            BeginAnimation(ProgressProperty, null);
            Progress = 0;
        }
    }

    private void AnimateChart()
    {
        BeginAnimation(ProgressProperty, null);
        if (!SystemParameters.ClientAreaAnimation)
        {
            Progress = 1;
            return;
        }

        Progress = 0;
        BeginAnimation(
            ProgressProperty,
            new DoubleAnimation(0, 1, AnimationDuration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            },
            HandoffBehavior.SnapshotAndReplace);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        _pointLocations.Clear();
        _visiblePointCount = 0;
        _plotBounds = new Rect(52, 12, Math.Max(0, ActualWidth - 68), Math.Max(0, ActualHeight - 52));

        var points = ItemsSource ?? Array.Empty<DailyRevenueSummary>();
        var maxRevenue = points.Count == 0 ? 0 : points.Max(point => point.NetRevenue);
        if (points.Count == 0 || maxRevenue <= 0 || _plotBounds.Width <= 0 || _plotBounds.Height <= 0)
        {
            DrawText(
                drawingContext,
                points.Count == 0
                    ? "Hãy chọn khoảng ngày để xem biểu đồ."
                    : "Chưa có doanh thu trong khoảng thời gian này.",
                SecondaryTextBrush,
                Math.Max(12, ActualWidth - 24),
                14,
                new Point(12, Math.Max(0, (ActualHeight - 20) / 2)));
            return;
        }

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10((double)maxRevenue)));
        var axisMaximum = (decimal)(Math.Ceiling((double)maxRevenue / magnitude) * magnitude);
        var gridPen = new Pen(BorderBrush, 1);
        var textBrush = SecondaryTextBrush;

        for (var tick = 0; tick <= 4; tick++)
        {
            var fraction = tick / 4d;
            var y = _plotBounds.Bottom - (_plotBounds.Height * fraction);
            drawingContext.DrawLine(gridPen, new Point(_plotBounds.Left, y), new Point(_plotBounds.Right, y));
            DrawText(
                drawingContext,
                (axisMaximum * (decimal)fraction).ToString("N0", CultureInfo.CurrentCulture),
                textBrush,
                46,
                11,
                new Point(0, y - 8));
        }

        for (var index = 0; index < points.Count; index++)
        {
            var x = points.Count == 1
                ? _plotBounds.Left + (_plotBounds.Width / 2)
                : _plotBounds.Left + (_plotBounds.Width * index / (points.Count - 1));
            var y = _plotBounds.Bottom
                - (_plotBounds.Height * (double)(points[index].NetRevenue / axisMaximum));
            _pointLocations.Add(new Point(x, y));
        }

        var revealedSegments = Progress * (points.Count - 1);
        var revenuePen = new Pen(PrimaryBrush, 2.5);
        for (var segment = 1; segment < points.Count; segment++)
        {
            var segmentProgress = Math.Clamp(revealedSegments - (segment - 1), 0, 1);
            if (segmentProgress <= 0)
            {
                break;
            }

            var start = _pointLocations[segment - 1];
            var end = _pointLocations[segment];
            var partialEnd = new Point(
                start.X + ((end.X - start.X) * segmentProgress),
                start.Y + ((end.Y - start.Y) * segmentProgress));
            drawingContext.DrawLine(revenuePen, start, partialEnd);
        }

        _visiblePointCount = Progress <= 0
            ? 0
            : Math.Clamp((int)Math.Floor(revealedSegments) + 1, 0, points.Count);
        var pointBrush = AccentBrush;
        for (var index = 0; index < _visiblePointCount; index++)
        {
            var reveal = points.Count == 1
                ? Progress
                : Math.Clamp(revealedSegments - index + 1, 0, 1);
            var radius = 3.5 * reveal;
            drawingContext.DrawEllipse(pointBrush, null, _pointLocations[index], radius, radius);
        }

        var labelCount = Math.Min(5, points.Count);
        for (var labelIndex = 0; labelIndex < labelCount; labelIndex++)
        {
            var index = labelCount == 1
                ? 0
                : (int)Math.Round(labelIndex * (points.Count - 1d) / (labelCount - 1));
            var label = points[index].Date.ToString("dd/MM", CultureInfo.CurrentCulture);
            var labelWidth = Math.Min(42, _plotBounds.Width);
            var labelX = Math.Clamp(_pointLocations[index].X - (labelWidth / 2), _plotBounds.Left, _plotBounds.Right - labelWidth);
            DrawText(
                drawingContext,
                label,
                textBrush,
                labelWidth,
                11,
                new Point(labelX, _plotBounds.Bottom + 8));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_visiblePointCount == 0 || !_plotBounds.Contains(e.GetPosition(this)))
        {
            ToolTip = null;
            return;
        }

        var x = e.GetPosition(this).X;
        var index = 0;
        var nearestDistance = Math.Abs(_pointLocations[0].X - x);
        for (var candidate = 1; candidate < _visiblePointCount; candidate++)
        {
            var distance = Math.Abs(_pointLocations[candidate].X - x);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                index = candidate;
            }
        }

        var point = ItemsSource[index];
        ToolTip = $"{point.Date:dd/MM/yyyy}: {point.NetRevenue.ToString("N0", CultureInfo.CurrentCulture)} đ";
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        ToolTip = null;
    }

    private void DrawText(
        DrawingContext drawingContext,
        string text,
        Brush brush,
        double maxWidth,
        double fontSize,
        Point origin)
    {
        var formattedText = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            fontSize,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(1, maxWidth),
            Trimming = TextTrimming.CharacterEllipsis
        };
        drawingContext.DrawText(formattedText, origin);
    }

    private Brush ResourceBrush(string resourceKey, Brush fallback) =>
        TryFindResource(resourceKey) as Brush ?? fallback;

    private Brush PrimaryBrush => ResourceBrush("PrimaryBrush", Brushes.SaddleBrown);

    private Brush AccentBrush => ResourceBrush("AccentBrush", Brushes.DarkOrange);

    private Brush BorderBrush => ResourceBrush("BorderBrush", Brushes.LightGray);

    private Brush SecondaryTextBrush => ResourceBrush("TextSecondaryBrush", Brushes.DimGray);
}
