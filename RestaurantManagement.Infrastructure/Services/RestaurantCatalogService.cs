using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using RestaurantManagement.Core.Entities;
using RestaurantManagement.Core.Enums;
using RestaurantManagement.Core.Models;
using RestaurantManagement.Core.Services;
using RestaurantManagement.Infrastructure.Data;

namespace RestaurantManagement.Infrastructure.Services;

public sealed class RestaurantCatalogService(
    RestaurantDbContext dbContext,
    ICurrentUserSession currentUserSession) : IRestaurantCatalogService
{
    public async Task<IReadOnlyList<RestaurantTableSummary>> GetTablesAsync(
        CancellationToken cancellationToken = default)
    {
        var isAdmin = currentUserSession.User?.Role == UserRole.Admin;
        EnsureSignedIn();

        return await dbContext.RestaurantTables
            .AsNoTracking()
            .Where(table => isAdmin || table.IsActive)
            .OrderBy(table => table.TableNumber)
            .Select(table => new RestaurantTableSummary(
                table.Id,
                table.TableNumber,
                table.SeatCount,
                table.IsActive,
                table.Orders.Any(order => order.Status == OrderStatus.Open)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CategorySummary>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var isAdmin = currentUserSession.User?.Role == UserRole.Admin;
        EnsureSignedIn();

        return await dbContext.Categories
            .AsNoTracking()
            .Where(category => isAdmin || category.IsActive)
            .OrderBy(category => category.Name)
            .Select(category => new CategorySummary(
                category.Id,
                category.Name,
                category.Description,
                category.IsActive,
                category.Products.Count()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductSummary>> GetProductsAsync(
        string? searchText = null,
        int? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = currentUserSession.User?.Role == UserRole.Admin;
        EnsureSignedIn();

        var products = dbContext.Products
            .AsNoTracking()
            .Where(product => isAdmin || (product.IsActive && product.Category.IsActive));
        if (categoryId.HasValue)
        {
            products = products.Where(product => product.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var search = searchText.Trim();
            products = products.Where(product =>
                product.Name.Contains(search)
                || (product.Description != null && product.Description.Contains(search))
                || product.Category.Name.Contains(search));
        }

        return await products
            .OrderBy(product => product.Category.Name)
            .ThenBy(product => product.Name)
            .Select(product => new ProductSummary(
                product.Id,
                product.Name,
                product.Description,
                product.ImagePath,
                product.Price,
                product.IsAvailable,
                product.IsActive,
                product.CategoryId,
                product.Category.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task CreateTableAsync(
        int tableNumber,
        int seatCount,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        ValidateTable(tableNumber, seatCount);
        dbContext.RestaurantTables.Add(new RestaurantTable
        {
            TableNumber = tableNumber,
            SeatCount = seatCount
        });
        await SaveChangesAsync("Số bàn này đã được sử dụng.", cancellationToken);
    }

    public async Task UpdateTableAsync(
        int tableId,
        int tableNumber,
        int seatCount,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        ValidateTable(tableNumber, seatCount);
        var table = await dbContext.RestaurantTables.SingleOrDefaultAsync(
            candidate => candidate.Id == tableId,
            cancellationToken);
        if (table is null)
        {
            throw new InvalidOperationException("Không tìm thấy bàn cần cập nhật.");
        }

        table.TableNumber = tableNumber;
        table.SeatCount = seatCount;
        await SaveChangesAsync("Số bàn này đã được sử dụng.", cancellationToken);
    }

    public async Task SetTableActiveAsync(
        int tableId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var table = await dbContext.RestaurantTables.SingleOrDefaultAsync(
            candidate => candidate.Id == tableId,
            cancellationToken);
        if (table is null)
        {
            throw new InvalidOperationException("Không tìm thấy bàn cần cập nhật.");
        }

        if (!isActive && await dbContext.RestaurantOrders.AnyAsync(
                order => order.RestaurantTableId == tableId && order.Status == OrderStatus.Open,
                cancellationToken))
        {
            throw new InvalidOperationException("Không thể ngừng sử dụng bàn đang có order mở.");
        }

        table.IsActive = isActive;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTableAsync(int tableId, CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var table = await dbContext.RestaurantTables.SingleOrDefaultAsync(
            candidate => candidate.Id == tableId,
            cancellationToken);
        if (table is null)
        {
            throw new InvalidOperationException("Không tìm thấy bàn cần xóa.");
        }

        if (await dbContext.RestaurantOrders.AnyAsync(
                order => order.RestaurantTableId == tableId,
                cancellationToken))
        {
            throw new InvalidOperationException("Bàn đã có lịch sử order. Hãy ngừng sử dụng thay vì xóa.");
        }

        dbContext.RestaurantTables.Remove(table);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateCategoryAsync(
        string name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        dbContext.Categories.Add(new Category
        {
            Name = ValidateRequiredText(name, 100, nameof(name)),
            Description = NormalizeOptionalText(description, 500, nameof(description))
        });
        await SaveChangesAsync("Tên danh mục này đã được sử dụng.", cancellationToken);
    }

    public async Task UpdateCategoryAsync(
        int categoryId,
        string name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var category = await dbContext.Categories.SingleOrDefaultAsync(
            candidate => candidate.Id == categoryId,
            cancellationToken);
        if (category is null)
        {
            throw new InvalidOperationException("Không tìm thấy danh mục cần cập nhật.");
        }

        category.Name = ValidateRequiredText(name, 100, nameof(name));
        category.Description = NormalizeOptionalText(description, 500, nameof(description));
        await SaveChangesAsync("Tên danh mục này đã được sử dụng.", cancellationToken);
    }

    public async Task SetCategoryActiveAsync(
        int categoryId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var category = await dbContext.Categories.SingleOrDefaultAsync(
            candidate => candidate.Id == categoryId,
            cancellationToken);
        if (category is null)
        {
            throw new InvalidOperationException("Không tìm thấy danh mục cần cập nhật.");
        }

        category.IsActive = isActive;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var category = await dbContext.Categories.SingleOrDefaultAsync(
            candidate => candidate.Id == categoryId,
            cancellationToken);
        if (category is null)
        {
            throw new InvalidOperationException("Không tìm thấy danh mục cần xóa.");
        }

        if (await dbContext.Products.AnyAsync(product => product.CategoryId == categoryId, cancellationToken))
        {
            throw new InvalidOperationException("Danh mục đang có món ăn. Hãy ngừng kinh doanh danh mục thay vì xóa.");
        }

        dbContext.Categories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateProductAsync(
        string name,
        string? description,
        decimal price,
        int categoryId,
        string? imagePath,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        ValidatePrice(price);
        await EnsureActiveCategoryAsync(categoryId, cancellationToken);
        var product = new Product
        {
            Name = ValidateRequiredText(name, 150, nameof(name)),
            Description = NormalizeOptionalText(description, 1000, nameof(description)),
            Price = price,
            CategoryId = categoryId,
            ImagePath = NormalizeOptionalText(imagePath, 500, nameof(imagePath))
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateProductAsync(
        int productId,
        string name,
        string? description,
        decimal price,
        int categoryId,
        string? imagePath,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        ValidatePrice(price);
        var product = await dbContext.Products.SingleOrDefaultAsync(
            candidate => candidate.Id == productId,
            cancellationToken);
        if (product is null)
        {
            throw new InvalidOperationException("Không tìm thấy món ăn cần cập nhật.");
        }

        await EnsureActiveCategoryAsync(categoryId, cancellationToken);
        product.Name = ValidateRequiredText(name, 150, nameof(name));
        product.Description = NormalizeOptionalText(description, 1000, nameof(description));
        product.Price = price;
        product.CategoryId = categoryId;
        product.ImagePath = NormalizeOptionalText(imagePath, 500, nameof(imagePath));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetProductAvailabilityAsync(
        int productId,
        bool isAvailable,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var product = await dbContext.Products.SingleOrDefaultAsync(
            candidate => candidate.Id == productId,
            cancellationToken);
        if (product is null)
        {
            throw new InvalidOperationException("Không tìm thấy món ăn cần cập nhật.");
        }

        product.IsAvailable = isAvailable;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetProductActiveAsync(
        int productId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var product = await dbContext.Products.SingleOrDefaultAsync(
            candidate => candidate.Id == productId,
            cancellationToken);
        if (product is null)
        {
            throw new InvalidOperationException("Không tìm thấy món ăn cần cập nhật.");
        }

        product.IsActive = isActive;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteProductAsync(int productId, CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var product = await dbContext.Products.SingleOrDefaultAsync(
            candidate => candidate.Id == productId,
            cancellationToken);
        if (product is null)
        {
            throw new InvalidOperationException("Không tìm thấy món ăn cần xóa.");
        }

        if (await dbContext.OrderDetails.AnyAsync(detail => detail.ProductId == productId, cancellationToken))
        {
            throw new InvalidOperationException("Món ăn đã xuất hiện trong order. Hãy ngừng kinh doanh thay vì xóa.");
        }

        dbContext.Products.Remove(product);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureActiveCategoryAsync(int categoryId, CancellationToken cancellationToken)
    {
        var categoryIsActive = await dbContext.Categories.AnyAsync(
            category => category.Id == categoryId && category.IsActive,
            cancellationToken);
        if (!categoryIsActive)
        {
            throw new InvalidOperationException("Hãy chọn một danh mục đang kinh doanh.");
        }
    }

    private async Task SaveChangesAsync(string duplicateMessage, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            foreach (var entry in exception.Entries)
            {
                entry.State = EntityState.Detached;
            }

            throw new InvalidOperationException(duplicateMessage, exception);
        }
    }

    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private void EnsureSignedIn()
    {
        if (currentUserSession.User is null)
        {
            throw new UnauthorizedAccessException("Vui lòng đăng nhập để xem dữ liệu nhà hàng.");
        }
    }

    private void EnsureAdmin()
    {
        if (currentUserSession.User?.Role != UserRole.Admin)
        {
            throw new UnauthorizedAccessException("Chỉ Admin mới được quản lý bàn và thực đơn.");
        }
    }

    private static void ValidateTable(int tableNumber, int seatCount)
    {
        if (tableNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tableNumber), "Số bàn phải lớn hơn 0.");
        }

        if (seatCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seatCount), "Số chỗ ngồi phải lớn hơn 0.");
        }
    }

    private static void ValidatePrice(decimal price)
    {
        if (price <= 0 || price > 999_999_999_999_999.99m)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Giá bán phải lớn hơn 0 và không vượt quá giới hạn.");
        }
    }

    private static string ValidateRequiredText(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Thông tin này không được để trống.", parameterName);
        }
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Nội dung không được vượt quá {maxLength} ký tự.", parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(string? value, int maxLength, string parameterName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Nội dung không được vượt quá {maxLength} ký tự.", parameterName);
        }

        return normalized;
    }
}
