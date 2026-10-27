using RestaurantManagement.Core.Models;

namespace RestaurantManagement.Core.Services;

public interface ICurrentUserSession
{
    AuthenticatedUser? User { get; }

    void SignIn(AuthenticatedUser user);

    void SignOut();
}
