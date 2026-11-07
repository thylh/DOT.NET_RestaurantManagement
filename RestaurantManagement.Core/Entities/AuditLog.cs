using RestaurantManagement.Core.Enums;

namespace RestaurantManagement.Core.Entities;

public class AuditLog
{
    public long Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    public int? ActorUserId { get; set; }

    public required string ActorUsername { get; set; }

    public required string ActorRole { get; set; }

    public AuditLogCategory Category { get; set; }

    public AuditLogAction Action { get; set; }

    public required string TargetType { get; set; }

    public int? TargetId { get; set; }

    public required string TargetName { get; set; }

    public required string Summary { get; set; }

    public string? ChangesJson { get; set; }
}
