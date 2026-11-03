namespace RestaurantManagement.Core.Models;

public sealed record CategorySummary(
    int Id,
    string Name,
    bool IsActive,
    int ProductCount);
