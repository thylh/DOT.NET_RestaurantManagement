using CommunityToolkit.Mvvm.ComponentModel;

namespace RestaurantManagement.WPF.ViewModels;

public sealed class ProductTagOption(string name) : ObservableObject
{
    private bool isSelected;

    public string Name { get; } = name;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }
}
