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
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);
        var normalizedUsername = username.Trim();
        if (normalizedUsername.Length is < 3 or > 50)
        {
            throw new ArgumentException("Username must contain between 3 and 50 characters.", nameof(username));
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
                "Password must contain at least 12 characters and no more than 72 UTF-8 bytes.",
                nameof(password));
        }
    }
}
