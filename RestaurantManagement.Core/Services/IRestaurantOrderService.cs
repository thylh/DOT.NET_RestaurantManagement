using RestaurantManagement.Core.Enums;
using RestaurantManagement.Core.Models;

namespace RestaurantManagement.Core.Services;

public interface IRestaurantOrderService
{
    Task<OpenOrderSummary?> GetOpenOrderAsync(
        int tableId,
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

    Task CancelOrderAsync(
        int orderId,
        string reason,
        CancellationToken cancellationToken = default);

    Task CompletePaymentAsync(
        int orderId,
        PaymentMethod method,
        CancellationToken cancellationToken = default);
}
