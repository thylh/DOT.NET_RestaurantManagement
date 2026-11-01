using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Core.Enums;
using RestaurantManagement.Core.Models;
using RestaurantManagement.Core.Services;

namespace RestaurantManagement.WPF.ViewModels;

public partial class MainWindowViewModel(
    IInitialAdminSetupService initialAdminSetupService,
    IAuthenticationService authenticationService,
    ICurrentUserSession currentUserSession,
    IEmployeeManagementService employeeManagementService,
    IRestaurantCatalogService restaurantCatalogService,
    ILogger<MainWindowViewModel> logger) : ObservableObject
{
    public ObservableCollection<EmployeeAccountSummary> Employees { get; } = [];

    public ObservableCollection<RestaurantTableSummary> Tables { get; } = [];

    public ObservableCollection<CategorySummary> Categories { get; } = [];

    public ObservableCollection<ProductSummary> Products { get; } = [];

    public IReadOnlyList<UserRole> AvailableRoles { get; } = Enum.GetValues<UserRole>();

    public string Title => "Quản lý nhà hàng";

    public bool IsAuthenticated => currentUserSession.User is not null;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsAdmin => currentUserSession.User?.Role == UserRole.Admin;

    public bool IsSetupScreen => IsSetupRequired;

    public bool IsLoginScreen => !IsSetupRequired && !IsAuthenticated;

    public bool IsAdminScreen => IsAuthenticated && IsAdmin;

    public bool IsStaffScreen => IsAuthenticated && !IsAdmin;

    public string CurrentUserDescription => currentUserSession.User is { } user
        ? $"{user.Username} ({(user.Role == UserRole.Admin ? "Quản trị viên" : "Nhân viên")})"
        : string.Empty;

    [ObservableProperty]
    private bool isSetupRequired;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private string setupUsername = string.Empty;

    [ObservableProperty]
    private string setupPassword = string.Empty;

    [ObservableProperty]
    private string loginUsername = string.Empty;

    [ObservableProperty]
    private string loginPassword = string.Empty;

    [ObservableProperty]
    private string currentPassword = string.Empty;

    [ObservableProperty]
    private string newPassword = string.Empty;

    [ObservableProperty]
    private string employeeName = string.Empty;

    [ObservableProperty]
    private string employeePhone = string.Empty;

    [ObservableProperty]
    private string employeeEmail = string.Empty;

    [ObservableProperty]
    private string accountUsername = string.Empty;

    [ObservableProperty]
    private string accountPassword = string.Empty;

    [ObservableProperty]
    private UserRole selectedRole = UserRole.Staff;

    [ObservableProperty]
    private EmployeeAccountSummary? selectedEmployee;

    [ObservableProperty]
    private RestaurantTableSummary? selectedTable;

    [ObservableProperty]
    private CategorySummary? selectedCategory;

    [ObservableProperty]
    private ProductSummary? selectedProduct;

    [ObservableProperty]
    private CategorySummary? selectedProductCategory;

    [ObservableProperty]
    private CategorySummary? selectedFilterCategory;

    [ObservableProperty]
    private string tableNumberText = string.Empty;

    [ObservableProperty]
    private string tableSeatCountText = string.Empty;

    [ObservableProperty]
    private string categoryName = string.Empty;

    [ObservableProperty]
    private string categoryDescription = string.Empty;

    [ObservableProperty]
    private string productName = string.Empty;

    [ObservableProperty]
    private string productDescription = string.Empty;

    [ObservableProperty]
    private string productPriceText = string.Empty;

    [ObservableProperty]
    private string productImagePath = string.Empty;

    [ObservableProperty]
    private string productSearchText = string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            IsSetupRequired = await initialAdminSetupService.IsSetupRequiredAsync();
            if (!IsSetupRequired)
            {
                StatusMessage = "Đăng nhập để tiếp tục.";
            }
        }
        catch (Exception exception) when (
            exception is DbUpdateException or InvalidOperationException or SqlException)
        {
            logger.LogError(exception, "Không thể kiểm tra trạng thái thiết lập Admin lần đầu.");
            ErrorMessage = "Không thể kiểm tra dữ liệu khởi tạo. Hãy kiểm tra LocalDB rồi khởi động lại ứng dụng.";
        }
    }

    [RelayCommand]
    private async Task CreateInitialAdminAsync()
    {
        await RunOperationAsync(async () =>
        {
            await initialAdminSetupService.CreateInitialAdminAsync(SetupUsername, SetupPassword);
            SetupPassword = string.Empty;
            LoginUsername = SetupUsername.Trim();
            SetupUsername = string.Empty;
            IsSetupRequired = false;
            StatusMessage = "Đã tạo tài khoản Admin. Hãy đăng nhập để tiếp tục.";
        });
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        await RunOperationAsync(async () =>
        {
            var user = await authenticationService.SignInAsync(LoginUsername, LoginPassword);
            LoginPassword = string.Empty;
            if (user is null)
            {
                ErrorMessage = "Tên đăng nhập hoặc mật khẩu không chính xác, hoặc tài khoản đã bị khóa.";
                return;
            }

            OnPropertyChanged(nameof(IsAuthenticated));
            OnPropertyChanged(nameof(IsAdmin));
            OnPropertyChanged(nameof(IsAdminScreen));
            OnPropertyChanged(nameof(IsStaffScreen));
            OnPropertyChanged(nameof(IsLoginScreen));
            OnPropertyChanged(nameof(CurrentUserDescription));
            StatusMessage = $"Đã đăng nhập với tài khoản {user.Username}.";
            if (user.Role == UserRole.Admin)
            {
                await RefreshEmployeesAsync();
            }

            await RefreshCatalogAsync();
        });
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        currentUserSession.SignOut();
        Employees.Clear();
        Tables.Clear();
        Categories.Clear();
        Products.Clear();
        SelectedEmployee = null;
        SelectedTable = null;
        SelectedCategory = null;
        SelectedProduct = null;
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(IsAdmin));
        OnPropertyChanged(nameof(IsAdminScreen));
        OnPropertyChanged(nameof(IsStaffScreen));
        OnPropertyChanged(nameof(IsLoginScreen));
        OnPropertyChanged(nameof(CurrentUserDescription));
        StatusMessage = "Đã đăng xuất.";
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        if (currentUserSession.User is not { } user)
        {
            ErrorMessage = "Hãy đăng nhập trước khi đổi mật khẩu.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await authenticationService.ChangePasswordAsync(user.Id, CurrentPassword, NewPassword);
            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            StatusMessage = "Đã đổi mật khẩu.";
        });
    }

    [RelayCommand]
    private async Task CreateEmployeeAsync()
    {
        await RunOperationAsync(async () =>
        {
            await employeeManagementService.CreateEmployeeAsync(
                EmployeeName,
                EmployeePhone,
                EmployeeEmail,
                AccountUsername,
                AccountPassword,
                SelectedRole);
            EmployeeName = string.Empty;
            EmployeePhone = string.Empty;
            EmployeeEmail = string.Empty;
            AccountUsername = string.Empty;
            AccountPassword = string.Empty;
            await RefreshEmployeesAsync();
            StatusMessage = "Đã tạo hồ sơ nhân viên và tài khoản.";
        });
    }

    [RelayCommand]
    private async Task UpdateEmployeeAsync()
    {
        if (SelectedEmployee is not { } employee)
        {
            ErrorMessage = "Hãy chọn nhân viên cần cập nhật.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await employeeManagementService.UpdateEmployeeAsync(
                employee.EmployeeId,
                EmployeeName,
                EmployeePhone,
                EmployeeEmail);
            await RefreshEmployeesAsync();
            StatusMessage = "Đã cập nhật hồ sơ nhân viên.";
        });
    }

    [RelayCommand]
    private async Task UpdateAccountAsync()
    {
        if (SelectedEmployee?.AccountId is not { } accountId)
        {
            ErrorMessage = "Hãy chọn nhân viên có tài khoản cần cập nhật.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await employeeManagementService.UpdateAccountAsync(
                accountId,
                AccountUsername,
                SelectedRole,
                AccountPassword);
            AccountPassword = string.Empty;
            await RefreshEmployeesAsync();
            StatusMessage = "Đã cập nhật tài khoản.";
        });
    }

    [RelayCommand]
    private async Task ToggleEmployeeStatusAsync()
    {
        if (SelectedEmployee is not { } employee)
        {
            ErrorMessage = "Hãy chọn nhân viên trước.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await employeeManagementService.SetEmployeeActiveAsync(
                employee.EmployeeId,
                !employee.IsEmployeeActive);
            await RefreshEmployeesAsync();
            StatusMessage = "Đã cập nhật trạng thái nhân viên.";
        });
    }

    [RelayCommand]
    private async Task ToggleAccountStatusAsync()
    {
        if (SelectedEmployee is not { AccountId: { } accountId, IsAccountActive: { } isActive })
        {
            ErrorMessage = "Nhân viên đã chọn chưa có tài khoản.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await employeeManagementService.SetAccountActiveAsync(accountId, !isActive);
            await RefreshEmployeesAsync();
            StatusMessage = "Đã cập nhật trạng thái tài khoản.";
        });
    }

    [RelayCommand]
    private async Task CreateTableAsync()
    {
        if (!int.TryParse(TableNumberText, out var tableNumber)
            || !int.TryParse(TableSeatCountText, out var seatCount))
        {
            ErrorMessage = "Số bàn và số chỗ ngồi phải là số nguyên.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await restaurantCatalogService.CreateTableAsync(tableNumber, seatCount);
            TableNumberText = string.Empty;
            TableSeatCountText = string.Empty;
            await RefreshCatalogAsync();
            StatusMessage = "Đã thêm bàn.";
        });
    }

    [RelayCommand]
    private async Task UpdateTableAsync()
    {
        if (SelectedTable is not { } table)
        {
            ErrorMessage = "Hãy chọn bàn cần cập nhật.";
            return;
        }

        if (!int.TryParse(TableNumberText, out var tableNumber)
            || !int.TryParse(TableSeatCountText, out var seatCount))
        {
            ErrorMessage = "Số bàn và số chỗ ngồi phải là số nguyên.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await restaurantCatalogService.UpdateTableAsync(table.Id, tableNumber, seatCount);
            await RefreshCatalogAsync();
            StatusMessage = "Đã cập nhật bàn.";
        });
    }

    [RelayCommand]
    private async Task ToggleTableStatusAsync()
    {
        if (SelectedTable is not { } table)
        {
            ErrorMessage = "Hãy chọn bàn cần cập nhật.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await restaurantCatalogService.SetTableActiveAsync(table.Id, !table.IsActive);
            await RefreshCatalogAsync();
            StatusMessage = "Đã cập nhật trạng thái bàn.";
        });
    }

    [RelayCommand]
    private async Task DeleteTableAsync()
    {
        if (SelectedTable is not { } table)
        {
            ErrorMessage = "Hãy chọn bàn cần xóa.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await restaurantCatalogService.DeleteTableAsync(table.Id);
            SelectedTable = null;
            await RefreshCatalogAsync();
            StatusMessage = "Đã xóa bàn.";
        });
    }

    [RelayCommand]
    private async Task CreateCategoryAsync()
    {
        await RunOperationAsync(async () =>
        {
            await restaurantCatalogService.CreateCategoryAsync(CategoryName, CategoryDescription);
            CategoryName = string.Empty;
            CategoryDescription = string.Empty;
            await RefreshCatalogAsync();
            StatusMessage = "Đã thêm danh mục.";
        });
    }

    [RelayCommand]
    private async Task UpdateCategoryAsync()
    {
        if (SelectedCategory is not { } category)
        {
            ErrorMessage = "Hãy chọn danh mục cần cập nhật.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await restaurantCatalogService.UpdateCategoryAsync(
                category.Id,
                CategoryName,
                CategoryDescription);
            await RefreshCatalogAsync();
            StatusMessage = "Đã cập nhật danh mục.";
        });
    }

    [RelayCommand]
    private async Task ToggleCategoryStatusAsync()
    {
        if (SelectedCategory is not { } category)
        {
            ErrorMessage = "Hãy chọn danh mục cần cập nhật.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await restaurantCatalogService.SetCategoryActiveAsync(category.Id, !category.IsActive);
            await RefreshCatalogAsync();
            StatusMessage = "Đã cập nhật trạng thái danh mục.";
        });
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync()
    {
        if (SelectedCategory is not { } category)
        {
            ErrorMessage = "Hãy chọn danh mục cần xóa.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await restaurantCatalogService.DeleteCategoryAsync(category.Id);
            SelectedCategory = null;
            await RefreshCatalogAsync();
            StatusMessage = "Đã xóa danh mục.";
        });
    }

    [RelayCommand]
    private async Task CreateProductAsync()
    {
        if (!TryParsePrice(out var price))
        {
            return;
        }

        if (SelectedProductCategory is not { } category)
        {
            ErrorMessage = "Hãy chọn danh mục cho món ăn.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await restaurantCatalogService.CreateProductAsync(
                ProductName,
                ProductDescription,
                price,
                category.Id,
                ProductImagePath);
            ProductName = string.Empty;
            ProductDescription = string.Empty;
            ProductPriceText = string.Empty;
            ProductImagePath = string.Empty;
            SelectedProduct = null;
            await RefreshCatalogAsync();
            StatusMessage = "Đã thêm món ăn.";
        });
    }

    [RelayCommand]
    private async Task UpdateProductAsync()
    {
        if (SelectedProduct is not { } product)
        {
            ErrorMessage = "Hãy chọn món ăn cần cập nhật.";
            return;
        }

        if (!TryParsePrice(out var price))
        {
            return;
        }

        if (SelectedProductCategory is not { } category)
        {
            ErrorMessage = "Hãy chọn danh mục cho món ăn.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await restaurantCatalogService.UpdateProductAsync(
                product.Id,
                ProductName,
                ProductDescription,
                price,
                category.Id,
                ProductImagePath);
            await RefreshCatalogAsync();
            StatusMessage = "Đã cập nhật món ăn.";
        });
    }

    [RelayCommand]
    private async Task ToggleProductAvailabilityAsync()
    {
        if (SelectedProduct is not { } product)
        {
            ErrorMessage = "Hãy chọn món ăn cần cập nhật.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await restaurantCatalogService.SetProductAvailabilityAsync(product.Id, !product.IsAvailable);
            await RefreshCatalogAsync();
            StatusMessage = "Đã cập nhật trạng thái còn/hết món.";
        });
    }

    [RelayCommand]
    private async Task ToggleProductStatusAsync()
    {
        if (SelectedProduct is not { } product)
        {
            ErrorMessage = "Hãy chọn món ăn cần cập nhật.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await restaurantCatalogService.SetProductActiveAsync(product.Id, !product.IsActive);
            await RefreshCatalogAsync();
            StatusMessage = "Đã cập nhật trạng thái kinh doanh của món.";
        });
    }

    [RelayCommand]
    private async Task DeleteProductAsync()
    {
        if (SelectedProduct is not { } product)
        {
            ErrorMessage = "Hãy chọn món ăn cần xóa.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await restaurantCatalogService.DeleteProductAsync(product.Id);
            SelectedProduct = null;
            await RefreshCatalogAsync();
            StatusMessage = "Đã xóa món ăn.";
        });
    }

    [RelayCommand]
    private async Task SearchProductsAsync() => await RefreshProductsAsync();

    private async Task RefreshEmployeesAsync()
    {
        var employees = await employeeManagementService.GetEmployeesAsync();
        Employees.Clear();
        foreach (var employee in employees)
        {
            Employees.Add(employee);
        }
    }

    private async Task RefreshCatalogAsync()
    {
        var selectedTableId = SelectedTable?.Id;
        var selectedCategoryId = SelectedCategory?.Id;
        var selectedProductId = SelectedProduct?.Id;
        var selectedProductCategoryId = SelectedProductCategory?.Id;
        var selectedFilterCategoryId = SelectedFilterCategory?.Id;
        var tables = await restaurantCatalogService.GetTablesAsync();
        var categories = await restaurantCatalogService.GetCategoriesAsync();

        Tables.Clear();
        foreach (var table in tables)
        {
            Tables.Add(table);
        }

        Categories.Clear();
        foreach (var category in categories)
        {
            Categories.Add(category);
        }

        SelectedTable = Tables.FirstOrDefault(table => table.Id == selectedTableId);
        SelectedCategory = Categories.FirstOrDefault(category => category.Id == selectedCategoryId);
        SelectedProductCategory = Categories.FirstOrDefault(category => category.Id == selectedProductCategoryId);
        SelectedFilterCategory = Categories.FirstOrDefault(category => category.Id == selectedFilterCategoryId);
        await RefreshProductsAsync(selectedProductId);
    }

    private async Task RefreshProductsAsync(int? selectedProductId = null)
    {
        var products = await restaurantCatalogService.GetProductsAsync(
            ProductSearchText,
            SelectedFilterCategory?.Id);
        Products.Clear();
        foreach (var product in products)
        {
            Products.Add(product);
        }

        SelectedProduct = Products.FirstOrDefault(product => product.Id == selectedProductId);
    }

    private bool TryParsePrice(out decimal price)
    {
        if (decimal.TryParse(
                ProductPriceText,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out price))
        {
            return true;
        }

        ErrorMessage = "Giá bán không đúng định dạng.";
        return false;
    }

    private async Task RunOperationAsync(Func<Task> operation)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            await operation();
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or UnauthorizedAccessException
                or DbUpdateException
                or SqlException)
        {
            if (exception is DbUpdateException or SqlException)
            {
                logger.LogError(exception, "Thao tác thất bại khi truy cập cơ sở dữ liệu.");
                ErrorMessage = "Không thể lưu thay đổi. Hãy kiểm tra kết nối cơ sở dữ liệu rồi thử lại.";
            }
            else
            {
                logger.LogWarning(exception, "Không thể hoàn tất thao tác được yêu cầu.");
                ErrorMessage = exception.Message;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedEmployeeChanged(EmployeeAccountSummary? value)
    {
        if (value is null)
        {
            return;
        }

        EmployeeName = value.FullName;
        EmployeePhone = value.Phone ?? string.Empty;
        EmployeeEmail = value.Email ?? string.Empty;
        AccountUsername = value.Username ?? string.Empty;
        SelectedRole = value.Role ?? UserRole.Staff;
        AccountPassword = string.Empty;
    }

    partial void OnSelectedTableChanged(RestaurantTableSummary? value)
    {
        if (value is null)
        {
            return;
        }

        TableNumberText = value.TableNumber.ToString(CultureInfo.CurrentCulture);
        TableSeatCountText = value.SeatCount.ToString(CultureInfo.CurrentCulture);
    }

    partial void OnSelectedCategoryChanged(CategorySummary? value)
    {
        if (value is null)
        {
            return;
        }

        CategoryName = value.Name;
        CategoryDescription = value.Description ?? string.Empty;
    }

    partial void OnSelectedProductChanged(ProductSummary? value)
    {
        if (value is null)
        {
            return;
        }

        ProductName = value.Name;
        ProductDescription = value.Description ?? string.Empty;
        ProductPriceText = value.Price.ToString("0.##", CultureInfo.CurrentCulture);
        ProductImagePath = value.ImagePath ?? string.Empty;
        SelectedProductCategory = Categories.FirstOrDefault(category => category.Id == value.CategoryId);
    }

    partial void OnIsSetupRequiredChanged(bool value)
    {
        OnPropertyChanged(nameof(IsSetupScreen));
        OnPropertyChanged(nameof(IsLoginScreen));
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
}
