namespace RestaurantManagement.Core.Models;

public sealed record ProductSummary(
    int Id,
    string Name,
    string? ImagePath,
    string? Tags,
    decimal Price,
    bool IsAvailable,
    bool IsActive,
    int CategoryId,
    string CategoryName);
