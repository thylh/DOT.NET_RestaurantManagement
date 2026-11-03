namespace RestaurantManagement.Tests;

public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("RESTAURANT_TEST_CONNECTION_STRING")))
        {
            Skip = "Thiết lập RESTAURANT_TEST_CONNECTION_STRING trỏ tới database thử nghiệm có tên kết thúc bằng _test.";
        }
    }
}
