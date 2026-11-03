using RestaurantManagement.Core.Enums;

namespace RestaurantManagement.Core.Models;

public sealed record AuthenticatedUser(
    int Id,
    string Username,
    UserRole Role,
    int? EmployeeId,
    string? FullName,
    bool RequiresPasswordChange = false);
