using RestaurantManagement.Core.Enums;

namespace RestaurantManagement.Core.Models;

public sealed record NotificationSummary(
    int Id,
    string Title,
    string Message,
    string SenderName,
    string SenderRole,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt,
    NotificationKind Kind,
    NotificationPriority Priority,
    int? ResetTargetUserId,
    string? ResetTargetName,
    string? ResetTargetUsername,
    DateTimeOffset? ResolvedAt)
{
    public bool IsRead => ReadAt.HasValue;

    public bool IsUrgent => Priority == NotificationPriority.Urgent;

    public bool IsPasswordResetRequest => Kind == NotificationKind.PasswordResetRequest;

    public bool IsResolved => ResolvedAt.HasValue;

    public string CreatedAtDisplay => CreatedAt.ToLocalTime().ToString("dd/MM HH:mm");
}
