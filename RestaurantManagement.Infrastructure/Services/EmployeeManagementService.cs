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
            throw new ArgumentOutOfRangeException(nameof(role), "Hãy chọn vai trò hợp lệ.");
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
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            foreach (var entry in exception.Entries)
            {
                entry.State = EntityState.Detached;
            }

            throw new InvalidOperationException(
                "Không thể tạo hồ sơ. Tên đăng nhập này có thể đã được sử dụng.",
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
            throw new InvalidOperationException("Không tìm thấy hồ sơ nhân viên cần cập nhật.");
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
            throw new ArgumentOutOfRangeException(nameof(role), "Hãy chọn vai trò hợp lệ.");
        }

        var account = await dbContext.UserAccounts.SingleOrDefaultAsync(
            candidate => candidate.Id == accountId,
            cancellationToken);
        if (account is null)
        {
            throw new InvalidOperationException("Không tìm thấy tài khoản cần cập nhật.");
        }

        if (account.Id == currentUserSession.User?.Id && role != UserRole.Admin)
        {
            throw new InvalidOperationException("Bạn không thể tự thay đổi vai trò của tài khoản đang đăng nhập.");
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
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            foreach (var entry in exception.Entries)
            {
                entry.State = EntityState.Detached;
            }

            throw new InvalidOperationException(
                "Không thể cập nhật tài khoản. Tên đăng nhập này có thể đã được sử dụng.",
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
            throw new InvalidOperationException("Không tìm thấy hồ sơ nhân viên cần cập nhật.");
        }

        if (!isActive && employee.UserAccount?.Id == currentUserSession.User?.Id)
        {
            throw new InvalidOperationException("Bạn không thể tự ngừng hoạt động hồ sơ của mình.");
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
            throw new InvalidOperationException("Không tìm thấy tài khoản cần cập nhật.");
        }

        if (!isActive && account.Id == currentUserSession.User?.Id)
        {
            throw new InvalidOperationException("Bạn không thể tự khóa tài khoản đang đăng nhập.");
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
            throw new UnauthorizedAccessException("Chỉ Admin mới được quản lý hồ sơ nhân viên và tài khoản.");
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
            throw new InvalidOperationException("Phải còn ít nhất một tài khoản Admin khác đang hoạt động.");
        }
    }

    private static string ValidateRequiredText(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Thông tin này không được để trống.", parameterName);
        }
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Nội dung không được vượt quá {maxLength} ký tự.", parameterName);
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
            throw new ArgumentException($"Nội dung không được vượt quá {maxLength} ký tự.", parameterName);
        }

        return normalized;
    }

    private static void ValidatePassword(string password)
    {
        if (password is null)
        {
            throw new ArgumentException("Hãy nhập mật khẩu.", nameof(password));
        }
        var passwordByteCount = System.Text.Encoding.UTF8.GetByteCount(password);
        if (passwordByteCount is < 12 or > 72)
        {
            throw new ArgumentException(
                "Mật khẩu phải có ít nhất 12 ký tự và không vượt quá 72 byte UTF-8.",
                nameof(password));
        }
    }

    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 };
}
