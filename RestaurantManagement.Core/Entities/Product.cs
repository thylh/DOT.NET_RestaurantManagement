namespace RestaurantManagement.Core.Entities;

public class Product
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public string? ImagePath { get; set; }

    public decimal Price { get; set; }

    public bool IsAvailable { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public int CategoryId { get; set; }

    public Category Category { get; set; } = null!;
}
