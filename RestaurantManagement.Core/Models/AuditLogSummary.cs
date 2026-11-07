using RestaurantManagement.Core.Enums;

namespace RestaurantManagement.Core.Models;

public sealed record AuditLogSummary(
    long Id,
    DateTimeOffset OccurredAt,
    string ActorUsername,
    string ActorRole,
    AuditLogCategory Category,
    AuditLogAction Action,
    string TargetType,
    int? TargetId,
    string TargetName,
    string Summary,
    string? ChangesJson)
{
    public string CategoryDisplay => Category switch
    {
        AuditLogCategory.Account => "Tài khoản",
        AuditLogCategory.Employee => "Nhân viên",
        AuditLogCategory.Catalog => "Bàn / thực đơn",
        AuditLogCategory.Order => "Order",
        _ => Category.ToString()
    };

    public string ActionDisplay => Action switch
    {
        AuditLogAction.Created => "Tạo mới",
        AuditLogAction.Updated => "Cập nhật",
        AuditLogAction.Deleted => "Xóa",
        AuditLogAction.RoleChanged => "Đổi vai trò",
        AuditLogAction.PasswordChanged => "Đổi mật khẩu",
        AuditLogAction.Suspended => "Đình chỉ",
        AuditLogAction.Reactivated => "Bỏ đình chỉ",
        AuditLogAction.Paid => "Thanh toán",
        AuditLogAction.Cancelled => "Hủy",
        _ => Action.ToString()
    };

    public string OccurredAtDisplay => OccurredAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");

    public string TargetDisplay => TargetId is { } id
        ? $"{TargetType} #{id} - {TargetName}"
        : $"{TargetType} - {TargetName}";
}

public sealed record AuditLogFilter(
    AuditLogCategory? Category,
    AuditLogAction? Action,
    string? SearchText,
    DateTimeOffset? FromInclusive,
    DateTimeOffset? ToExclusive,
    int Limit = 500);
