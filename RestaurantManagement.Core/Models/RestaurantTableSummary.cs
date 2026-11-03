namespace RestaurantManagement.Core.Models;

public sealed record RestaurantTableSummary(
    int Id,
    string TableNumber,
    string Area,
    int SeatCount,
    bool IsActive,
    bool IsOccupied)
{
    public string OccupancyStatus => IsOccupied ? "Có khách" : "Trống";

    public string ServiceStatus => IsActive ? "Đang phục vụ" : "Ngừng phục vụ";

    public bool CanToggleServiceStatus => !IsOccupied || !IsActive;
}
