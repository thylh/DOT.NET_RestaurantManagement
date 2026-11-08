using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Core.Entities;
using RestaurantManagement.Core.Enums;
using RestaurantManagement.Core.Models;
using RestaurantManagement.Core.Services;
using RestaurantManagement.Infrastructure.Data;
using RestaurantManagement.Infrastructure.Security;

namespace RestaurantManagement.Infrastructure.Services;

public sealed class NotificationService(
    RestaurantDbContext dbContext,
    ICurrentUserSession currentUserSession) : INotificationService
{
    public async Task<IReadOnlyList<NotificationSummary>> GetInboxAsync(
        CancellationToken cancellationToken = default)
    {
        var currentUser = EnsureAuthenticated();
        return await dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.RecipientUserId == currentUser.Id)
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(100)
            .Select(notification => new NotificationSummary(
                notification.Id,
                notification.Title,
                notification.Message,
                notification.Sender == null ? "Hệ thống" : notification.Sender.FullName,
                notification.Sender == null ? "Hệ thống" : notification.Sender.Role.ToString(),
                notification.CreatedAt,
                notification.ReadAt,
                notification.Kind,
                notification.Priority,
                notification.ResetTargetUserId,
                notification.ResetTargetUser == null ? null : notification.ResetTargetUser.FullName,
                notification.ResetTargetUser == null ? null : notification.ResetTargetUser.Username,
                notification.ResolvedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationRecipientOption>> GetRecipientsAsync(
        CancellationToken cancellationToken = default)
    {
        var currentUser = EnsureCanSendNotifications();
        var allowedRoles = currentUser.Role == UserRole.Admin
            ? new[] { UserRole.Staff, UserRole.Manager }
            : [UserRole.Staff];
        var recipients = await dbContext.UserAccounts
            .AsNoTracking()
            .Where(account => !account.IsDeleted
                && allowedRoles.Contains(account.Role))
            .OrderBy(account => account.FullName)
            .Select(account => new
            {
                account.Id,
                account.FullName,
                account.Username,
                account.Role
            })
            .ToListAsync(cancellationToken);

        var options = new List<NotificationRecipientOption>();
        foreach (var role in allowedRoles)
        {
            options.Add(new NotificationRecipientOption(
                role == UserRole.Staff ? "Tất cả nhân viên" : "Tất cả quản lý",
                null,
                role));
        }

        options.AddRange(recipients.Select(account => new NotificationRecipientOption(
            $"{account.FullName} ({account.Username}) · {GetRoleDisplay(account.Role)}",
            account.Id,
            null)));
        return options;
    }

    public async Task SendAsync(
        string title,
        string message,
        NotificationPriority priority,
        int? recipientUserId,
        UserRole? recipientRole,
        CancellationToken cancellationToken = default)
    {
        var sender = EnsureCanSendNotifications();
        var normalizedTitle = ValidateText(title, 120, nameof(title));
        var normalizedMessage = ValidateText(message, 1000, nameof(message));
        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority), "Hãy chọn mức độ thông báo hợp lệ.");
        }

        if (recipientUserId.HasValue == recipientRole.HasValue)
        {
            throw new ArgumentException("Hãy chọn một người nhận hoặc một vai trò.");
        }

        var allowedRoles = sender.Role == UserRole.Admin
            ? new[] { UserRole.Staff, UserRole.Manager }
            : [UserRole.Staff];
        if (recipientRole is { } role && !allowedRoles.Contains(role))
        {
            throw new InvalidOperationException("Bạn không có quyền gửi thông báo tới vai trò đã chọn.");
        }

        var recipientsQuery = dbContext.UserAccounts
            .Where(account => !account.IsDeleted
                && allowedRoles.Contains(account.Role));
        var recipients = recipientUserId is { } userId
            ? await recipientsQuery
                .Where(account => account.Id == userId)
                .ToListAsync(cancellationToken)
            : await recipientsQuery
                .Where(account => account.Role == recipientRole)
                .ToListAsync(cancellationToken);
        if (recipients.Count == 0)
        {
            throw new InvalidOperationException("Không tìm thấy người nhận phù hợp.");
        }

        var createdAt = DateTimeOffset.UtcNow;
        dbContext.Notifications.AddRange(recipients.Select(recipient => new Notification
        {
            Title = normalizedTitle,
            Message = normalizedMessage,
            SenderUserId = sender.Id,
            RecipientUserId = recipient.Id,
            Kind = NotificationKind.Manual,
            Priority = priority,
            CreatedAt = createdAt
        }));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RequestPasswordResetAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = username?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUsername) || normalizedUsername.Length > 50)
        {
            return;
        }

        var account = await dbContext.UserAccounts
            .SingleOrDefaultAsync(
                candidate => candidate.Username == normalizedUsername
                    && !candidate.IsDeleted
                    && candidate.Role == UserRole.Staff,
                cancellationToken);
        if (account is null
            || await dbContext.Notifications.AnyAsync(notification =>
                notification.Kind == NotificationKind.PasswordResetRequest
                && notification.ResetTargetUserId == account.Id
                && notification.ResolvedAt == null,
                cancellationToken))
        {
            return;
        }

        var administrators = await dbContext.UserAccounts
            .Where(candidate => !candidate.IsDeleted && candidate.Role == UserRole.Admin)
            .Select(candidate => candidate.Id)
            .ToListAsync(cancellationToken);
        if (administrators.Count == 0)
        {
            throw new InvalidOperationException("Hiện không có tài khoản Admin để nhận yêu cầu.");
        }

        var createdAt = DateTimeOffset.UtcNow;
        dbContext.Notifications.AddRange(administrators.Select(adminId => new Notification
        {
            Title = "Yêu cầu cấp lại mật khẩu",
            Message = $"{account.FullName} ({account.Username}) yêu cầu Admin cấp lại mật khẩu.",
            SenderUserId = account.Id,
            RecipientUserId = adminId,
            Kind = NotificationKind.PasswordResetRequest,
            Priority = NotificationPriority.Normal,
            CreatedAt = createdAt,
            ResetTargetUserId = account.Id
        }));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<GeneratedEmployeeCredentials> IssueTemporaryPasswordAsync(
        int notificationId,
        CancellationToken cancellationToken = default)
    {
        var administrator = EnsureAdmin();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var request = await dbContext.Notifications
            .SingleOrDefaultAsync(notification =>
                notification.Id == notificationId
                && notification.RecipientUserId == administrator.Id
                && notification.Kind == NotificationKind.PasswordResetRequest
                && notification.ResolvedAt == null,
                cancellationToken);
        if (request?.ResetTargetUserId is not { } targetUserId)
        {
            throw new InvalidOperationException("Yêu cầu cấp lại mật khẩu không còn chờ xử lý.");
        }

        var account = await dbContext.UserAccounts.SingleOrDefaultAsync(candidate =>
            candidate.Id == targetUserId
            && candidate.Role == UserRole.Staff
            && !candidate.IsDeleted,
            cancellationToken);
        if (account is null)
        {
            throw new InvalidOperationException("Không tìm thấy tài khoản nhân viên còn hoạt động.");
        }

        var temporaryPassword = GenerateTemporaryPassword();
        account.PasswordHash = global::BCrypt.Net.BCrypt.HashPassword(temporaryPassword);
        account.EncryptedTemporaryPassword = TemporaryPasswordProtector.Protect(temporaryPassword);
        account.RequiresPasswordChange = true;

        var pendingRequests = await dbContext.Notifications
            .Where(notification =>
                notification.Kind == NotificationKind.PasswordResetRequest
                && notification.ResetTargetUserId == account.Id
                && notification.ResolvedAt == null)
            .ToListAsync(cancellationToken);
        var resolvedAt = DateTimeOffset.UtcNow;
        foreach (var pendingRequest in pendingRequests)
        {
            pendingRequest.ResolvedAt = resolvedAt;
        }

        dbContext.Notifications.Add(new Notification
        {
            Title = "Mật khẩu tạm đã được cấp",
            Message = "Admin đã cấp mật khẩu tạm mới. Hãy liên hệ Admin để nhận mật khẩu; sau khi đăng nhập, bạn sẽ cần đổi mật khẩu.",
            SenderUserId = administrator.Id,
            RecipientUserId = account.Id,
            Kind = NotificationKind.PasswordResetIssued,
            Priority = NotificationPriority.Normal,
            CreatedAt = resolvedAt
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new GeneratedEmployeeCredentials(account.Id, account.Username, temporaryPassword);
    }

    public async Task MarkReadAsync(
        int notificationId,
        CancellationToken cancellationToken = default)
    {
        var currentUser = EnsureAuthenticated();
        var notification = await dbContext.Notifications.SingleOrDefaultAsync(candidate =>
            candidate.Id == notificationId && candidate.RecipientUserId == currentUser.Id,
            cancellationToken);
        if (notification is null)
        {
            throw new InvalidOperationException("Không tìm thấy thông báo trong hộp thư của bạn.");
        }

        notification.ReadAt ??= DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private AuthenticatedUser EnsureAuthenticated() =>
        currentUserSession.User
        ?? throw new InvalidOperationException("Hãy đăng nhập để xem thông báo.");

    private AuthenticatedUser EnsureCanSendNotifications()
    {
        var currentUser = EnsureAuthenticated();
        if (currentUser.Role is not UserRole.Admin and not UserRole.Manager)
        {
            throw new InvalidOperationException("Chỉ Admin hoặc Quản lý mới có thể gửi thông báo.");
        }

        return currentUser;
    }

    private AuthenticatedUser EnsureAdmin()
    {
        var currentUser = EnsureAuthenticated();
        if (currentUser.Role != UserRole.Admin)
        {
            throw new InvalidOperationException("Chỉ Admin mới có thể cấp lại mật khẩu.");
        }

        return currentUser;
    }

    private static string ValidateText(string? value, int maximumLength, string parameterName)
    {
        var normalizedValue = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedValue) || normalizedValue.Length > maximumLength)
        {
            throw new ArgumentException(
                $"Nội dung bắt buộc và không được vượt quá {maximumLength} ký tự.",
                parameterName);
        }

        return normalizedValue;
    }

    private static string GetRoleDisplay(UserRole role) => role switch
    {
        UserRole.Admin => "Admin",
        UserRole.Manager => "Quản lý",
        _ => "Nhân viên"
    };

    private static string GenerateTemporaryPassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
        return string.Create(20, alphabet, static (password, characters) =>
        {
            for (var index = 0; index < password.Length; index++)
            {
                password[index] = characters[RandomNumberGenerator.GetInt32(characters.Length)];
            }
        });
    }
}
