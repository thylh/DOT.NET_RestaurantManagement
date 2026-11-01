namespace RestaurantManagement.Core.Models;

public sealed record RestaurantTableSummary(
    int Id,
    int TableNumber,
    int SeatCount,
    bool IsActive,
    bool IsOccupied);
