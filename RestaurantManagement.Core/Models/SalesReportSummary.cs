using RestaurantManagement.Core.Enums;

namespace RestaurantManagement.Core.Models;

public sealed record SalesReportSummary(
    int PaidOrderCount,
    decimal GrossRevenue,
    decimal DiscountTotal,
    decimal NetRevenue,
    IReadOnlyList<PaymentMethodRevenueSummary> PaymentMethods,
    IReadOnlyList<TopSellingProductSummary> TopSellingProducts,
    IReadOnlyList<DailyRevenueSummary> DailyRevenue);

public sealed record DailyRevenueSummary(
    DateOnly Date,
    decimal NetRevenue);

public sealed record PaymentMethodRevenueSummary(
    PaymentMethod Method,
    int PaymentCount,
    decimal TotalAmount)
{
    public string MethodDisplay => Method switch
    {
        PaymentMethod.Cash => "Tiền mặt",
        PaymentMethod.BankTransfer => "Chuyển khoản",
        _ => "Không xác định"
    };
}

public sealed record TopSellingProductSummary(
    string ProductName,
    int QuantitySold,
    decimal GrossRevenue);
