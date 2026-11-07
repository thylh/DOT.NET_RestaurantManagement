using CommunityToolkit.Mvvm.ComponentModel;
using RestaurantManagement.Core.Models;

namespace RestaurantManagement.WPF.ViewModels;

public sealed partial class OrderWorkspaceTab : ObservableObject
{
    public OrderWorkspaceTab()
    {
    }

    public OrderWorkspaceTab(int? tableId, string title, OpenOrderSummary? order = null)
    {
        TableId = tableId;
        Title = title;
        Order = order;
    }

    [ObservableProperty]
    private int? tableId;

    [ObservableProperty]
    private string title = "Tab mới";

    [ObservableProperty]
    private OpenOrderSummary? order;

    [ObservableProperty]
    private bool isSelected;
}
