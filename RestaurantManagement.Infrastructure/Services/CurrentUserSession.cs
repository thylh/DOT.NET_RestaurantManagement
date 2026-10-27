using RestaurantManagement.Core.Models;
using RestaurantManagement.Core.Services;

namespace RestaurantManagement.Infrastructure.Services;

public sealed class CurrentUserSession : ICurrentUserSession
{
    public AuthenticatedUser? User { get; private set; }

    public void SignIn(AuthenticatedUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        User = user;
    }

    public void SignOut() => User = null;
}
