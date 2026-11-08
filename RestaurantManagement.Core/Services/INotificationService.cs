using RestaurantManagement.Core.Enums;
using RestaurantManagement.Core.Models;

namespace RestaurantManagement.Core.Services;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationSummary>> GetInboxAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationRecipientOption>> GetRecipientsAsync(
        CancellationToken cancellationToken = default);

    Task SendAsync(
        string title,
        string message,
        NotificationPriority priority,
        int? recipientUserId,
        UserRole? recipientRole,
        CancellationToken cancellationToken = default);

    Task RequestPasswordResetAsync(
        string username,
        CancellationToken cancellationToken = default);

    Task<GeneratedEmployeeCredentials> IssueTemporaryPasswordAsync(
        int notificationId,
        CancellationToken cancellationToken = default);

    Task MarkReadAsync(
        int notificationId,
        CancellationToken cancellationToken = default);
}
