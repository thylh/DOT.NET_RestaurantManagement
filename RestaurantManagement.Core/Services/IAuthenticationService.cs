using RestaurantManagement.Core.Models;

namespace RestaurantManagement.Core.Services;

public interface IAuthenticationService
{
    Task<AuthenticatedUser?> SignInAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    Task ChangePasswordAsync(
        int userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);
}
