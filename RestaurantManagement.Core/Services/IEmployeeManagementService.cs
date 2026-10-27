using RestaurantManagement.Core.Enums;
using RestaurantManagement.Core.Models;

namespace RestaurantManagement.Core.Services;

public interface IEmployeeManagementService
{
    Task<IReadOnlyList<EmployeeAccountSummary>> GetEmployeesAsync(
        CancellationToken cancellationToken = default);

    Task CreateEmployeeAsync(
        string fullName,
        string? phone,
        string? email,
        string username,
        string password,
        UserRole role,
        CancellationToken cancellationToken = default);

    Task UpdateEmployeeAsync(
        int employeeId,
        string fullName,
        string? phone,
        string? email,
        CancellationToken cancellationToken = default);

    Task UpdateAccountAsync(
        int accountId,
        string username,
        UserRole role,
        string? newPassword,
        CancellationToken cancellationToken = default);

    Task SetEmployeeActiveAsync(
        int employeeId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task SetAccountActiveAsync(
        int accountId,
        bool isActive,
        CancellationToken cancellationToken = default);
}
