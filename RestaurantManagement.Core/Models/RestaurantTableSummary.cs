namespace RestaurantManagement.Core.Models;

public sealed record RestaurantTableSummary(
    int Id,
    string TableNumber,
    string Area,
    int SeatCount,
    bool IsActive,
    bool IsOccupied,
    int? OpenOrderId = null)
{
    public string OccupancyStatus => OpenOrderId is { } orderId
        ? $"Order #{orderId}"
        : IsOccupied ? "Có khách" : "Trống";

    public string OpenOrderStatus => IsOccupied ? "Có order" : "Không có order";

    public string ServiceStatus => IsActive ? "Đang phục vụ" : "Ngừng phục vụ";

    public bool CanToggleServiceStatus => !IsOccupied || !IsActive;
}
