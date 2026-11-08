namespace RestaurantManagement.Core.Entities;

public class Notification
{
    public int Id { get; set; }

    public required string Title { get; set; }

    public required string Message { get; set; }

    public int? SenderUserId { get; set; }

    public required int RecipientUserId { get; set; }

    public RestaurantManagement.Core.Enums.NotificationKind Kind { get; set; }

    public RestaurantManagement.Core.Enums.NotificationPriority Priority { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ReadAt { get; set; }

    public int? ResetTargetUserId { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public UserAccount? Sender { get; set; }

    public UserAccount Recipient { get; set; } = null!;

    public UserAccount? ResetTargetUser { get; set; }
}
