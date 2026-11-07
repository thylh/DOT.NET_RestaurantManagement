using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Core.Enums;
using RestaurantManagement.Core.Models;
using RestaurantManagement.Core.Services;
using RestaurantManagement.Infrastructure.Data;

namespace RestaurantManagement.Infrastructure.Services;

public sealed class AuditLogService(
    RestaurantDbContext dbContext,
    ICurrentUserSession currentUserSession) : IAuditLogService
{
    public async Task<IReadOnlyList<AuditLogSummary>> GetAuditLogsAsync(
        AuditLogFilter filter,
        CancellationToken cancellationToken = default)
    {
        if (currentUserSession.User?.Role != UserRole.Admin)
        {
            throw new UnauthorizedAccessException("Chỉ Admin mới được xem Audit Log.");
        }

        if (filter.FromInclusive is { } from
            && filter.ToExclusive is { } to
            && to <= from)
        {
            throw new ArgumentException("Khoảng thời gian lọc không hợp lệ.", nameof(filter));
        }

        var query = dbContext.AuditLogs.AsNoTracking();
        if (filter.Category is { } category)
        {
            query = query.Where(log => log.Category == category);
        }

        if (filter.Action is { } action)
        {
            query = query.Where(log => log.Action == action);
        }

        if (filter.FromInclusive is { } start)
        {
            query = query.Where(log => log.OccurredAt >= start);
        }

        if (filter.ToExclusive is { } end)
        {
            query = query.Where(log => log.OccurredAt < end);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim();
            query = query.Where(log =>
                log.ActorUsername.Contains(search)
                || log.TargetName.Contains(search)
                || log.Summary.Contains(search));
        }

        return await query
            .OrderByDescending(log => log.OccurredAt)
            .ThenByDescending(log => log.Id)
            .Take(Math.Clamp(filter.Limit, 1, 1000))
            .Select(log => new AuditLogSummary(
                log.Id,
                log.OccurredAt,
                log.ActorUsername,
                log.ActorRole,
                log.Category,
                log.Action,
                log.TargetType,
                log.TargetId,
                log.TargetName,
                log.Summary,
                log.ChangesJson))
            .ToListAsync(cancellationToken);
    }
}
