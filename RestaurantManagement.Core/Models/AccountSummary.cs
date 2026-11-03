using RestaurantManagement.Core.Enums;

namespace RestaurantManagement.Core.Models;

public sealed record AccountSummary(
    int AccountId,
    int? EmployeeId,
    string FullName,
    string? Phone,
    string? Email,
    bool IsEmployeeActive,
    string Username,
    UserRole Role,
    bool IsAccountActive,
    bool RequiresPasswordChange)
{
    public string? TemporaryPassword { get; init; }

    public string PasswordDisplay => RequiresPasswordChange
        ? TemporaryPassword ?? "Cần đặt lại mật khẩu"
        : "Đã đổi mật khẩu";

    public bool CanCopyTemporaryPassword => !string.IsNullOrEmpty(TemporaryPassword);

    public bool HasEmployeeProfile => EmployeeId.HasValue;

    public bool CanCreateEmployeeProfile => Role == UserRole.Staff && !HasEmployeeProfile;

    public string ProfileActionLabel => HasEmployeeProfile ? "Đã tạo hs" : "Tạo hồ sơ";

    public string AccountStatusActionLabel => IsAccountActive ? "Khóa tài khoản" : "Mở khóa tài khoản";
}
