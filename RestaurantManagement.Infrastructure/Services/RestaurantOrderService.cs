using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using RestaurantManagement.Core.Entities;
using RestaurantManagement.Core.Enums;
using RestaurantManagement.Core.Models;
using RestaurantManagement.Core.Services;
using RestaurantManagement.Infrastructure.Data;

namespace RestaurantManagement.Infrastructure.Services;

public sealed class RestaurantOrderService(
    RestaurantDbContext dbContext,
    ICurrentUserSession currentUserSession) : IRestaurantOrderService
{
    public async Task<OpenOrderSummary?> GetOpenOrderAsync(
        int tableId,
        CancellationToken cancellationToken = default)
    {
        await GetAuthorizedUserAsync(cancellationToken);
        var order = await dbContext.RestaurantOrders
            .AsNoTracking()
            .Include(candidate => candidate.OrderDetails)
                .ThenInclude(detail => detail.Product)
            .SingleOrDefaultAsync(
                candidate => candidate.RestaurantTableId == tableId
                    && candidate.Status == OrderStatus.Open,
                cancellationToken);
        if (order is null)
        {
            return null;
        }

        var promotion = await GetBestPromotionAsync(
            order.OrderDetails, DateTimeOffset.UtcNow, cancellationToken);
        return ToSummary(order, promotion);
    }

    public async Task<int> StartOrderAsync(
        int tableId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetAuthorizedUserAsync(cancellationToken);
        var table = await dbContext.RestaurantTables.SingleOrDefaultAsync(
            candidate => candidate.Id == tableId,
            cancellationToken);
        if (table is null)
        {
            throw new InvalidOperationException("Không tìm thấy bàn cần tạo order.");
        }

        if (!table.IsActive)
        {
            throw new InvalidOperationException("Không thể tạo order cho bàn đang ngừng phục vụ.");
        }

        if (await dbContext.RestaurantOrders.AnyAsync(
                order => order.RestaurantTableId == tableId && order.Status == OrderStatus.Open,
                cancellationToken))
        {
            throw new InvalidOperationException("Bàn này đã có order đang mở.");
        }

        var employeeId = user.EmployeeId
            ?? throw new UnauthorizedAccessException(
                "Chỉ nhân viên có hồ sơ đang hoạt động mới được thao tác order.");
        var employeeName = await dbContext.Employees
            .Where(employee => employee.Id == employeeId)
            .Select(employee => employee.FullName)
            .SingleAsync(cancellationToken);

        var order = new RestaurantOrder
        {
            RestaurantTableId = tableId,
            CreatedByEmployeeId = employeeId,
            CreatedByUserId = user.Id,
            CreatedByEmployeeNameSnapshot = employeeName
        };
        dbContext.RestaurantOrders.Add(order);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            dbContext.Entry(order).State = EntityState.Detached;
            throw new InvalidOperationException(
                "Bàn vừa được tạo order ở một thao tác khác. Hãy tải lại danh sách bàn.",
                exception);
        }

        return order.Id;
    }

    public async Task AddItemAsync(
        int orderId,
        int productId,
        int quantity,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        await GetAuthorizedUserAsync(cancellationToken);
        ValidateQuantity(quantity);
        var normalizedNotes = NormalizeNotes(notes);

        var order = await dbContext.RestaurantOrders
            .SingleOrDefaultAsync(
                candidate => candidate.Id == orderId && candidate.Status == OrderStatus.Open,
                cancellationToken);
        if (order is null)
        {
            throw new InvalidOperationException("Order không tồn tại hoặc đã đóng.");
        }

        var product = await dbContext.Products
            .Include(candidate => candidate.Category)
            .SingleOrDefaultAsync(candidate => candidate.Id == productId, cancellationToken);
        if (product is null || !product.IsActive || !product.IsAvailable || !product.Category.IsActive)
        {
            throw new InvalidOperationException("Món ăn không còn được phép gọi.");
        }

        dbContext.OrderDetails.Add(new OrderDetail
        {
            RestaurantOrderId = orderId,
            ProductId = productId,
            ProductNameSnapshot = product.Name,
            UnitPriceSnapshot = product.Price,
            Quantity = quantity,
            Notes = normalizedNotes
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateItemAsync(
        int orderId,
        int orderDetailId,
        int quantity,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        await GetAuthorizedUserAsync(cancellationToken);
        ValidateQuantity(quantity);
        var normalizedNotes = NormalizeNotes(notes);
        var detail = await dbContext.OrderDetails
            .SingleOrDefaultAsync(
                candidate => candidate.Id == orderDetailId
                    && candidate.RestaurantOrderId == orderId
                    && candidate.RestaurantOrder.Status == OrderStatus.Open,
                cancellationToken);
        if (detail is null)
        {
            throw new InvalidOperationException(
                "Món không tồn tại trong order đang mở hoặc order đã đóng.");
        }

        detail.Quantity = quantity;
        detail.Notes = normalizedNotes;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveItemAsync(
        int orderId,
        int orderDetailId,
        CancellationToken cancellationToken = default)
    {
        await GetAuthorizedUserAsync(cancellationToken);
        var detail = await dbContext.OrderDetails
            .SingleOrDefaultAsync(
                candidate => candidate.Id == orderDetailId
                    && candidate.RestaurantOrderId == orderId
                    && candidate.RestaurantOrder.Status == OrderStatus.Open,
                cancellationToken);
        if (detail is null)
        {
            throw new InvalidOperationException(
                "Món không tồn tại trong order đang mở hoặc order đã đóng.");
        }

        dbContext.OrderDetails.Remove(detail);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelOrderAsync(
        int orderId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var user = await GetAuthorizedUserAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Hãy nhập lý do hủy order.", nameof(reason));
        }

        var normalizedReason = reason.Trim();
        if (normalizedReason.Length > 500)
        {
            throw new ArgumentException("Lý do hủy không được vượt quá 500 ký tự.", nameof(reason));
        }

        var order = await dbContext.RestaurantOrders.SingleOrDefaultAsync(
            candidate => candidate.Id == orderId && candidate.Status == OrderStatus.Open,
            cancellationToken);
        if (order is null)
        {
            throw new InvalidOperationException("Order không tồn tại hoặc đã đóng.");
        }

        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = DateTimeOffset.UtcNow;
        order.CancelledByUserId = user.Id;
        order.CancellationReason = normalizedReason;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CompletePaymentAsync(
        int orderId,
        PaymentMethod method,
        CancellationToken cancellationToken = default)
    {
        var user = await GetAuthorizedUserAsync(cancellationToken);
        if (method is not PaymentMethod.Cash and not PaymentMethod.BankTransfer)
        {
            throw new ArgumentOutOfRangeException(nameof(method), "Hãy chọn phương thức thanh toán hợp lệ.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var order = await dbContext.RestaurantOrders
            .Include(candidate => candidate.OrderDetails)
                .ThenInclude(detail => detail.Product)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == orderId && candidate.Status == OrderStatus.Open,
                cancellationToken);
        if (order is null)
        {
            throw new InvalidOperationException("Order không tồn tại hoặc đã được thanh toán/hủy.");
        }

        if (order.OrderDetails.Count == 0)
        {
            throw new InvalidOperationException("Không thể thanh toán order chưa có món.");
        }

        var subtotal = order.OrderDetails.Sum(detail => detail.UnitPriceSnapshot * detail.Quantity);
        var promotion = await GetBestPromotionAsync(
            order.OrderDetails, DateTimeOffset.UtcNow, cancellationToken);
        var discount = promotion?.DiscountAmount ?? 0;
        var total = subtotal - discount;
        order.AppliedPromotionNameSnapshot = promotion?.Name;
        order.DiscountAmountSnapshot = discount;
        dbContext.Payments.Add(new Payment
        {
            RestaurantOrderId = orderId,
            Amount = total,
            Method = method,
            PaidByUserId = user.Id
        });
        order.Status = OrderStatus.Paid;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<AuthenticatedUser> GetAuthorizedUserAsync(CancellationToken cancellationToken)
    {
        var user = currentUserSession.User
            ?? throw new UnauthorizedAccessException("Hãy đăng nhập để thao tác với order.");
        var account = await dbContext.UserAccounts
            .Where(candidate => candidate.Id == user.Id && !candidate.IsDeleted)
            .Select(candidate => new { candidate.Role, candidate.EmployeeId })
            .SingleOrDefaultAsync(
                cancellationToken);
        if (account is null
            || account.Role != user.Role
            || account.EmployeeId != user.EmployeeId)
        {
            throw new UnauthorizedAccessException("Tài khoản không còn quyền thao tác.");
        }

        if (user.Role != UserRole.Staff || user.EmployeeId is not { } employeeId)
        {
            throw new UnauthorizedAccessException(
                "Chỉ nhân viên có hồ sơ đang hoạt động mới được thao tác order.");
        }

        if (!await dbContext.Employees.AnyAsync(
                employee => employee.Id == employeeId && employee.IsActive,
                cancellationToken))
        {
            throw new UnauthorizedAccessException("Hồ sơ nhân viên không còn hoạt động.");
        }

        return user;
    }

    private static OpenOrderSummary ToSummary(
        RestaurantOrder order,
        PromotionApplication? promotion) =>
        new(
            order.Id,
            order.RestaurantTableId,
            order.OrderDetails
                .OrderBy(detail => detail.Id)
                .Select(detail => new OrderDetailSummary(
                    detail.Id,
                    detail.ProductNameSnapshot,
                    detail.UnitPriceSnapshot,
                    detail.Quantity,
                    detail.Notes))
                .ToList(),
            promotion?.Name,
            promotion?.DiscountAmount ?? 0);

    private async Task<PromotionApplication?> GetBestPromotionAsync(
        ICollection<OrderDetail> details,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (details.Count == 0)
        {
            return null;
        }

        var productIds = details.Select(detail => detail.ProductId).Distinct().ToArray();
        var categoryIds = details
            .Select(detail => detail.Product.CategoryId)
            .Distinct()
            .ToArray();
        var promotions = await dbContext.Promotions
            .AsNoTracking()
            .Where(promotion => promotion.IsActive
                && promotion.StartsAt <= now
                && promotion.EndsAt > now
                && ((promotion.ProductId.HasValue && productIds.Contains(promotion.ProductId.Value))
                    || (promotion.CategoryId.HasValue && categoryIds.Contains(promotion.CategoryId.Value))))
            .ToListAsync(cancellationToken);

        return promotions
            .Select(promotion =>
            {
                var eligibleSubtotal = details
                    .Where(detail => promotion.ProductId == detail.ProductId
                        || promotion.CategoryId == detail.Product.CategoryId)
                    .Sum(detail => detail.UnitPriceSnapshot * detail.Quantity);
                var discount = decimal.Round(
                    eligibleSubtotal * promotion.DiscountPercentage / 100m,
                    2,
                    MidpointRounding.AwayFromZero);
                return new PromotionApplication(promotion.Id, promotion.Name, discount);
            })
            .Where(candidate => candidate.DiscountAmount > 0)
            .OrderByDescending(candidate => candidate.DiscountAmount)
            .ThenBy(candidate => candidate.Id)
            .FirstOrDefault();
    }

    private sealed record PromotionApplication(int Id, string Name, decimal DiscountAmount);

    private static void ValidateQuantity(int quantity)
    {
        if (quantity is < 1 or > 99)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Số lượng phải từ 1 đến 99.");
        }
    }

    private static string? NormalizeNotes(string? notes)
    {
        var normalizedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (normalizedNotes?.Length > 500)
        {
            throw new ArgumentException("Ghi chú không được vượt quá 500 ký tự.", nameof(notes));
        }

        return normalizedNotes;
    }

    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is MySqlException { Number: 1062 };
}
