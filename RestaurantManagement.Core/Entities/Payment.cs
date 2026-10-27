using RestaurantManagement.Core.Enums;

namespace RestaurantManagement.Core.Entities;

public class Payment
{
    public int Id { get; set; }

    public int RestaurantOrderId { get; set; }

    public RestaurantOrder RestaurantOrder { get; set; } = null!;

    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; }

    public DateTimeOffset PaidAt { get; set; } = DateTimeOffset.UtcNow;

    public PaymentStatus Status { get; set; } = PaymentStatus.Completed;

    public int PaidByUserId { get; set; }

    public UserAccount PaidByUser { get; set; } = null!;
}
