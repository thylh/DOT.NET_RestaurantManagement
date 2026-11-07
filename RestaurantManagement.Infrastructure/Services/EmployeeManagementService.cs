using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Core.Entities;
using RestaurantManagement.Core.Enums;
using RestaurantManagement.Core.Models;
using RestaurantManagement.Core.Services;
using RestaurantManagement.Core.Validation;
using RestaurantManagement.Infrastructure.Data;
using RestaurantManagement.Infrastructure.Security;

namespace RestaurantManagement.Infrastructure.Services;

public sealed class EmployeeManagementService(
    RestaurantDbContext dbContext,
    ICurrentUserSession currentUserSession) : IEmployeeManagementService
{
    public async Task<IReadOnlyList<EmployeeAccountSummary>> GetEmployeesAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureAdminOrManager();

        var employees = dbContext.Employees.AsNoTracking();
        if (currentUserSession.User?.Role == UserRole.Manager)
        {
            employees = employees.Where(employee =>
                employee.UserAccount == null || employee.UserAccount.Role == UserRole.Staff);
        }

        return await employees
            .OrderBy(employee => employee.FullName)
            .Select(employee => new EmployeeAccountSummary(
                employee.Id,
                employee.FullName,
                employee.Phone,
                employee.Email,
                employee.IsActive,
                employee.UserAccount == null || employee.UserAccount.IsDeleted
                    ? null
                    : employee.UserAccount.Id,
                employee.UserAccount == null || employee.UserAccount.IsDeleted
                    ? null
                    : employee.UserAccount.Username,
                employee.UserAccount == null || employee.UserAccount.IsDeleted
                    ? null
                    : employee.UserAccount.Role,
                employee.UserAccount != null
                    && !employee.UserAccount.IsDeleted
                    && employee.UserAccount.RequiresPasswordChange))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccountSummary>> GetAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();

        var accounts = await dbContext.UserAccounts
            .AsNoTracking()
            .Where(account => account.Role != UserRole.Admin && !account.IsDeleted)
            .OrderBy(account => account.FullName)
            .Select(account => new
            {
                Summary = new AccountSummary(
                    account.Id,
                    account.EmployeeId,
                    account.Employee == null ? account.FullName : account.Employee.FullName,
                    account.Employee == null ? account.Phone : account.Employee.Phone,
                    account.Employee == null ? account.Email : account.Employee.Email,
                    account.Employee == null || account.Employee.IsActive,
                    account.Username,
                    account.Role,
                    account.RequiresPasswordChange),
                account.EncryptedTemporaryPassword,
                account.EmployeeProfileDeleted
            })
            .ToListAsync(cancellationToken);

        return accounts
            .Select(account => account.Summary with
            {
                EmployeeProfileDeleted = account.EmployeeProfileDeleted,
                TemporaryPassword = account.EncryptedTemporaryPassword is null
                    ? null
                    : TemporaryPasswordProtector.Unprotect(account.EncryptedTemporaryPassword)
            })
            .ToList();
    }

    public async Task<GeneratedEmployeeCredentials> CreateAccountAsync(
        string fullName,
        string? phone,
        string? email,
        UserRole role = UserRole.Staff,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        if (role is not UserRole.Staff and not UserRole.Manager)
        {
            throw new ArgumentOutOfRangeException(nameof(role), "Chỉ có thể cấp tài khoản Quản lý hoặc Nhân viên.");
        }

        var normalizedName = ValidateRequiredText(fullName, 150, nameof(fullName));
        var username = await GenerateUsernameAsync(normalizedName, cancellationToken);
        var temporaryPassword = GenerateTemporaryPassword();
        var account = new UserAccount
        {
            FullName = normalizedName,
            Phone = EmployeeContactValidator.NormalizePhone(phone),
            Email = EmployeeContactValidator.NormalizeEmail(email),
            Username = username,
            PasswordHash = global::BCrypt.Net.BCrypt.HashPassword(temporaryPassword),
            EncryptedTemporaryPassword = TemporaryPasswordProtector.Protect(temporaryPassword),
            Role = role,
            RequiresPasswordChange = true
        };

        dbContext.UserAccounts.Add(account);
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
                "Không thể cấp tài khoản do thông tin bị trùng. Hãy kiểm tra lại dữ liệu rồi thử lại.",
                exception);
        }

        return new GeneratedEmployeeCredentials(account.Id, username, temporaryPassword);
    }

    public async Task CreateEmployeeProfileAsync(
        int accountId,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var account = await dbContext.UserAccounts
            .Include(candidate => candidate.Employee)
            .SingleOrDefaultAsync(candidate => candidate.Id == accountId, cancellationToken);
        if (account is null)
        {
            throw new InvalidOperationException("Không tìm thấy tài khoản cần tạo hồ sơ.");
        }

        EnsureAccountNotDeleted(account);

        if (account.Role is not UserRole.Staff and not UserRole.Manager)
        {
            throw new InvalidOperationException("Chỉ tài khoản Quản lý hoặc Nhân viên mới được tạo hồ sơ.");
        }

        if (account.Employee is not null)
        {
            throw new InvalidOperationException("Tài khoản này đã có hồ sơ nhân viên.");
        }

        account.Employee = new Employee
        {
            FullName = account.FullName,
            Phone = account.Phone,
            Email = account.Email
        };
        account.EmployeeProfileDeleted = false;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAccountEmployeeNameAsync(
        int accountId,
        string fullName,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var account = await dbContext.UserAccounts
            .Include(candidate => candidate.Employee)
            .SingleOrDefaultAsync(candidate => candidate.Id == accountId, cancellationToken);
        if (account is null)
        {
            throw new InvalidOperationException("Không tìm thấy tài khoản cần cập nhật.");
        }

        EnsureAccountNotDeleted(account);

        var normalizedName = ValidateRequiredText(fullName, 150, nameof(fullName));
        var username = string.Equals(account.FullName, normalizedName, StringComparison.Ordinal)
            ? account.Username
            : await GenerateUsernameAsync(normalizedName, cancellationToken, account.Id);
        account.FullName = normalizedName;
        account.Username = username;
        if (account.Employee is not null)
        {
            account.Employee.FullName = normalizedName;
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
                "Không thể cập nhật tên và tên đăng nhập vì tên đăng nhập mới đã được sử dụng. Hãy thử lại.",
                exception);
        }
    }

    public async Task DeleteAccountAsync(
        int accountId,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var account = await dbContext.UserAccounts
            .Include(candidate => candidate.Employee)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == accountId,
                cancellationToken);
        if (account is null)
        {
            throw new InvalidOperationException("Không tìm thấy tài khoản cần xóa.");
        }

        if (account.Id == currentUserSession.User?.Id)
        {
            throw new InvalidOperationException("Bạn không thể xóa tài khoản đang đăng nhập.");
        }

        if (account.IsDeleted)
        {
            throw new InvalidOperationException("Tài khoản này đã bị xóa.");
        }

        if (account.Role == UserRole.Admin)
        {
            await EnsureAnotherActiveAdminExistsAsync(account.Id, account.EmployeeId, cancellationToken);
        }

        if (account.Employee is not null)
        {
            account.Employee.IsActive = false;
        }

        account.IsDeleted = true;
        account.RequiresPasswordChange = false;
        account.EncryptedTemporaryPassword = null;
        account.PasswordHash = global::BCrypt.Net.BCrypt.HashPassword(
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateEmployeeAsync(
        int employeeId,
        string fullName,
        string? phone,
        string? email,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var employee = await dbContext.Employees
            .Include(candidate => candidate.UserAccount)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == employeeId,
                cancellationToken);
        if (employee is null)
        {
            throw new InvalidOperationException("Không tìm thấy hồ sơ nhân viên cần cập nhật.");
        }

        var normalizedName = ValidateRequiredText(fullName, 150, nameof(fullName));
        employee.FullName = normalizedName;
        employee.Phone = EmployeeContactValidator.NormalizePhone(phone);
        employee.Email = EmployeeContactValidator.NormalizeEmail(email);
        if (employee.UserAccount is not null)
        {
            var account = employee.UserAccount;
            if (!account.IsDeleted)
            {
                if (!string.Equals(account.FullName, normalizedName, StringComparison.Ordinal))
                {
                    account.Username = await GenerateUsernameAsync(
                        normalizedName,
                        cancellationToken,
                        account.Id);
                }

                account.FullName = normalizedName;
                account.Phone = employee.Phone;
                account.Email = employee.Email;
            }
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
                "Không thể cập nhật nhân viên vì tên đăng nhập mới đã được sử dụng. Hãy thử lại.",
                exception);
        }
    }

    public async Task DeleteEmployeeProfileAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var employee = await dbContext.Employees
            .Include(candidate => candidate.UserAccount)
            .SingleOrDefaultAsync(candidate => candidate.Id == employeeId, cancellationToken);
        if (employee is null)
        {
            throw new InvalidOperationException("Không tìm thấy hồ sơ nhân viên cần xóa.");
        }

        if (employee.UserAccount?.Id == currentUserSession.User?.Id)
        {
            throw new InvalidOperationException("Bạn không thể xóa hồ sơ đang dùng cho tài khoản đăng nhập hiện tại.");
        }

        var orders = await dbContext.RestaurantOrders
            .Where(order => order.CreatedByEmployeeId == employeeId)
            .ToListAsync(cancellationToken);
        foreach (var order in orders)
        {
            if (string.IsNullOrWhiteSpace(order.CreatedByEmployeeNameSnapshot))
            {
                order.CreatedByEmployeeNameSnapshot = employee.FullName;
            }

            order.CreatedByEmployeeId = null;
        }

        var account = employee.UserAccount;
        dbContext.Employees.Remove(employee);
        if (account is not null)
        {
            account.EmployeeProfileDeleted = true;
        }

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
        if (role is not UserRole.Admin and not UserRole.Staff and not UserRole.Manager)
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

        EnsureAccountNotDeleted(account);

        if (account.Id == currentUserSession.User?.Id && role != UserRole.Admin)
        {
            throw new InvalidOperationException("Bạn không thể tự thay đổi vai trò của tài khoản đang đăng nhập.");
        }

        if (account.Id == currentUserSession.User?.Id && !string.IsNullOrWhiteSpace(newPassword))
        {
            throw new InvalidOperationException(
                "Hãy dùng chức năng Đổi mật khẩu để thay đổi mật khẩu của tài khoản đang đăng nhập.");
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
            account.RequiresPasswordChange = true;
            account.EncryptedTemporaryPassword = TemporaryPasswordProtector.Protect(newPassword);
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
        EnsureAdminOrManager();
        var employee = await dbContext.Employees
            .Include(candidate => candidate.UserAccount)
            .SingleOrDefaultAsync(candidate => candidate.Id == employeeId, cancellationToken);
        if (employee is null)
        {
            throw new InvalidOperationException("Không tìm thấy hồ sơ nhân viên cần cập nhật.");
        }

        if (currentUserSession.User?.Role == UserRole.Manager
            && (employee.UserAccount is not null && employee.UserAccount.Role != UserRole.Staff
                || isActive))
        {
            throw new UnauthorizedAccessException(
                "Quản lý chỉ có quyền đình chỉ nhân viên; chỉ Admin mới có thể bỏ đình chỉ.");
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

        if (isActive && employee.UserAccount?.IsDeleted == true)
        {
            throw new InvalidOperationException("Không thể kích hoạt hồ sơ thuộc tài khoản đã bị xóa.");
        }

        employee.IsActive = isActive;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void EnsureAdmin()
    {
        if (currentUserSession.User?.Role != UserRole.Admin)
        {
            throw new UnauthorizedAccessException("Chỉ Admin mới được quản lý tài khoản.");
        }
    }

    private void EnsureAdminOrManager()
    {
        if (currentUserSession.User?.Role is not UserRole.Admin and not UserRole.Manager)
        {
            throw new UnauthorizedAccessException("Chỉ Admin hoặc Quản lý mới được quản lý hồ sơ nhân viên.");
        }
    }

    private static void EnsureAccountNotDeleted(UserAccount account)
    {
        if (account.IsDeleted)
        {
            throw new InvalidOperationException("Tài khoản này đã bị xóa.");
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
                && !account.IsDeleted
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

    private async Task<string> GenerateUsernameAsync(
        string fullName,
        CancellationToken cancellationToken,
        int? excludedAccountId = null)
    {
        var normalizedName = fullName
            .Replace('đ', 'd')
            .Replace('Đ', 'D')
            .Normalize(NormalizationForm.FormD);
        var tokens = new List<string>();
        var currentToken = new StringBuilder();
        foreach (var character in normalizedName)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character) && character <= 127)
            {
                currentToken.Append(char.ToLowerInvariant(character));
            }
            else if (currentToken.Length > 0)
            {
                tokens.Add(currentToken.ToString());
                currentToken.Clear();
            }
        }

        if (currentToken.Length > 0)
        {
            tokens.Add(currentToken.ToString());
        }

        var baseUsername = tokens.Count switch
        {
            0 => "nhanvien",
            1 => tokens[0],
            _ => $"{tokens[^1]}.{tokens[0]}"
        };
        baseUsername = baseUsername[..Math.Min(baseUsername.Length, 40)];

        for (var suffix = 1; ; suffix++)
        {
            var suffixText = suffix == 1 ? string.Empty : suffix.ToString(CultureInfo.InvariantCulture);
            var candidateLength = Math.Min(baseUsername.Length, 50 - suffixText.Length);
            var candidate = baseUsername[..candidateLength] + suffixText;
            if (!await dbContext.UserAccounts.AnyAsync(
                    account => account.Username == candidate
                        && account.Id != excludedAccountId,
                    cancellationToken))
            {
                return candidate;
            }
        }
    }

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
        exception.InnerException is MySql.Data.MySqlClient.MySqlException { Number: 1062 };
}
