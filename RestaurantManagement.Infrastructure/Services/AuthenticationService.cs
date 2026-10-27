using Microsoft.EntityFrameworkCore;
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
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var normalizedUsername = username.Trim();
        var account = await dbContext.UserAccounts
            .Include(candidate => candidate.Employee)
            .SingleOrDefaultAsync(candidate => candidate.Username == normalizedUsername, cancellationToken);

        if (account is null)
        {
            return null;
        }

        var employee = account.Employee;
        if (!account.IsActive
            || (employee is not null && !employee.IsActive)
            || !global::BCrypt.Net.BCrypt.Verify(password, account.PasswordHash))
        {
            return null;
        }

        var user = new AuthenticatedUser(
            account.Id,
            account.Username,
            account.Role,
            account.EmployeeId,
            employee?.FullName);
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
            throw new UnauthorizedAccessException("You can only change the password of the signed-in account.");
        }

        ArgumentNullException.ThrowIfNull(currentPassword);
        ArgumentNullException.ThrowIfNull(newPassword);
        var passwordByteCount = System.Text.Encoding.UTF8.GetByteCount(newPassword);
        if (passwordByteCount is < 12 or > 72)
        {
            throw new ArgumentException(
                "New password must contain at least 12 characters and no more than 72 UTF-8 bytes.",
                nameof(newPassword));
        }

        var account = await dbContext.UserAccounts.SingleOrDefaultAsync(
            candidate => candidate.Id == userId,
            cancellationToken);
        if (account is null || !account.IsActive)
        {
            currentUserSession.SignOut();
            throw new UnauthorizedAccessException("The signed-in account is no longer active.");
        }

        if (!global::BCrypt.Net.BCrypt.Verify(currentPassword, account.PasswordHash))
        {
            throw new InvalidOperationException("The current password is incorrect.");
        }

        account.PasswordHash = global::BCrypt.Net.BCrypt.HashPassword(newPassword);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
