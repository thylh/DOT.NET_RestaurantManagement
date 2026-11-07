using RestaurantManagement.Core.Models;

namespace RestaurantManagement.WPF.ViewModels;

public sealed class OrderProductCategoryGroup(string categoryName, IReadOnlyList<ProductSummary> products)
{
    public string CategoryName { get; } = categoryName;

    public IReadOnlyList<ProductSummary> Products { get; } = products;
}
