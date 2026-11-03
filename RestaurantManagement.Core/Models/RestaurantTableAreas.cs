namespace RestaurantManagement.Core.Models;

public static class RestaurantTableAreas
{
    public const string Default = "Chung";

    public static IReadOnlyList<string> Values { get; } =
        Array.AsReadOnly(new[] { "Chung", "Riêng tư", "VIP", "Ngoài trời" });
}
