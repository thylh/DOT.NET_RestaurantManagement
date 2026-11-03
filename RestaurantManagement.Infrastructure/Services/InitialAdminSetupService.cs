using System.Data;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Core.Entities;
using RestaurantManagement.Core.Enums;
using RestaurantManagement.Core.Services;
using RestaurantManagement.Infrastructure.Data;

namespace RestaurantManagement.Infrastructure.Services;

public sealed class InitialAdminSetupService(RestaurantDbContext dbContext) : IInitialAdminSetupService
{
    public async Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default) =>
        !await dbContext.UserAccounts.AnyAsync(cancellationToken);

    public async Task CreateInitialAdminAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Hãy nhập tên đăng nhập.", nameof(username));
        }

        if (password is null)
        {
            throw new ArgumentException("Hãy nhập mật khẩu.", nameof(password));
        }
        var normalizedUsername = username.Trim();
        if (normalizedUsername.Length is < 3 or > 50)
        {
            throw new ArgumentException("Tên đăng nhập phải có từ 3 đến 50 ký tự.", nameof(username));
        }

        ValidatePassword(password);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        if (await dbContext.UserAccounts.AnyAsync(cancellationToken))
        {
            throw new InitialAdminAlreadyExistsException();
        }

        var account = new UserAccount
        {
            Username = normalizedUsername,
            FullName = normalizedUsername,
            PasswordHash = global::BCrypt.Net.BCrypt.HashPassword(password),
            Role = UserRole.Admin
        };

        dbContext.UserAccounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static void ValidatePassword(string password)
    {
        var passwordByteCount = System.Text.Encoding.UTF8.GetByteCount(password);
        if (passwordByteCount is < 12 or > 72)
        {
            throw new ArgumentException(
                "Mật khẩu phải có ít nhất 12 ký tự và không vượt quá 72 byte UTF-8.",
                nameof(password));
        }
    }
}
