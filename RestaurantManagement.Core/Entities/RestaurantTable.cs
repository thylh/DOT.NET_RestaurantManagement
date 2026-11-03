namespace RestaurantManagement.Core.Entities;

public class RestaurantTable
{
    public int Id { get; set; }

    public string TableNumber { get; set; } = string.Empty;

    public string Area { get; set; } = "Chung";

    public int SeatCount { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<RestaurantOrder> Orders { get; set; } = new List<RestaurantOrder>();
}
