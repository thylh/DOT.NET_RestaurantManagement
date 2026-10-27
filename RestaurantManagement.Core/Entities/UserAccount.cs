using RestaurantManagement.Core.Enums;

namespace RestaurantManagement.Core.Entities;

public class UserAccount
{
    public int Id { get; set; }

    public required string Username { get; set; }

    public required string PasswordHash { get; set; }

    public UserRole Role { get; set; }

    public bool IsActive { get; set; } = true;

    public int? EmployeeId { get; set; }

    public Employee? Employee { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public ICollection<RestaurantOrder> CancelledOrders { get; set; } = new List<RestaurantOrder>();
}
