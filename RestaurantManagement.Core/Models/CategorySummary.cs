namespace RestaurantManagement.Core.Models;

public sealed record CategorySummary(
    int Id,
    string Name,
    string? Description,
    bool IsActive,
    int ProductCount);
