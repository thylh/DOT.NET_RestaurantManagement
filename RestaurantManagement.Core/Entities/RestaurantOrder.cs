using RestaurantManagement.Core.Enums;

namespace RestaurantManagement.Core.Entities;

public class RestaurantOrder
{
    public int Id { get; set; }

    public int RestaurantTableId { get; set; }

    public RestaurantTable RestaurantTable { get; set; } = null!;

    public int? CreatedByEmployeeId { get; set; }

    public Employee? CreatedByEmployee { get; set; }

    public string CreatedByEmployeeNameSnapshot { get; set; } = string.Empty;

    public int? CreatedByUserId { get; set; }

    public UserAccount? CreatedByUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public OrderStatus Status { get; set; } = OrderStatus.Open;

    public DateTimeOffset? CancelledAt { get; set; }

    public int? CancelledByUserId { get; set; }

    public UserAccount? CancelledByUser { get; set; }

    public string? CancellationReason { get; set; }

    public string? AppliedPromotionNameSnapshot { get; set; }

    public decimal DiscountAmountSnapshot { get; set; }

    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public Payment? Payment { get; set; }
}
