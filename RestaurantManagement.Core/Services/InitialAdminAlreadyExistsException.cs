namespace RestaurantManagement.Core.Services;

public sealed class InitialAdminAlreadyExistsException : InvalidOperationException
{
    public InitialAdminAlreadyExistsException()
        : base("Initial Admin setup is only available before any user account exists.")
    {
    }
}
