using RestaurantManagement.Core.Enums;

namespace RestaurantManagement.Core.Models;

public sealed record OrderReceiptSummary(
    int OrderId,
    string TableNumber,
    string Area,
    string EmployeeName,
    DateTimeOffset CreatedAt,
    OrderStatus Status,
    string? PromotionName,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal Total,
    PaymentMethod? PaymentMethod,
    DateTimeOffset? PaidAt,
    string? CancellationReason,
    IReadOnlyList<OrderDetailSummary> Details)
{
    public string StatusDisplay => Status switch
    {
        OrderStatus.Open => "Đang mở",
        OrderStatus.Paid => "Đã thanh toán",
        OrderStatus.Cancelled => "Đã hủy",
        _ => "Không xác định"
    };

    public bool IsCancelled => Status == OrderStatus.Cancelled;

    public string PromotionDisplay => $"Khuyến mãi: {PromotionName ?? "không có"}";

    public string PaymentMethodDisplay => PaymentMethod switch
    {
        global::RestaurantManagement.Core.Enums.PaymentMethod.Cash => "Tiền mặt",
        global::RestaurantManagement.Core.Enums.PaymentMethod.BankTransfer => "Chuyển khoản",
        _ => "—"
    };
}
