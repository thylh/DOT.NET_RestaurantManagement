using RestaurantManagement.Core.Enums;
using RestaurantManagement.Core.Models;

namespace RestaurantManagement.Core.Services;

public interface IEmployeeManagementService
{
    Task<IReadOnlyList<EmployeeAccountSummary>> GetEmployeesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountSummary>> GetAccountsAsync(
        CancellationToken cancellationToken = default);

    Task CreateEmployeeAsync(
        string fullName,
        string? phone,
        string? email,
        CancellationToken cancellationToken = default);

    Task<GeneratedEmployeeCredentials> CreateAccountAsync(
        string fullName,
        string? phone,
        string? email,
        CancellationToken cancellationToken = default);

    Task CreateEmployeeProfileAsync(
        int accountId,
        CancellationToken cancellationToken = default);

    Task UpdateAccountEmployeeNameAsync(
        int accountId,
        string fullName,
        CancellationToken cancellationToken = default);

    Task DeleteAccountAsync(
        int accountId,
        CancellationToken cancellationToken = default);

    Task UpdateEmployeeAsync(
        int employeeId,
        string fullName,
        string? phone,
        string? email,
        CancellationToken cancellationToken = default);

    Task DeleteEmployeeProfileAsync(
        int employeeId,
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
