using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
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
            .OrderBy(table => table.Area)
            .ThenBy(table => table.TableNumber)
            .Select(table => new RestaurantTableSummary(
                table.Id,
                table.TableNumber,
                table.Area,
                table.SeatCount,
                table.IsActive,
                table.Orders.Any(order => order.Status == OrderStatus.Open),
                table.Orders
                    .Where(order => order.Status == OrderStatus.Open)
                    .Select(order => (int?)order.Id)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    private static string? NormalizeProductTags(string? tags)
    {
        var normalizedTags = tags?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedTags is null || normalizedTags.Length == 0)
        {
            return null;
        }

        var normalized = string.Join(", ", normalizedTags);
        if (normalized.Length > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(tags), "Tổng độ dài nhãn không được vượt quá 500 ký tự.");
        }

        return normalized;
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
                || (product.Tags != null && product.Tags.Contains(search))
                || product.Category.Name.Contains(search));
        }

        return await products
            .OrderBy(product => product.Category.Name)
            .ThenBy(product => product.Name)
            .Select(product => new ProductSummary(
                product.Id,
                product.Name,
                product.ImagePath,
                product.Tags,
                product.Price,
                product.IsAvailable,
                product.IsActive,
                product.CategoryId,
                product.Category.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PromotionSummary>> GetPromotionsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        return await dbContext.Promotions
            .AsNoTracking()
            .OrderBy(promotion => promotion.Name)
            .Select(promotion => new PromotionSummary(
                promotion.Id,
                promotion.Name,
                promotion.DiscountPercentage,
                promotion.StartsAt,
                promotion.EndsAt,
                promotion.IsActive,
                promotion.ProductId,
                promotion.Product == null ? null : promotion.Product.Name,
                promotion.CategoryId,
                promotion.Category == null ? null : promotion.Category.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task CreatePromotionAsync(
        string name,
        decimal discountPercentage,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        int? productId,
        int? categoryId,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        await ValidatePromotionAsync(
            discountPercentage, startsAt, endsAt, productId, categoryId, cancellationToken);
        dbContext.Promotions.Add(new Promotion
        {
            Name = ValidateRequiredText(name, 100, nameof(name)),
            DiscountPercentage = discountPercentage,
            StartsAt = startsAt.ToUniversalTime(),
            EndsAt = endsAt.ToUniversalTime(),
            ProductId = productId,
            CategoryId = categoryId
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePromotionAsync(
        int promotionId,
        string name,
        decimal discountPercentage,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        int? productId,
        int? categoryId,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var promotion = await dbContext.Promotions.SingleOrDefaultAsync(
            candidate => candidate.Id == promotionId,
            cancellationToken);
        if (promotion is null)
        {
            throw new InvalidOperationException("Không tìm thấy khuyến mãi cần cập nhật.");
        }

        await ValidatePromotionAsync(
            discountPercentage, startsAt, endsAt, productId, categoryId, cancellationToken);
        promotion.Name = ValidateRequiredText(name, 100, nameof(name));
        promotion.DiscountPercentage = discountPercentage;
        promotion.StartsAt = startsAt.ToUniversalTime();
        promotion.EndsAt = endsAt.ToUniversalTime();
        promotion.ProductId = productId;
        promotion.CategoryId = categoryId;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetPromotionActiveAsync(
        int promotionId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var promotion = await dbContext.Promotions.SingleOrDefaultAsync(
            candidate => candidate.Id == promotionId,
            cancellationToken);
        if (promotion is null)
        {
            throw new InvalidOperationException("Không tìm thấy khuyến mãi cần cập nhật.");
        }

        promotion.IsActive = isActive;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateTableAsync(
        string tableNumber,
        int seatCount,
        string area = "Chung",
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var normalizedTableNumber = ValidateTable(tableNumber, area, seatCount);
        dbContext.RestaurantTables.Add(new RestaurantTable
        {
            TableNumber = normalizedTableNumber,
            Area = area.Trim(),
            SeatCount = seatCount
        });
        await SaveChangesAsync(
            "Số hiệu bàn này đã được sử dụng trong khu vực đã chọn.",
            cancellationToken);
    }

    public async Task UpdateTableAsync(
        int tableId,
        string tableNumber,
        int seatCount,
        string area = "Chung",
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var normalizedTableNumber = ValidateTable(tableNumber, area, seatCount);
        var table = await dbContext.RestaurantTables.SingleOrDefaultAsync(
            candidate => candidate.Id == tableId,
            cancellationToken);
        if (table is null)
        {
            throw new InvalidOperationException("Không tìm thấy bàn cần cập nhật.");
        }

        if (await dbContext.RestaurantOrders.AnyAsync(
                order => order.RestaurantTableId == tableId && order.Status == OrderStatus.Open,
                cancellationToken))
        {
            throw new InvalidOperationException("Không thể thay đổi bố trí bàn đang có order mở.");
        }

        table.TableNumber = normalizedTableNumber;
        table.Area = area.Trim();
        table.SeatCount = seatCount;
        await SaveChangesAsync(
            "Số hiệu bàn này đã được sử dụng trong khu vực đã chọn.",
            cancellationToken);
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
            throw new InvalidOperationException("Không thể ngừng phục vụ bàn đang có order mở.");
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
            throw new InvalidOperationException(
                "Không thể xóa bàn đã phát sinh order. Hãy ngừng phục vụ bàn nếu không còn sử dụng.");
        }

        dbContext.RestaurantTables.Remove(table);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateCategoryAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        dbContext.Categories.Add(new Category
        {
            Name = ValidateRequiredText(name, 100, nameof(name))
        });
        await SaveChangesAsync("Tên danh mục này đã được sử dụng.", cancellationToken);
    }

    public async Task UpdateCategoryAsync(
        int categoryId,
        string name,
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

        if (await dbContext.Promotions.AnyAsync(
                promotion => promotion.CategoryId == categoryId, cancellationToken))
        {
            throw new InvalidOperationException(
                "Danh mục đang được khuyến mãi tham chiếu. Hãy ngừng chương trình trước khi xóa danh mục.");
        }

        dbContext.Categories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateProductAsync(
        string name,
        string? tags,
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
            Tags = NormalizeProductTags(tags),
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
        string? tags,
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
        product.Tags = NormalizeProductTags(tags);
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
        await EnsureCanManageProductAvailabilityAsync(cancellationToken);
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

    private async Task EnsureCanManageProductAvailabilityAsync(CancellationToken cancellationToken)
    {
        var user = currentUserSession.User
            ?? throw new UnauthorizedAccessException("Vui lòng đăng nhập để cập nhật tình trạng món.");
        if (user.Role == UserRole.Admin)
        {
            return;
        }

        if (user.Role != UserRole.Staff
            || user.EmployeeId is not { } employeeId
            || !await dbContext.UserAccounts.AnyAsync(
                account => account.Id == user.Id
                    && account.Role == UserRole.Staff
                    && !account.IsDeleted
                    && account.EmployeeId == employeeId
                    && !account.EmployeeProfileDeleted
                    && account.Employee != null
                    && account.Employee.IsActive,
                cancellationToken))
        {
            throw new UnauthorizedAccessException(
                "Chỉ Admin hoặc nhân viên có hồ sơ đang hoạt động mới được cập nhật tình trạng món.");
        }
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

        if (await dbContext.Promotions.AnyAsync(
                promotion => promotion.ProductId == productId, cancellationToken))
        {
            throw new InvalidOperationException(
                "Món ăn đang được khuyến mãi tham chiếu. Hãy ngừng chương trình trước khi xóa món.");
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

    private async Task ValidatePromotionAsync(
        decimal discountPercentage,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        int? productId,
        int? categoryId,
        CancellationToken cancellationToken)
    {
        if (discountPercentage is <= 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(discountPercentage), "Mức giảm phải lớn hơn 0% và không vượt quá 100%.");
        }

        if (endsAt <= startsAt)
        {
            throw new ArgumentException("Thời gian kết thúc phải sau thời gian bắt đầu.", nameof(endsAt));
        }

        if (productId.HasValue == categoryId.HasValue)
        {
            throw new ArgumentException("Khuyến mãi phải áp dụng cho đúng một món hoặc một danh mục.");
        }

        var targetExists = productId is { } selectedProductId
            ? await dbContext.Products.AnyAsync(
                product => product.Id == selectedProductId, cancellationToken)
            : await dbContext.Categories.AnyAsync(
                category => category.Id == categoryId, cancellationToken);
        if (!targetExists)
        {
            throw new InvalidOperationException("Món ăn hoặc danh mục áp dụng không còn tồn tại.");
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
        exception.InnerException is MySqlException { Number: 1062 };

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

    private static string ValidateTable(string tableNumber, string area, int seatCount)
    {
        if (seatCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seatCount), "Số chỗ ngồi phải lớn hơn 0.");
        }

        _ = ValidateRequiredText(area, 100, nameof(area));
        return ValidateRequiredText(tableNumber, 50, nameof(tableNumber));
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
