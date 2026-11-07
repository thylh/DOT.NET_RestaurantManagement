using RestaurantManagement.Core.Enums;
using RestaurantManagement.Core.Models;

namespace RestaurantManagement.Core.Services;

public interface IRestaurantOrderService
{
    Task<OpenOrderSummary?> GetOpenOrderAsync(
        int tableId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderHistorySummary>> GetOrderHistoryAsync(
        int? orderId = null,
        DateTimeOffset? fromInclusive = null,
        DateTimeOffset? toExclusive = null,
        CancellationToken cancellationToken = default);

    Task<OrderReceiptSummary?> GetOrderReceiptAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    Task<SalesReportSummary> GetSalesReportAsync(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken = default);

    Task<int> StartOrderAsync(
        int tableId,
        CancellationToken cancellationToken = default);

    Task AddItemAsync(
        int orderId,
        int productId,
        int quantity,
        string? notes,
        CancellationToken cancellationToken = default);

    Task UpdateItemAsync(
        int orderId,
        int orderDetailId,
        int quantity,
        string? notes,
        CancellationToken cancellationToken = default);

    Task RemoveItemAsync(
        int orderId,
        int orderDetailId,
        CancellationToken cancellationToken = default);

    Task CancelOrderAsync(
        int orderId,
        string reason,
        CancellationToken cancellationToken = default);

    Task CompletePaymentAsync(
        int orderId,
        PaymentMethod method,
        CancellationToken cancellationToken = default);
}
