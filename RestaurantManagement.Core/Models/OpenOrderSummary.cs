namespace RestaurantManagement.Core.Models;

public sealed record OpenOrderSummary(
    int OrderId,
    int TableId,
    IReadOnlyList<OrderDetailSummary> Details,
    string? PromotionName = null,
    decimal DiscountAmount = 0)
{
    public decimal Subtotal => Details.Sum(detail => detail.LineTotal);

    public decimal Total => Subtotal - DiscountAmount;
}
