namespace RestaurantManagement.Core.Entities;

public class Promotion
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public decimal DiscountPercentage { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public bool IsActive { get; set; } = true;

    public int? ProductId { get; set; }

    public Product? Product { get; set; }

    public int? CategoryId { get; set; }

    public Category? Category { get; set; }
}
