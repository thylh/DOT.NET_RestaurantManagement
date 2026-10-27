namespace RestaurantManagement.Core.Entities;

public class OrderDetail
{
    public int Id { get; set; }

    public int RestaurantOrderId { get; set; }

    public RestaurantOrder RestaurantOrder { get; set; } = null!;

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public required string ProductNameSnapshot { get; set; }

    public decimal UnitPriceSnapshot { get; set; }

    public int Quantity { get; set; }

    public string? Notes { get; set; }
}
