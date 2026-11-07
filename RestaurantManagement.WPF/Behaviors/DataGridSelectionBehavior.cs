using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace RestaurantManagement.WPF.Behaviors;

public static class DataGridSelectionBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(DataGridSelectionBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty PreserveSelectionOnClickProperty =
        DependencyProperty.RegisterAttached(
            "PreserveSelectionOnClick",
            typeof(bool),
            typeof(DataGridSelectionBehavior),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetPreserveSelectionOnClick(DependencyObject element, bool value) =>
        element.SetValue(PreserveSelectionOnClickProperty, value);

    public static bool GetPreserveSelectionOnClick(DependencyObject element) =>
        (bool)element.GetValue(PreserveSelectionOnClickProperty);

    private static void OnIsEnabledChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not UIElement element)
        {
            return;
        }

        if ((bool)args.NewValue)
        {
            element.PreviewMouseDown += Element_PreviewMouseDown;
        }
        else
        {
            element.PreviewMouseDown -= Element_PreviewMouseDown;
        }
    }

    private static void Element_PreviewMouseDown(object sender, MouseButtonEventArgs args)
    {
        if (sender is not DependencyObject root
            || args.OriginalSource is not DependencyObject source
            || ShouldPreserveSelection(source))
        {
            return;
        }

        var clickedGrid = FindContainingDataGrid(source);
        if (clickedGrid is null)
        {
            return;
        }

        foreach (var dataGrid in FindVisualChildren<DataGrid>(root))
        {
            if (!ReferenceEquals(dataGrid, clickedGrid))
            {
                dataGrid.SelectedItem = null;
            }
        }
    }

    private static bool ShouldPreserveSelection(DependencyObject source)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (GetPreserveSelectionOnClick(current))
            {
                return true;
            }

            if (current is ComboBoxItem comboBoxItem
                && ItemsControl.ItemsControlFromItemContainer(comboBoxItem) is { } owner
                && GetPreserveSelectionOnClick(owner))
            {
                return true;
            }

            current = GetParent(current);
        }

        return false;
    }

    private static DataGrid? FindContainingDataGrid(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is DataGrid dataGrid)
            {
                return dataGrid;
            }

            source = GetParent(source);
        }

        return null;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        if (parent is not System.Windows.Media.Visual
            && parent is not System.Windows.Media.Media3D.Visual3D)
        {
            yield break;
        }

        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
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

    private static DependencyObject? GetParent(DependencyObject element)
    {
        if (element is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D)
        {
            return System.Windows.Media.VisualTreeHelper.GetParent(element);
        }

        if (element is ContentElement contentElement)
        {
            return ContentOperations.GetParent(contentElement)
                ?? (contentElement as FrameworkContentElement)?.Parent;
        }

        return LogicalTreeHelper.GetParent(element);
    }
}
