using RestaurantManagement.Core.Enums;

namespace RestaurantManagement.Core.Models;

public sealed record OrderHistorySummary(
    int OrderId,
    string TableNumber,
    string Area,
    string EmployeeName,
    DateTimeOffset CreatedAt,
    OrderStatus Status,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal Total,
    PaymentMethod? PaymentMethod,
    DateTimeOffset? PaidAt,
    string? CancellationReason)
{
    public string StatusDisplay => Status switch
    {
        OrderStatus.Open => "Đang mở",
        OrderStatus.Paid => "Đã thanh toán",
        OrderStatus.Cancelled => "Đã hủy",
        _ => "Không xác định"
    };

    public string PaymentMethodDisplay => PaymentMethod switch
    {
        global::RestaurantManagement.Core.Enums.PaymentMethod.Cash => "Tiền mặt",
        global::RestaurantManagement.Core.Enums.PaymentMethod.BankTransfer => "Chuyển khoản",
        _ => "—"
    };
}
