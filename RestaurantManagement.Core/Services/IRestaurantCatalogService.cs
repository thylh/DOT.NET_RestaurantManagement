using RestaurantManagement.Core.Models;

namespace RestaurantManagement.Core.Services;

public interface IRestaurantCatalogService
{
    Task<IReadOnlyList<RestaurantTableSummary>> GetTablesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategorySummary>> GetCategoriesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductSummary>> GetProductsAsync(
        string? searchText = null,
        int? categoryId = null,
        CancellationToken cancellationToken = default);

    Task CreateTableAsync(
        int tableNumber,
        int seatCount,
        CancellationToken cancellationToken = default);

    Task UpdateTableAsync(
        int tableId,
        int tableNumber,
        int seatCount,
        CancellationToken cancellationToken = default);

    Task SetTableActiveAsync(
        int tableId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task DeleteTableAsync(int tableId, CancellationToken cancellationToken = default);

    Task CreateCategoryAsync(
        string name,
        string? description,
        CancellationToken cancellationToken = default);

    Task UpdateCategoryAsync(
        int categoryId,
        string name,
        string? description,
        CancellationToken cancellationToken = default);

    Task SetCategoryActiveAsync(
        int categoryId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task DeleteCategoryAsync(int categoryId, CancellationToken cancellationToken = default);

    Task CreateProductAsync(
        string name,
        string? description,
        decimal price,
        int categoryId,
        string? imagePath,
        CancellationToken cancellationToken = default);

    Task UpdateProductAsync(
        int productId,
        string name,
        string? description,
        decimal price,
        int categoryId,
        string? imagePath,
        CancellationToken cancellationToken = default);

    Task SetProductAvailabilityAsync(
        int productId,
        bool isAvailable,
        CancellationToken cancellationToken = default);

    Task SetProductActiveAsync(
        int productId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task DeleteProductAsync(int productId, CancellationToken cancellationToken = default);
}
