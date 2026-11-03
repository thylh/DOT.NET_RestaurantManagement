using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Core.Enums;
using RestaurantManagement.Core.Models;
using RestaurantManagement.Core.Services;
using RestaurantManagement.Infrastructure.Data;

namespace RestaurantManagement.Infrastructure.Services;

public sealed class AuthenticationService(
    RestaurantDbContext dbContext,
    ICurrentUserSession currentUserSession) : IAuthenticationService
{
    public async Task<AuthenticatedUser?> SignInAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Hãy nhập tên đăng nhập.", nameof(username));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Hãy nhập mật khẩu.", nameof(password));
        }

        var normalizedUsername = username.Trim();
        var account = await dbContext.UserAccounts
            .Include(candidate => candidate.Employee)
            .SingleOrDefaultAsync(candidate => candidate.Username == normalizedUsername, cancellationToken);

        if (account is null)
        {
            throw new InvalidOperationException("Tài khoản không tồn tại.");
        }

        if (account.IsDeleted)
        {
            throw new InvalidOperationException("Tài khoản không tồn tại.");
        }

        var employee = account.Employee;
        if (!global::BCrypt.Net.BCrypt.Verify(password, account.PasswordHash))
        {
            return null;
        }

        if (employee is not null && !employee.IsActive)
        {
            throw new InvalidOperationException("Nhân viên đang bị tạm đình chỉ.");
        }

        var user = new AuthenticatedUser(
            account.Id,
            account.Username,
            account.Role,
            account.EmployeeId,
            employee?.FullName ?? account.FullName,
            account.RequiresPasswordChange && employee is not null);
        currentUserSession.SignIn(user);

        return user;
    }

    public async Task ChangePasswordAsync(
        int userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (currentUserSession.User?.Id != userId)
        {
            throw new UnauthorizedAccessException("Bạn chỉ có thể đổi mật khẩu của tài khoản đang đăng nhập.");
        }

        if (currentPassword is null)
        {
            throw new ArgumentException("Hãy nhập mật khẩu hiện tại.", nameof(currentPassword));
        }

        if (newPassword is null)
        {
            throw new ArgumentException("Hãy nhập mật khẩu mới.", nameof(newPassword));
        }
        var passwordByteCount = System.Text.Encoding.UTF8.GetByteCount(newPassword);
        if (passwordByteCount is < 12 or > 72)
        {
            throw new ArgumentException(
                "Mật khẩu mới phải có ít nhất 12 ký tự và không vượt quá 72 byte UTF-8.",
                nameof(newPassword));
        }

        var account = await dbContext.UserAccounts.SingleOrDefaultAsync(
            candidate => candidate.Id == userId,
            cancellationToken);
        if (account is null)
        {
            currentUserSession.SignOut();
            throw new UnauthorizedAccessException("Tài khoản đang đăng nhập không còn hoạt động.");
        }

        if (!global::BCrypt.Net.BCrypt.Verify(currentPassword, account.PasswordHash))
        {
            throw new InvalidOperationException("Mật khẩu hiện tại không chính xác.");
        }

        account.PasswordHash = global::BCrypt.Net.BCrypt.HashPassword(newPassword);
        account.RequiresPasswordChange = false;
        account.EncryptedTemporaryPassword = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (currentUserSession.User is { } currentUser)
        {
            currentUserSession.SignIn(currentUser with { RequiresPasswordChange = false });
        }
    }
}
