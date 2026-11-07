namespace RestaurantManagement.Core.Models;

public sealed record OrderDetailSummary(
    int Id,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    string? Notes)
{
    public decimal LineTotal => UnitPrice * Quantity;
}
