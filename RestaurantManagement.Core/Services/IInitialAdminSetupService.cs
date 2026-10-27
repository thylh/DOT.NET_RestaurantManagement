namespace RestaurantManagement.Core.Services;

public interface IInitialAdminSetupService
{
    Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default);

    Task CreateInitialAdminAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);
}
