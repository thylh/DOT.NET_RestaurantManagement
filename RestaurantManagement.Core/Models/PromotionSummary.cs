namespace RestaurantManagement.Core.Models;

public sealed record PromotionSummary(
    int Id,
    string Name,
    decimal DiscountPercentage,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsActive,
    int? ProductId,
    string? ProductName,
    int? CategoryId,
    string? CategoryName)
{
    public string TargetName => ProductName ?? CategoryName ?? string.Empty;

    public string PeriodDisplay =>
        $"{StartsAt.ToLocalTime():dd/MM/yyyy} - {EndsAt.ToLocalTime():dd/MM/yyyy}";
}
