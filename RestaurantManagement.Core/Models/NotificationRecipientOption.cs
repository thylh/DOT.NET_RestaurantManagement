using RestaurantManagement.Core.Enums;

namespace RestaurantManagement.Core.Models;

public sealed record NotificationRecipientOption(
    string Label,
    int? UserId,
    UserRole? Role);
