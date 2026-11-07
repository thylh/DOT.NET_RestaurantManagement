using RestaurantManagement.Core.Models;

namespace RestaurantManagement.Core.Services;

public interface IAuditLogService
{
    Task<IReadOnlyList<AuditLogSummary>> GetAuditLogsAsync(
        AuditLogFilter filter,
        CancellationToken cancellationToken = default);
}
