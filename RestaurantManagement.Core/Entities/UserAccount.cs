using RestaurantManagement.Core.Enums;

namespace RestaurantManagement.Core.Entities;

public class UserAccount
{
    public int Id { get; set; }

    public required string Username { get; set; }

    public required string FullName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public required string PasswordHash { get; set; }

    public string? EncryptedTemporaryPassword { get; set; }

    public UserRole Role { get; set; }

    public bool RequiresPasswordChange { get; set; }

    public bool IsActive { get; set; } = true;

    public int? EmployeeId { get; set; }

    public Employee? Employee { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public ICollection<RestaurantOrder> CancelledOrders { get; set; } = new List<RestaurantOrder>();

    public ICollection<RestaurantOrder> CreatedOrders { get; set; } = new List<RestaurantOrder>();
}
