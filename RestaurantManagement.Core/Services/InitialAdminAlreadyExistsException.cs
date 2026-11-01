namespace RestaurantManagement.Core.Services;

public sealed class InitialAdminAlreadyExistsException : InvalidOperationException
{
    public InitialAdminAlreadyExistsException()
        : base("Chỉ có thể thiết lập Admin lần đầu khi chưa có tài khoản nào.")
    {
    }
}
