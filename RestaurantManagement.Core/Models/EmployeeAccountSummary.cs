using RestaurantManagement.Core.Enums;

namespace RestaurantManagement.Core.Models;

public sealed record EmployeeAccountSummary(
    int EmployeeId,
    string FullName,
    string? Phone,
    string? Email,
    bool IsEmployeeActive,
    int? AccountId,
    string? Username,
    UserRole? Role,
    bool RequiresPasswordChange)
{
    public string? TemporaryPassword { get; init; }

    public string EmployeeStatusActionLabel => IsEmployeeActive ? "Tạm đình chỉ" : "Bỏ tạm đình chỉ";
}
