namespace RestaurantManagement.Core.Models;

public sealed record ProductSummary(
    int Id,
    string Name,
    string? Description,
    string? ImagePath,
    decimal Price,
    bool IsAvailable,
    bool IsActive,
    int CategoryId,
    string CategoryName);
