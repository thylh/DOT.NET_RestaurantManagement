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

    Task<IReadOnlyList<PromotionSummary>> GetPromotionsAsync(
        CancellationToken cancellationToken = default);

    Task CreatePromotionAsync(
        string name,
        decimal discountPercentage,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        int? productId,
        int? categoryId,
        CancellationToken cancellationToken = default);

    Task UpdatePromotionAsync(
        int promotionId,
        string name,
        decimal discountPercentage,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        int? productId,
        int? categoryId,
        CancellationToken cancellationToken = default);

    Task SetPromotionActiveAsync(
        int promotionId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task CreateTableAsync(
        string tableNumber,
        int seatCount,
        string area = "Chung",
        CancellationToken cancellationToken = default);

    Task UpdateTableAsync(
        int tableId,
        string tableNumber,
        int seatCount,
        string area = "Chung",
        CancellationToken cancellationToken = default);

    Task SetTableActiveAsync(
        int tableId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task DeleteTableAsync(int tableId, CancellationToken cancellationToken = default);

    Task CreateCategoryAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task UpdateCategoryAsync(
        int categoryId,
        string name,
        CancellationToken cancellationToken = default);

    Task SetCategoryActiveAsync(
        int categoryId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task DeleteCategoryAsync(int categoryId, CancellationToken cancellationToken = default);

    Task CreateProductAsync(
        string name,
        string? tags,
        decimal price,
        int categoryId,
        string? imagePath,
        CancellationToken cancellationToken = default);

    Task UpdateProductAsync(
        int productId,
        string name,
        string? tags,
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
