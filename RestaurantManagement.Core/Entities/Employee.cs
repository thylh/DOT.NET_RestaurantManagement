namespace RestaurantManagement.Core.Entities;

public class Employee
{
    public int Id { get; set; }

    public required string FullName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public UserAccount? UserAccount { get; set; }

    public ICollection<RestaurantOrder> Orders { get; set; } = new List<RestaurantOrder>();
}
