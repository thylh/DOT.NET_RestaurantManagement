using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RestaurantManagement.Core.Entities;
using RestaurantManagement.Core.Enums;
using RestaurantManagement.Core.Models;
using RestaurantManagement.Core.Services;

namespace RestaurantManagement.Infrastructure.Data;

public sealed class AuditLogSaveChangesInterceptor(ICurrentUserSession currentUserSession)
    : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, List<AuditLog>> _pendingAuditLogs = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        AddAuditEntries(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddAuditEntries(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        CompleteSave(eventData.Context, detachPendingEntries: false);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        CompleteSave(eventData.Context, detachPendingEntries: false);
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) =>
        CompleteSave(eventData.Context, detachPendingEntries: true);

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        CompleteSave(eventData.Context, detachPendingEntries: true);
        return Task.CompletedTask;
    }

    public override void SaveChangesCanceled(DbContextEventData eventData) =>
        CompleteSave(eventData.Context, detachPendingEntries: true);

    public override Task SaveChangesCanceledAsync(
        DbContextEventData eventData,
        CancellationToken cancellationToken = default)
    {
        CompleteSave(eventData.Context, detachPendingEntries: true);
        return Task.CompletedTask;
    }

    private void AddAuditEntries(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();
        var logs = context.ChangeTracker.Entries()
            .Where(entry => entry.Entity is not AuditLog)
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(CreateAuditLog)
            .Where(log => log is not null)
            .Cast<AuditLog>()
            .ToArray();

        if (logs.Length > 0)
        {
            context.Set<AuditLog>().AddRange(logs);
            _pendingAuditLogs.GetOrCreateValue(context).AddRange(logs);
        }
    }

    private void CompleteSave(DbContext? context, bool detachPendingEntries)
    {
        if (context is null || !_pendingAuditLogs.TryGetValue(context, out var logs))
        {
            return;
        }

        if (detachPendingEntries)
        {
            foreach (var log in logs)
            {
                context.Entry(log).State = EntityState.Detached;
            }
        }

        _pendingAuditLogs.Remove(context);
    }

    private AuditLog? CreateAuditLog(EntityEntry entry)
    {
        var changedProperties = GetChangedProperties(entry);
        if (changedProperties.Count == 0)
        {
            return null;
        }

        var (category, action, targetType, targetName, summary) = Describe(entry, changedProperties);
        if (category is null)
        {
            return null;
        }

        var user = currentUserSession.User;
        var idProperty = entry.Metadata.FindProperty("Id");
        var idEntry = idProperty is null ? null : entry.Property(idProperty.Name);
        var id = entry.State != EntityState.Added && idEntry is { IsTemporary: false }
            ? Convert.ToInt32(idEntry.CurrentValue)
            : (int?)null;
        if (entry.Entity is OrderDetail)
        {
            id = null;
        }

        var changes = CreateChanges(entry);
        return new AuditLog
        {
            OccurredAt = DateTimeOffset.UtcNow,
            ActorUserId = user?.Id,
            ActorUsername = user?.Username ?? "system",
            ActorRole = user?.Role.ToString() ?? "System",
            Category = category.Value,
            Action = action,
            TargetType = targetType,
            TargetId = id,
            TargetName = targetName,
            Summary = summary,
            ChangesJson = changes.Count == 0 ? null : JsonSerializer.Serialize(changes, JsonOptions)
        };
    }

    private static (AuditLogCategory? Category, AuditLogAction Action, string TargetType, string TargetName, string Summary)
        Describe(EntityEntry entry, IReadOnlyList<PropertyEntry> changedProperties)
    {
        var targetName = GetString(entry, "FullName")
            ?? GetString(entry, "Name")
            ?? GetString(entry, "TableNumber")
            ?? GetString(entry, "ProductNameSnapshot")
            ?? "Không xác định";

        if (entry.Entity is UserAccount account)
        {
            var action = entry.State switch
            {
                EntityState.Added => AuditLogAction.Created,
                EntityState.Deleted => AuditLogAction.Deleted,
                _ when IsChanged(changedProperties, nameof(UserAccount.IsDeleted))
                    && account.IsDeleted => AuditLogAction.Deleted,
                _ when IsChanged(changedProperties, nameof(UserAccount.Role))
                    => AuditLogAction.RoleChanged,
                _ when IsChanged(changedProperties, nameof(UserAccount.PasswordHash))
                    => AuditLogAction.PasswordChanged,
                _ => AuditLogAction.Updated
            };
            return (AuditLogCategory.Account, action, "Tài khoản", account.Username,
                $"{GetActionText(action)} tài khoản {account.Username}.");
        }

        if (entry.Entity is Employee employee)
        {
            var action = entry.State switch
            {
                EntityState.Added => AuditLogAction.Created,
                EntityState.Deleted => AuditLogAction.Deleted,
                _ when IsChanged(changedProperties, nameof(Employee.IsActive))
                    && !employee.IsActive => AuditLogAction.Suspended,
                _ when IsChanged(changedProperties, nameof(Employee.IsActive))
                    && employee.IsActive => AuditLogAction.Reactivated,
                _ => AuditLogAction.Updated
            };
            return (AuditLogCategory.Employee, action, "Hồ sơ nhân viên", employee.FullName,
                $"{GetActionText(action)} hồ sơ nhân viên {employee.FullName}.");
        }

        if (entry.Entity is RestaurantTable table)
        {
            return (AuditLogCategory.Catalog, GetCrudAction(entry), "Bàn", table.TableNumber,
                $"{GetActionText(GetCrudAction(entry))} bàn {table.TableNumber}.");
        }

        if (entry.Entity is Category category)
        {
            return (AuditLogCategory.Catalog, GetCrudAction(entry), "Danh mục", category.Name,
                $"{GetActionText(GetCrudAction(entry))} danh mục {category.Name}.");
        }

        if (entry.Entity is Product product)
        {
            return (AuditLogCategory.Catalog, GetCrudAction(entry), "Món ăn", product.Name,
                $"{GetActionText(GetCrudAction(entry))} món {product.Name}.");
        }

        if (entry.Entity is Promotion promotion)
        {
            return (AuditLogCategory.Catalog, GetCrudAction(entry), "Khuyến mãi", promotion.Name,
                $"{GetActionText(GetCrudAction(entry))} khuyến mãi {promotion.Name}.");
        }

        if (entry.Entity is RestaurantOrder order)
        {
            var action = entry.State == EntityState.Added
                ? AuditLogAction.Created
                : IsChanged(changedProperties, nameof(RestaurantOrder.Status))
                    ? order.Status switch
                    {
                        OrderStatus.Paid => AuditLogAction.Paid,
                        OrderStatus.Cancelled => AuditLogAction.Cancelled,
                        _ => AuditLogAction.Updated
                    }
                    : AuditLogAction.Updated;
            var orderName = order.Id > 0
                ? $"Order #{order.Id}"
                : $"Order mới (Bàn #{order.RestaurantTableId})";
            return (AuditLogCategory.Order, action, "Order", orderName,
                $"{GetActionText(action)} {orderName}.");
        }

        if (entry.Entity is OrderDetail detail)
        {
            var action = GetCrudAction(entry);
            return (AuditLogCategory.Order, action, $"Order #{detail.RestaurantOrderId}",
                detail.ProductNameSnapshot,
                $"{GetActionText(action)} món {detail.ProductNameSnapshot} trong order #{detail.RestaurantOrderId}.");
        }

        return (null, default, string.Empty, string.Empty, string.Empty);
    }

    private static AuditLogAction GetCrudAction(EntityEntry entry) => entry.State switch
    {
        EntityState.Added => AuditLogAction.Created,
        EntityState.Deleted => AuditLogAction.Deleted,
        _ => AuditLogAction.Updated
    };

    private static List<PropertyEntry> GetChangedProperties(EntityEntry entry) =>
        entry.Properties
            .Where(property => property.Metadata.Name != "Id")
            .Where(property => entry.Entity is not UserAccount
                || property.Metadata.Name is not nameof(UserAccount.EmployeeId)
                    and not nameof(UserAccount.EmployeeProfileDeleted))
            .Where(property => entry.State != EntityState.Modified || property.IsModified)
            .ToList();

    private static Dictionary<string, object?> CreateChanges(EntityEntry entry)
    {
        var result = new Dictionary<string, object?>();
        foreach (var property in GetChangedProperties(entry))
        {
            if (IsSensitive(property.Metadata.Name))
            {
                continue;
            }

            var before = entry.State == EntityState.Added ? null : property.OriginalValue;
            var after = entry.State == EntityState.Deleted ? null : property.CurrentValue;
            if (Equals(before, after))
            {
                continue;
            }

            result[property.Metadata.Name] = new
            {
                Before = before,
                After = after
            };
        }

        return result;
    }

    private static bool IsSensitive(string propertyName) =>
        propertyName.Contains("Password", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("Secret", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("Token", StringComparison.OrdinalIgnoreCase);

    private static bool IsChanged(
        IReadOnlyList<PropertyEntry> properties,
        string propertyName) =>
        properties.Any(property => property.Metadata.Name == propertyName && property.IsModified);

    private static string? GetString(EntityEntry entry, string propertyName) =>
        entry.Metadata.FindProperty(propertyName) is null
            ? null
            : entry.Property(propertyName).CurrentValue?.ToString();

    private static string GetActionText(AuditLogAction action) => action switch
    {
        AuditLogAction.Created => "Tạo",
        AuditLogAction.Updated => "Cập nhật",
        AuditLogAction.Deleted => "Xóa",
        AuditLogAction.RoleChanged => "Đổi vai trò cho",
        AuditLogAction.PasswordChanged => "Đổi mật khẩu cho",
        AuditLogAction.Suspended => "Đình chỉ",
        AuditLogAction.Reactivated => "Bỏ đình chỉ",
        AuditLogAction.Paid => "Thanh toán",
        AuditLogAction.Cancelled => "Hủy",
        _ => "Thay đổi"
    };
}
