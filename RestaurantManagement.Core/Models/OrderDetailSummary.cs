namespace RestaurantManagement.Core.Models;

public sealed record OrderDetailSummary(
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    string? Notes)
{
    public decimal LineTotal => UnitPrice * Quantity;
}
