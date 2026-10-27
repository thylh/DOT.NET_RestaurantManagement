using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Core.Entities;
using RestaurantManagement.Core.Enums;
using RestaurantManagement.Core.Models;
using RestaurantManagement.Core.Services;
using RestaurantManagement.Infrastructure.Data;

namespace RestaurantManagement.Infrastructure.Services;

public sealed class EmployeeManagementService(
    RestaurantDbContext dbContext,
    ICurrentUserSession currentUserSession) : IEmployeeManagementService
{
    public async Task<IReadOnlyList<EmployeeAccountSummary>> GetEmployeesAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();

        return await dbContext.Employees
            .AsNoTracking()
            .OrderBy(employee => employee.FullName)
            .Select(employee => new EmployeeAccountSummary(
                employee.Id,
                employee.FullName,
                employee.Phone,
                employee.Email,
                employee.IsActive,
                employee.UserAccount == null ? null : employee.UserAccount.Id,
                employee.UserAccount == null ? null : employee.UserAccount.Username,
                employee.UserAccount == null ? null : employee.UserAccount.Role,
                employee.UserAccount == null ? null : employee.UserAccount.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task CreateEmployeeAsync(
        string fullName,
        string? phone,
        string? email,
        string username,
        string password,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var normalizedName = ValidateRequiredText(fullName, 150, nameof(fullName));
        var normalizedUsername = ValidateRequiredText(username, 50, nameof(username));
        ValidatePassword(password);
        if (role is not UserRole.Admin and not UserRole.Staff)
        {
            throw new ArgumentOutOfRangeException(nameof(role), "Select a valid account role.");
        }

        var employee = new Employee
        {
            FullName = normalizedName,
            Phone = NormalizeOptional(phone, 30, nameof(phone)),
            Email = NormalizeOptional(email, 254, nameof(email))
        };
        employee.UserAccount = new UserAccount
        {
            Username = normalizedUsername,
            PasswordHash = global::BCrypt.Net.BCrypt.HashPassword(password),
            Role = role
        };

        dbContext.Employees.Add(employee);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new InvalidOperationException(
                "Could not create the employee. The username may already be in use.",
                exception);
        }
    }

    public async Task UpdateEmployeeAsync(
        int employeeId,
        string fullName,
        string? phone,
        string? email,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var employee = await dbContext.Employees.SingleOrDefaultAsync(
            candidate => candidate.Id == employeeId,
            cancellationToken);
        if (employee is null)
        {
            throw new InvalidOperationException("The selected employee no longer exists.");
        }

        employee.FullName = ValidateRequiredText(fullName, 150, nameof(fullName));
        employee.Phone = NormalizeOptional(phone, 30, nameof(phone));
        employee.Email = NormalizeOptional(email, 254, nameof(email));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAccountAsync(
        int accountId,
        string username,
        UserRole role,
        string? newPassword,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var normalizedUsername = ValidateRequiredText(username, 50, nameof(username));
        if (role is not UserRole.Admin and not UserRole.Staff)
        {
            throw new ArgumentOutOfRangeException(nameof(role), "Select a valid account role.");
        }

        var account = await dbContext.UserAccounts.SingleOrDefaultAsync(
            candidate => candidate.Id == accountId,
            cancellationToken);
        if (account is null)
        {
            throw new InvalidOperationException("The selected account no longer exists.");
        }

        if (account.Id == currentUserSession.User?.Id && role != UserRole.Admin)
        {
            throw new InvalidOperationException("You cannot change your own role.");
        }

        if (account.Role == UserRole.Admin && role != UserRole.Admin)
        {
            await EnsureAnotherActiveAdminExistsAsync(account.Id, account.EmployeeId, cancellationToken);
        }

        account.Username = normalizedUsername;
        account.Role = role;
        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            ValidatePassword(newPassword);
            account.PasswordHash = global::BCrypt.Net.BCrypt.HashPassword(newPassword);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new InvalidOperationException(
                "Could not update the account. The username may already be in use.",
                exception);
        }
    }

    public async Task SetEmployeeActiveAsync(
        int employeeId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var employee = await dbContext.Employees
            .Include(candidate => candidate.UserAccount)
            .SingleOrDefaultAsync(candidate => candidate.Id == employeeId, cancellationToken);
        if (employee is null)
        {
            throw new InvalidOperationException("The selected employee no longer exists.");
        }

        if (!isActive && employee.UserAccount?.Id == currentUserSession.User?.Id)
        {
            throw new InvalidOperationException("You cannot deactivate your own employee profile.");
        }

        if (!isActive && employee.UserAccount?.Role == UserRole.Admin)
        {
            await EnsureAnotherActiveAdminExistsAsync(
                employee.UserAccount.Id,
                employee.Id,
                cancellationToken);
        }

        employee.IsActive = isActive;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetAccountActiveAsync(
        int accountId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var account = await dbContext.UserAccounts
            .SingleOrDefaultAsync(candidate => candidate.Id == accountId, cancellationToken);
        if (account is null)
        {
            throw new InvalidOperationException("The selected account no longer exists.");
        }

        if (!isActive && account.Id == currentUserSession.User?.Id)
        {
            throw new InvalidOperationException("You cannot lock your own signed-in account.");
        }

        if (!isActive && account.Role == UserRole.Admin)
        {
            await EnsureAnotherActiveAdminExistsAsync(account.Id, account.EmployeeId, cancellationToken);
        }

        account.IsActive = isActive;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void EnsureAdmin()
    {
        if (currentUserSession.User?.Role != UserRole.Admin)
        {
            throw new UnauthorizedAccessException("Only an Admin can manage employee accounts.");
        }
    }

    private async Task EnsureAnotherActiveAdminExistsAsync(
        int excludedAccountId,
        int? excludedEmployeeId,
        CancellationToken cancellationToken)
    {
        var anotherAdminExists = await dbContext.UserAccounts.AnyAsync(
            account => account.Id != excludedAccountId
                && account.Role == UserRole.Admin
                && account.IsActive
                && (account.EmployeeId == null
                    || account.EmployeeId != excludedEmployeeId && account.Employee!.IsActive),
            cancellationToken);
        if (!anotherAdminExists)
        {
            throw new InvalidOperationException("At least one other active Admin account must remain.");
        }
    }

    private static string ValidateRequiredText(string value, int maxLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value must not exceed {maxLength} characters.", parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string parameterName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value must not exceed {maxLength} characters.", parameterName);
        }

        return normalized;
    }

    private static void ValidatePassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var passwordByteCount = System.Text.Encoding.UTF8.GetByteCount(password);
        if (passwordByteCount is < 12 or > 72)
        {
            throw new ArgumentException(
                "Password must contain at least 12 characters and no more than 72 UTF-8 bytes.",
                nameof(password));
        }
    }
}
