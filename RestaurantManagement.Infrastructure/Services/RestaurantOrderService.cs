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

    public async Task<IReadOnlyList<OrderHistorySummary>> GetOrderHistoryAsync(
        int? orderId = null,
        DateTimeOffset? fromInclusive = null,
        DateTimeOffset? toExclusive = null,
        CancellationToken cancellationToken = default)
    {
        var user = await GetAuthorizedHistoryReaderAsync(cancellationToken);
        ValidateDateRange(fromInclusive, toExclusive);
        if (orderId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(orderId), "Mã order phải lớn hơn 0.");
        }

        var query = dbContext.RestaurantOrders
            .AsNoTracking()
            .Include(order => order.RestaurantTable)
            .Include(order => order.OrderDetails)
            .Include(order => order.Payment)
            .AsQueryable();
        if (user.Role == UserRole.Staff)
        {
            query = query.Where(order => order.Status == OrderStatus.Paid);
        }

        if (orderId is { } id)
        {
            query = query.Where(order => order.Id == id);
        }

        if (fromInclusive is { } start)
        {
            query = query.Where(order => order.CreatedAt >= start);
        }

        if (toExclusive is { } end)
        {
            query = query.Where(order => order.CreatedAt < end);
        }

        var orders = await query
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(cancellationToken);
        return orders.Select(ToHistorySummary).ToArray();
    }

    public async Task<OrderReceiptSummary?> GetOrderReceiptAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetAuthorizedHistoryReaderAsync(cancellationToken);
        if (orderId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(orderId), "Mã order phải lớn hơn 0.");
        }

        var receiptQuery = dbContext.RestaurantOrders
            .AsNoTracking()
            .Include(candidate => candidate.RestaurantTable)
            .Include(candidate => candidate.OrderDetails)
            .Include(candidate => candidate.Payment)
            .AsQueryable();
        if (user.Role == UserRole.Staff)
        {
            receiptQuery = receiptQuery.Where(candidate => candidate.Status == OrderStatus.Paid);
        }

        var order = await receiptQuery.SingleOrDefaultAsync(
            candidate => candidate.Id == orderId,
            cancellationToken);
        if (order is null)
        {
            return null;
        }

        var history = ToHistorySummary(order);
        return new OrderReceiptSummary(
            history.OrderId,
            history.TableNumber,
            history.Area,
            history.EmployeeName,
            history.CreatedAt,
            history.Status,
            order.AppliedPromotionNameSnapshot,
            history.Subtotal,
            history.DiscountAmount,
            history.Total,
            history.PaymentMethod,
            history.PaidAt,
            history.CancellationReason,
            order.OrderDetails
                .OrderBy(detail => detail.Id)
                .Select(detail => new OrderDetailSummary(
                    detail.Id,
                    detail.ProductNameSnapshot,
                    detail.UnitPriceSnapshot,
                    detail.Quantity,
                    detail.Notes,
                    detail.ProductId))
                .ToArray());
    }

    public async Task<SalesReportSummary> GetSalesReportAsync(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken = default)
    {
        await GetAuthorizedAdminAsync(cancellationToken);
        ValidateDateRange(fromInclusive, toExclusive);

        var payments = await dbContext.Payments
            .AsNoTracking()
            .Where(payment => payment.Status == PaymentStatus.Completed
                && payment.RestaurantOrder.Status == OrderStatus.Paid
                && payment.PaidAt >= fromInclusive
                && payment.PaidAt < toExclusive)
            .Include(payment => payment.RestaurantOrder)
                .ThenInclude(order => order.OrderDetails)
            .ToListAsync(cancellationToken);

        var paymentMethods = payments
            .GroupBy(payment => payment.Method)
            .OrderBy(group => group.Key)
            .Select(group => new PaymentMethodRevenueSummary(
                group.Key,
                group.Count(),
                group.Sum(payment => payment.Amount)))
            .ToArray();
        var topSellingProducts = payments
            .SelectMany(payment => payment.RestaurantOrder.OrderDetails)
            .GroupBy(detail => detail.ProductNameSnapshot)
            .Select(group => new TopSellingProductSummary(
                group.Key,
                group.Sum(detail => detail.Quantity),
                group.Sum(detail => detail.UnitPriceSnapshot * detail.Quantity)))
            .OrderByDescending(product => product.QuantitySold)
            .ThenByDescending(product => product.GrossRevenue)
            .ThenBy(product => product.ProductName)
            .Take(10)
            .ToArray();
        var revenueByDate = payments
            .GroupBy(payment => DateOnly.FromDateTime(payment.PaidAt.ToLocalTime().Date))
            .ToDictionary(
                group => group.Key,
                group => group.Sum(payment => payment.Amount));
        var firstDate = DateOnly.FromDateTime(fromInclusive.ToLocalTime().Date);
        var lastDate = DateOnly.FromDateTime(toExclusive.ToLocalTime().AddTicks(-1).Date);
        var dailyRevenue = new List<DailyRevenueSummary>();
        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        {
            dailyRevenue.Add(new DailyRevenueSummary(
                date,
                revenueByDate.GetValueOrDefault(date)));
        }

        return new SalesReportSummary(
            payments.Count,
            payments.Sum(payment => payment.RestaurantOrder.OrderDetails
                .Sum(detail => detail.UnitPriceSnapshot * detail.Quantity)),
            payments.Sum(payment => payment.RestaurantOrder.DiscountAmountSnapshot),
            payments.Sum(payment => payment.Amount),
            paymentMethods,
            topSellingProducts,
            dailyRevenue);
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

    private async Task<AuthenticatedUser> GetAuthorizedHistoryReaderAsync(CancellationToken cancellationToken)
    {
        var user = await GetCurrentAccountAsync(cancellationToken);
        if (user.Role == UserRole.Admin)
        {
            throw new UnauthorizedAccessException("Admin không có quyền xem lịch sử order.");
        }

        if (user.Role == UserRole.Manager && user.EmployeeId is null)
        {
            throw new UnauthorizedAccessException("Tài khoản Quản lý chưa có hồ sơ nhân viên.");
        }

        if (user.Role is UserRole.Staff or UserRole.Manager
            && user.EmployeeId is { } employeeId
            && !await dbContext.Employees.AnyAsync(
                employee => employee.Id == employeeId && employee.IsActive,
                cancellationToken))
        {
            throw new UnauthorizedAccessException("Hồ sơ nhân viên không còn hoạt động.");
        }

        return user;
    }

    private async Task<AuthenticatedUser> GetAuthorizedAdminAsync(CancellationToken cancellationToken)
    {
        var user = await GetCurrentAccountAsync(cancellationToken);
        if (user.Role != UserRole.Manager)
        {
            throw new UnauthorizedAccessException("Chỉ Quản lý được xem báo cáo doanh thu.");
        }

        return user;
    }

    private async Task<AuthenticatedUser> GetCurrentAccountAsync(CancellationToken cancellationToken)
    {
        var user = currentUserSession.User
            ?? throw new UnauthorizedAccessException("Hãy đăng nhập để xem dữ liệu.");
        var account = await dbContext.UserAccounts
            .Where(candidate => candidate.Id == user.Id && !candidate.IsDeleted)
            .Select(candidate => new { candidate.Role, candidate.EmployeeId })
            .SingleOrDefaultAsync(cancellationToken);
        if (account is null
            || account.Role != user.Role
            || account.EmployeeId != user.EmployeeId)
        {
            throw new UnauthorizedAccessException("Tài khoản không còn quyền truy cập.");
        }

        return user;
    }

    private static void ValidateDateRange(DateTimeOffset? fromInclusive, DateTimeOffset? toExclusive)
    {
        if (fromInclusive is { } start
            && toExclusive is { } end
            && start >= end)
        {
            throw new ArgumentException("Ngày bắt đầu phải trước ngày kết thúc.");
        }
    }

    private static OrderHistorySummary ToHistorySummary(RestaurantOrder order)
    {
        var subtotal = order.OrderDetails.Sum(detail => detail.UnitPriceSnapshot * detail.Quantity);
        var payment = order.Payment;
        return new OrderHistorySummary(
            order.Id,
            order.RestaurantTable.TableNumber,
            order.RestaurantTable.Area,
            order.CreatedByEmployeeNameSnapshot,
            order.CreatedAt,
            order.Status,
            subtotal,
            order.DiscountAmountSnapshot,
            payment?.Amount ?? Math.Max(0, subtotal - order.DiscountAmountSnapshot),
            payment?.Method,
            payment?.PaidAt,
            order.CancellationReason);
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
                    detail.Notes,
                    detail.ProductId))
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
