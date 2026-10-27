namespace RestaurantManagement.Core.Entities;

public class RestaurantTable
{
    public int Id { get; set; }

    public int TableNumber { get; set; }

    public int SeatCount { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<RestaurantOrder> Orders { get; set; } = new List<RestaurantOrder>();
}
