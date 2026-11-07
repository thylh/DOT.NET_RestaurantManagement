using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
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
    ILogger<MainWindowViewModel> logger,
    IRestaurantOrderService? restaurantOrderService = null) : ObservableObject
{
    private ObservableCollection<ProductTagOption>? _productTagOptions;
    private IReadOnlyList<ProductSummary> _catalogProducts = [];

    public ObservableCollection<EmployeeAccountSummary> Employees { get; } = [];

    public ObservableCollection<EmployeeAccountSummary> FilteredEmployees { get; } = [];

    public ObservableCollection<AccountSummary> Accounts { get; } = [];

    public ObservableCollection<AccountSummary> FilteredAccounts { get; } = [];

    public ObservableCollection<string> AccountRoleFilters { get; } =
        ["Tất cả vai trò", "Quản trị viên", "Nhân viên"];

    public ObservableCollection<string> AccountProfileFilters { get; } =
        ["Tất cả hồ sơ", "Đã tạo hồ sơ", "Chưa tạo hồ sơ", "Hồ sơ đã bị xóa"];

    public ObservableCollection<string> EmployeeStatusFilters { get; } =
        ["Tất cả trạng thái", "Đang làm việc", "Tạm đình chỉ"];

    public ObservableCollection<RestaurantTableSummary> Tables { get; } = [];

    public ObservableCollection<OrderWorkspaceTab> OrderTabs { get; } = [];

    public ObservableCollection<RestaurantTableSummary> FilteredTables { get; } = [];

    public ObservableCollection<RestaurantTableSummary> FilteredOrderTables { get; } = [];

    public ObservableCollection<string> TableAreas { get; } = [.. RestaurantTableAreas.Values];

    public ObservableCollection<string> TableAreaFilters { get; } = ["Tất cả khu vực", .. RestaurantTableAreas.Values];

    public ObservableCollection<string> OrderTableAreaFilters { get; } =
        ["Tất cả khu vực", .. RestaurantTableAreas.Values];

    public ObservableCollection<string> OrderTableStatusFilters { get; } =
        ["Tất cả trạng thái", "Trống", "Đang có order"];

    public ObservableCollection<string> TableServiceStatusFilters { get; } =
        ["Tất cả trạng thái", "Đang phục vụ", "Ngừng phục vụ"];

    public ObservableCollection<CategorySummary> Categories { get; } = [];

    public ObservableCollection<ProductSummary> Products { get; } = [];

    public ObservableCollection<ProductSummary> PromotionProducts { get; } = [];

    public ObservableCollection<ProductSummary> OrderableProducts { get; } = [];

    public ObservableCollection<PromotionSummary> Promotions { get; } = [];

    public ObservableCollection<string> ProductCategoryFilters { get; } = ["Tất cả danh mục"];

    public ObservableCollection<string> PromotionTargetTypes { get; } = ["Món ăn", "Danh mục"];

    public ObservableCollection<ProductTagOption> ProductTagOptions =>
        _productTagOptions ??= CreateProductTagOptions();

    public static string Title => "DineEase - Quản lý nhà hàng";

    public bool IsAuthenticated => currentUserSession.User is not null;

    public bool IsReadOnlyStaff => currentUserSession.User is
    { Role: UserRole.Staff, EmployeeId: null };

    public bool IsStaffOrderScreen => IsStaffScreen && !IsReadOnlyStaff;

    public bool CanToggleProductAvailability => IsAdminScreen || IsStaffOrderScreen;

    public bool IsPromotionProductTarget => SelectedPromotionTargetType == "Món ăn";

    public bool IsPromotionCategoryTarget => SelectedPromotionTargetType == "Danh mục";

    public string ReadOnlyStaffWarning =>
        "Bạn chỉ có thể xem nghiệp vụ, không thể thao tác ảnh hưởng đến dữ liệu. "
        + "Hãy đổi mật khẩu được cấp để tránh rủi ro không đáng có.";

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsPasswordChangeRequired => currentUserSession.User is
    { RequiresPasswordChange: true, Role: not UserRole.Admin };

    public bool IsAuthenticatedWorkspace => IsAuthenticated && !IsPasswordChangeRequired;

    public bool IsAdmin => currentUserSession.User?.Role == UserRole.Admin;

    public bool IsSetupScreen => IsSetupRequired;

    public bool IsLoginScreen => !IsSetupRequired && !IsAuthenticated;

    public bool IsAdminScreen => IsAuthenticatedWorkspace && IsAdmin;

    public bool IsStaffScreen => IsAuthenticatedWorkspace && !IsAdmin;

    public string CurrentUserDescription => currentUserSession.User is { } user
        ? $"{user.Username} ({(user.Role == UserRole.Admin ? "Quản trị viên" : "Nhân viên")})"
        : string.Empty;

    [ObservableProperty]
    private bool isSetupRequired;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    partial void OnStatusMessageChanged(string value) =>
        OnPropertyChanged(nameof(HasStatusMessage));

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
    private string accountEmployeeName = string.Empty;

    [ObservableProperty]
    private string accountEmployeePhone = string.Empty;

    [ObservableProperty]
    private string accountEmployeeEmail = string.Empty;

    [ObservableProperty]
    private EmployeeAccountSummary? selectedEmployee;

    [ObservableProperty]
    private AccountSummary? selectedAccount;

    [ObservableProperty]
    private string editAccountEmployeeName = string.Empty;

    [ObservableProperty]
    private string accountSearchText = string.Empty;

    [ObservableProperty]
    private string selectedAccountRoleFilter = "Tất cả vai trò";

    [ObservableProperty]
    private string selectedAccountProfileFilter = "Tất cả hồ sơ";

    [ObservableProperty]
    private string employeeSearchText = string.Empty;

    [ObservableProperty]
    private string selectedEmployeeStatusFilter = "Tất cả trạng thái";

    [ObservableProperty]
    private RestaurantTableSummary? selectedTable;

    [ObservableProperty]
    private RestaurantTableSummary? selectedOrderTable;

    [ObservableProperty]
    private OrderWorkspaceTab? selectedOrderTab;

    [ObservableProperty]
    private ProductSummary? selectedOrderProduct;

    [ObservableProperty]
    private OpenOrderSummary? currentOpenOrder;

    [ObservableProperty]
    private OrderDetailSummary? selectedOrderDetail;

    [ObservableProperty]
    private CategorySummary? selectedCategory;

    [ObservableProperty]
    private ProductSummary? selectedProduct;

    [ObservableProperty]
    private PromotionSummary? selectedPromotion;

    [ObservableProperty]
    private ProductSummary? selectedPromotionProduct;

    [ObservableProperty]
    private CategorySummary? selectedPromotionCategory;

    [ObservableProperty]
    private string selectedPromotionTargetType = "Danh mục";

    [ObservableProperty]
    private CategorySummary? selectedProductCategory;

    [ObservableProperty]
    private string selectedProductCategoryFilter = "Tất cả danh mục";

    [ObservableProperty]
    private string tableNumberText = string.Empty;

    [ObservableProperty]
    private string tableSearchText = string.Empty;

    [ObservableProperty]
    private string selectedTableAreaFilter = "Tất cả khu vực";

    [ObservableProperty]
    private string selectedTableServiceStatusFilter = "Tất cả trạng thái";

    [ObservableProperty]
    private string orderTableSearchText = string.Empty;

    [ObservableProperty]
    private string selectedOrderTableAreaFilter = "Tất cả khu vực";

    [ObservableProperty]
    private string selectedOrderTableStatusFilter = "Tất cả trạng thái";

    [ObservableProperty]
    private string tableAreaText = RestaurantTableAreas.Default;

    [ObservableProperty]
    private string tableSeatCountText = string.Empty;

    [ObservableProperty]
    private string orderQuantityText = "1";

    [ObservableProperty]
    private string orderItemNotes = string.Empty;

    [ObservableProperty]
    private string editOrderQuantityText = string.Empty;

    [ObservableProperty]
    private string editOrderItemNotes = string.Empty;

    [ObservableProperty]
    private string orderCancellationReason = string.Empty;

    [ObservableProperty]
    private string categoryName = string.Empty;

    [ObservableProperty]
    private string productName = string.Empty;

    [ObservableProperty]
    private string productTags = string.Empty;

    public string ProductTagsDisplay => string.IsNullOrWhiteSpace(ProductTags)
        ? "Chưa chọn nhãn"
        : ProductTags;

    [ObservableProperty]
    private bool isProductTagsDropDownOpen;

    [ObservableProperty]
    private string productPriceText = string.Empty;

    [ObservableProperty]
    private string productImagePath = string.Empty;

    [ObservableProperty]
    private string productSearchText = string.Empty;

    [ObservableProperty]
    private string promotionName = string.Empty;

    [ObservableProperty]
    private string promotionDiscountPercentageText = string.Empty;

    [ObservableProperty]
    private DateTime? promotionStartsAt = DateTime.Today;

    [ObservableProperty]
    private DateTime? promotionEndsAt = DateTime.Today.AddDays(1);

    partial void OnProductSearchTextChanged(string value) => ApplyProductFilters();

    partial void OnSelectedProductCategoryFilterChanged(string value) => ApplyProductFilters();

    partial void OnSelectedPromotionTargetTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsPromotionProductTarget));
        OnPropertyChanged(nameof(IsPromotionCategoryTarget));
        if (value == "Món ăn")
        {
            SelectedPromotionCategory = null;
        }
        else
        {
            SelectedPromotionProduct = null;
        }
    }

    partial void OnTableSearchTextChanged(string value) => ApplyTableFilters();

    partial void OnSelectedTableAreaFilterChanged(string value) => ApplyTableFilters();

    partial void OnSelectedTableServiceStatusFilterChanged(string value) => ApplyTableFilters();

    partial void OnOrderTableSearchTextChanged(string value) => ApplyOrderTableFilters();

    partial void OnSelectedOrderTableAreaFilterChanged(string value) => ApplyOrderTableFilters();

    partial void OnSelectedOrderTableStatusFilterChanged(string value) => ApplyOrderTableFilters();

    partial void OnAccountSearchTextChanged(string value) => ApplyAccountFilters();

    partial void OnSelectedAccountRoleFilterChanged(string value) => ApplyAccountFilters();

    partial void OnSelectedAccountProfileFilterChanged(string value) => ApplyAccountFilters();

    partial void OnEmployeeSearchTextChanged(string value) => ApplyEmployeeFilters();

    partial void OnSelectedEmployeeStatusFilterChanged(string value) => ApplyEmployeeFilters();

    partial void OnProductTagsChanged(string value) =>
        OnPropertyChanged(nameof(ProductTagsDisplay));

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
            exception is DbUpdateException or InvalidOperationException or MySqlException)
        {
            logger.LogError(exception, "Không thể kiểm tra trạng thái thiết lập Admin lần đầu.");
            ErrorMessage = "Không thể kiểm tra dữ liệu khởi tạo. Hãy kiểm tra kết nối MySQL rồi khởi động lại ứng dụng.";
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
            AuthenticatedUser? user;
            try
            {
                user = await authenticationService.SignInAsync(LoginUsername, LoginPassword);
            }
            finally
            {
                LoginPassword = string.Empty;
            }

            if (user is null)
            {
                ErrorMessage = "Tên đăng nhập hoặc mật khẩu không chính xác.";
                return;
            }

            OnPropertyChanged(nameof(IsAuthenticated));
            OnPropertyChanged(nameof(IsReadOnlyStaff));
            OnPropertyChanged(nameof(IsStaffOrderScreen));
            OnPropertyChanged(nameof(CanToggleProductAvailability));
            OnPropertyChanged(nameof(IsAdmin));
            OnPropertyChanged(nameof(IsPasswordChangeRequired));
            OnPropertyChanged(nameof(IsAuthenticatedWorkspace));
            OnPropertyChanged(nameof(IsAdminScreen));
            OnPropertyChanged(nameof(IsStaffScreen));
            OnPropertyChanged(nameof(IsLoginScreen));
            OnPropertyChanged(nameof(CurrentUserDescription));
            if (IsStaffOrderScreen && OrderTabs.Count == 0)
            {
                AddNewOrderTab();
            }

            StatusMessage = $"Đã đăng nhập với tài khoản {user.Username}.";
            if (IsPasswordChangeRequired)
            {
                StatusMessage = "Đây là lần đăng nhập đầu tiên. Hãy đổi mật khẩu để tiếp tục.";
            }
            else if (IsReadOnlyStaff)
            {
                StatusMessage = string.Empty;
                await RefreshCatalogAsync();
            }
            else if (user.Role == UserRole.Admin)
            {
                await RefreshEmployeesAsync();
                await RefreshAccountsAsync();
                await RefreshCatalogAsync();
            }
            else
            {
                await RefreshCatalogAsync();
            }
        });
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        currentUserSession.SignOut();
        Employees.Clear();
        Accounts.Clear();
        Tables.Clear();
        Categories.Clear();
        Products.Clear();
        OrderableProducts.Clear();
        OrderTabs.Clear();
        SelectedOrderTab = null;
        CurrentOpenOrder = null;
        SelectedOrderDetail = null;
        SelectedOrderTable = null;
        SelectedOrderProduct = null;
        OrderCancellationReason = string.Empty;
        OrderItemNotes = string.Empty;
        SelectedEmployee = null;
        SelectedAccount = null;
        SelectedTable = null;
        SelectedCategory = null;
        SelectedProduct = null;
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(IsReadOnlyStaff));
        OnPropertyChanged(nameof(IsStaffOrderScreen));
        OnPropertyChanged(nameof(CanToggleProductAvailability));
        OnPropertyChanged(nameof(IsAdmin));
        OnPropertyChanged(nameof(IsPasswordChangeRequired));
        OnPropertyChanged(nameof(IsAuthenticatedWorkspace));
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
            await SignOutAsync();
            StatusMessage = "Đổi mật khẩu thành công. Hãy đăng nhập lại.";
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
                EmployeeEmail);
            EmployeeName = string.Empty;
            EmployeePhone = string.Empty;
            EmployeeEmail = string.Empty;
            await RefreshEmployeesAsync();
            StatusMessage = "Đã tạo hồ sơ nhân viên.";
        });
    }

    [RelayCommand]
    private async Task CreateAccountAsync()
    {
        await RunOperationAsync(async () =>
        {
            var credentials = await employeeManagementService.CreateAccountAsync(
                AccountEmployeeName,
                AccountEmployeePhone,
                AccountEmployeeEmail);
            AccountEmployeeName = string.Empty;
            AccountEmployeePhone = string.Empty;
            AccountEmployeeEmail = string.Empty;
            await RefreshAccountsAsync();
            StatusMessage = "Đã tạo tài khoản. Hãy gửi mật khẩu tạm cho nhân viên; hệ thống sẽ yêu cầu đổi mật khẩu ở lần đăng nhập đầu.";
        });
    }

    [RelayCommand]
    private async Task CreateEmployeeProfileAsync()
    {
        if (SelectedAccount is not { } account)
        {
            ErrorMessage = "Hãy chọn tài khoản cần tạo hồ sơ.";
            return;
        }

        if (!account.CanCreateEmployeeProfile)
        {
            ErrorMessage = account.HasEmployeeProfile
                ? "Tài khoản này đã có hồ sơ nhân viên."
                : "Chỉ tài khoản nhân viên chưa có hồ sơ mới được tạo hồ sơ.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await employeeManagementService.CreateEmployeeProfileAsync(account.AccountId);
            await RefreshAccountsAsync();
            await RefreshEmployeesAsync();
            StatusMessage = "Đã tạo hồ sơ nhân viên.";
        });
    }

    [RelayCommand]
    private async Task UpdateAccountEmployeeNameAsync()
    {
        if (SelectedAccount is not { } account)
        {
            ErrorMessage = "Hãy chọn tài khoản cần chỉnh sửa.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await employeeManagementService.UpdateAccountEmployeeNameAsync(
                account.AccountId,
                EditAccountEmployeeName);
            await RefreshAccountsAsync();
            await RefreshEmployeesAsync();
            StatusMessage = "Đã cập nhật họ tên và tên đăng nhập nhân viên.";
        });
    }

    [RelayCommand]
    private async Task DeleteAccountAsync()
    {
        if (SelectedAccount is not { } account)
        {
            ErrorMessage = "Hãy chọn tài khoản cần xóa.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await employeeManagementService.DeleteAccountAsync(account.AccountId);
            SelectedAccount = null;
            await RefreshAccountsAsync();
            await RefreshEmployeesAsync();
            StatusMessage = "Đã thu hồi quyền đăng nhập; hồ sơ và lịch sử giao dịch được giữ lại.";
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
                employee.FullName,
                EmployeePhone,
                EmployeeEmail);
            await RefreshEmployeesAsync();
            StatusMessage = "Đã cập nhật số điện thoại và email nhân viên.";
        });
    }

    [RelayCommand]
    private async Task DeleteEmployeeProfileAsync()
    {
        if (SelectedEmployee is not { } employee)
        {
            ErrorMessage = "Hãy chọn hồ sơ nhân viên cần xóa.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await employeeManagementService.DeleteEmployeeProfileAsync(employee.EmployeeId);
            SelectedEmployee = null;
            await RefreshEmployeesAsync();
            await RefreshAccountsAsync();
            StatusMessage = "Đã xóa hồ sơ; tài khoản đăng nhập được giữ lại và có thể tạo lại hồ sơ. Lịch sử order vẫn được giữ.";
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
            StatusMessage = employee.IsEmployeeActive
                ? "Đã tạm đình chỉ nhân viên."
                : "Đã bỏ tạm đình chỉ nhân viên.";
        });
    }

    [RelayCommand]
    private async Task CreateTableAsync()
    {
        if (string.IsNullOrWhiteSpace(TableNumberText)
            || !int.TryParse(TableSeatCountText, out var seatCount))
        {
            ErrorMessage = "Hãy nhập số hiệu bàn và số chỗ ngồi hợp lệ.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await restaurantCatalogService.CreateTableAsync(
                TableNumberText,
                seatCount,
                TableAreaText);
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

        if (string.IsNullOrWhiteSpace(TableNumberText)
            || !int.TryParse(TableSeatCountText, out var seatCount))
        {
            ErrorMessage = "Hãy nhập số hiệu bàn và số chỗ ngồi hợp lệ.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await restaurantCatalogService.UpdateTableAsync(
                table.Id,
                TableNumberText,
                seatCount,
                TableAreaText);
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
            var willBeActive = !table.IsActive;
            await restaurantCatalogService.SetTableActiveAsync(table.Id, willBeActive);
            await RefreshCatalogAsync();
            StatusMessage = willBeActive
                ? "Đã mở lại bàn."
                : "Đã ngừng phục vụ bàn.";
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

    private async Task LoadOpenOrderAsync()
    {
        if (SelectedOrderTable is not { } table)
        {
            ErrorMessage = "Hãy chọn bàn cần xem order.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await RefreshOpenOrderAsync(table.Id);
            await RefreshCatalogAsync();
            StatusMessage = CurrentOpenOrder is null
                ? $"Bàn {table.Area} - {table.TableNumber} hiện chưa có order đang mở."
                : $"Đã tải order của bàn {table.Area} - {table.TableNumber}.";
        });
    }

    [RelayCommand]
    private void NewOrderTab() => AddNewOrderTab();

    [RelayCommand]
    private void SelectOrderTab(OrderWorkspaceTab? tab)
    {
        if (tab is not null && OrderTabs.Contains(tab))
        {
            SelectedOrderTab = tab;
        }
    }

    [RelayCommand]
    private void CloseOrderTab(OrderWorkspaceTab? tab)
    {
        if (tab is null)
        {
            return;
        }

        var index = OrderTabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }

        var wasSelected = SelectedOrderTab == tab;
        OrderTabs.RemoveAt(index);
        if (!wasSelected)
        {
            return;
        }

        if (OrderTabs.Count == 0)
        {
            AddNewOrderTab();
        }
        else
        {
            SelectedOrderTab = OrderTabs[Math.Min(index, OrderTabs.Count - 1)];
        }
    }

    [RelayCommand]
    private async Task StartOrderAsync()
    {
        if (SelectedOrderTable is not { } table)
        {
            ErrorMessage = "Hãy chọn bàn cần bắt đầu order.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            var service = GetRestaurantOrderService();
            SelectOrCreateOrderTab(table.Id);
            await service.StartOrderAsync(table.Id);
            await RefreshCatalogAsync();
            await RefreshOpenOrderAsync(table.Id);
            StatusMessage = $"Đã bắt đầu order cho bàn {table.Area} - {table.TableNumber}.";
        });
    }

    [RelayCommand]
    private async Task AddOrderItemAsync()
    {
        if (CurrentOpenOrder is not { } order)
        {
            ErrorMessage = "Hãy tải hoặc bắt đầu order trước.";
            return;
        }

        if (SelectedOrderProduct is not { } product)
        {
            ErrorMessage = "Hãy chọn món cần thêm.";
            return;
        }

        if (!int.TryParse(OrderQuantityText, out var quantity))
        {
            ErrorMessage = "Số lượng phải là số nguyên từ 1 đến 99.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await GetRestaurantOrderService().AddItemAsync(
                order.OrderId,
                product.Id,
                quantity,
                OrderItemNotes);
            OrderQuantityText = "1";
            OrderItemNotes = string.Empty;
            await RefreshOpenOrderAsync(order.TableId);
            await RefreshCatalogAsync();
            StatusMessage = "Đã thêm món vào order.";
        });
    }

    [RelayCommand]
    private async Task UpdateOrderItemAsync()
    {
        if (CurrentOpenOrder is not { } order)
        {
            ErrorMessage = "Hãy tải hoặc bắt đầu order trước.";
            return;
        }

        if (SelectedOrderDetail is not { } detail)
        {
            ErrorMessage = "Hãy chọn món cần cập nhật.";
            return;
        }

        if (!int.TryParse(EditOrderQuantityText, out var quantity))
        {
            ErrorMessage = "Số lượng phải là số nguyên từ 1 đến 99.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await GetRestaurantOrderService().UpdateItemAsync(
                order.OrderId,
                detail.Id,
                quantity,
                EditOrderItemNotes);
            SelectedOrderDetail = null;
            await RefreshOpenOrderAsync(order.TableId);
            await RefreshCatalogAsync();
            StatusMessage = "Đã cập nhật món trong order.";
        });
    }

    [RelayCommand]
    private async Task RemoveOrderItemAsync()
    {
        if (CurrentOpenOrder is not { } order)
        {
            ErrorMessage = "Hãy tải hoặc bắt đầu order trước.";
            return;
        }

        if (SelectedOrderDetail is not { } detail)
        {
            ErrorMessage = "Hãy chọn món cần bỏ khỏi order.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await GetRestaurantOrderService().RemoveItemAsync(order.OrderId, detail.Id);
            SelectedOrderDetail = null;
            await RefreshOpenOrderAsync(order.TableId);
            await RefreshCatalogAsync();
            StatusMessage = "Đã bỏ món khỏi order.";
        });
    }

    [RelayCommand]
    private async Task CancelOpenOrderAsync()
    {
        if (CurrentOpenOrder is not { } order)
        {
            ErrorMessage = "Bàn hiện không có order để hủy.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await GetRestaurantOrderService().CancelOrderAsync(
                order.OrderId,
                OrderCancellationReason);
            CloseOrderTab(SelectedOrderTab);
            OrderCancellationReason = string.Empty;
            await RefreshCatalogAsync();
            StatusMessage = "Đã hủy order; bàn hiện đã trống.";
        });
    }

    [RelayCommand]
    private async Task CompleteCashPaymentAsync() =>
        await CompletePaymentAsync(PaymentMethod.Cash);

    [RelayCommand]
    private async Task CompleteBankTransferPaymentAsync() =>
        await CompletePaymentAsync(PaymentMethod.BankTransfer);

    private async Task CompletePaymentAsync(PaymentMethod method)
    {
        if (CurrentOpenOrder is not { } order)
        {
            ErrorMessage = "Bàn hiện không có order cần thanh toán.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await GetRestaurantOrderService().CompletePaymentAsync(order.OrderId, method);
            CloseOrderTab(SelectedOrderTab);
            OrderCancellationReason = string.Empty;
            await RefreshCatalogAsync();
            StatusMessage = "Thanh toán thành công; bàn hiện đã trống.";
        });
    }

    [RelayCommand]
    private async Task CreateCategoryAsync()
    {
        await RunOperationAsync(async () =>
        {
            await restaurantCatalogService.CreateCategoryAsync(CategoryName);
            CategoryName = string.Empty;
            await RefreshCatalogAsync();
            StatusMessage = "Đã thêm danh mục.";
        });
    }

    [RelayCommand]
    private async Task CreatePromotionAsync()
    {
        if (!TryGetPromotionValues(out var discountPercentage, out var startsAt, out var endsAt,
                out var productId, out var categoryId))
        {
            return;
        }

        await RunOperationAsync(async () =>
        {
            await restaurantCatalogService.CreatePromotionAsync(
                PromotionName,
                discountPercentage,
                startsAt,
                endsAt,
                productId,
                categoryId);
            await RefreshPromotionsAsync();
            StatusMessage = "Đã tạo khuyến mãi.";
        });
    }

    [RelayCommand]
    private async Task UpdatePromotionAsync()
    {
        if (SelectedPromotion is not { } promotion)
        {
            ErrorMessage = "Hãy chọn khuyến mãi cần cập nhật.";
            return;
        }

        if (!TryGetPromotionValues(out var discountPercentage, out var startsAt, out var endsAt,
                out var productId, out var categoryId))
        {
            return;
        }

        await RunOperationAsync(async () =>
        {
            await restaurantCatalogService.UpdatePromotionAsync(
                promotion.Id,
                PromotionName,
                discountPercentage,
                startsAt,
                endsAt,
                productId,
                categoryId);
            await RefreshPromotionsAsync(promotion.Id);
            StatusMessage = "Đã cập nhật khuyến mãi.";
        });
    }

    [RelayCommand]
    private async Task TogglePromotionStatusAsync()
    {
        if (SelectedPromotion is not { } promotion)
        {
            ErrorMessage = "Hãy chọn khuyến mãi cần cập nhật.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await restaurantCatalogService.SetPromotionActiveAsync(promotion.Id, !promotion.IsActive);
            await RefreshPromotionsAsync(promotion.Id);
            StatusMessage = "Đã cập nhật trạng thái khuyến mãi.";
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
                CategoryName);
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
                ProductTags,
                price,
                category.Id,
                null);
            ProductName = string.Empty;
            ClearProductTagSelection();
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
                ProductTags,
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

    private ObservableCollection<ProductTagOption> CreateProductTagOptions()
    {
        var options = new ObservableCollection<ProductTagOption>(
        [
            new("Đặc sản"),
            new("Bán chạy"),
            new("Món mới"),
            new("Siêu cay"),
            new("Món chay")
        ]);
        foreach (var option in options)
        {
            SubscribeToProductTagOption(option);
        }

        return options;
    }

    private void SetProductTagSelection(string? tags)
    {
        var selectedTags = tags?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        foreach (var tag in selectedTags)
        {
            if (!ProductTagOptions.Any(option => string.Equals(option.Name, tag, StringComparison.OrdinalIgnoreCase)))
            {
                AddProductTagOption(tag);
            }
        }

        foreach (var option in ProductTagOptions)
        {
            option.IsSelected = selectedTags.Contains(option.Name);
        }
    }

    private void ClearProductTagSelection()
    {
        foreach (var option in ProductTagOptions)
        {
            option.IsSelected = false;
        }

        ProductTags = string.Empty;
    }

    private void AddProductTagOption(string tag)
    {
        var option = new ProductTagOption(tag);
        ProductTagOptions.Add(option);
        SubscribeToProductTagOption(option);
    }

    private void SubscribeToProductTagOption(ProductTagOption option)
    {
        option.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ProductTagOption.IsSelected))
            {
                ProductTags = string.Join(
                    ", ",
                    ProductTagOptions.Where(candidate => candidate.IsSelected).Select(candidate => candidate.Name));
            }
        };
    }

    private async Task RefreshEmployeesAsync()
    {
        var selectedEmployeeId = SelectedEmployee?.EmployeeId;
        var employees = await employeeManagementService.GetEmployeesAsync();
        Employees.Clear();
        foreach (var employee in employees)
        {
            Employees.Add(employee);
        }

        ApplyEmployeeFilters();
        SelectedEmployee = FilteredEmployees.FirstOrDefault(employee => employee.EmployeeId == selectedEmployeeId);
    }

    private async Task RefreshAccountsAsync()
    {
        var selectedAccountId = SelectedAccount?.AccountId;
        var accounts = await employeeManagementService.GetAccountsAsync();
        Accounts.Clear();
        foreach (var account in accounts)
        {
            Accounts.Add(account);
        }

        ApplyAccountFilters();
        SelectedAccount = FilteredAccounts.FirstOrDefault(account => account.AccountId == selectedAccountId);
    }

    private void ApplyAccountFilters()
    {
        var searchText = AccountSearchText.Trim();
        var filteredAccounts = Accounts.Where(account =>
            (string.IsNullOrEmpty(searchText)
             || ContainsSearchText(account.FullName, searchText)
             || ContainsSearchText(account.Username, searchText)
             || ContainsSearchText(account.Phone, searchText)
             || ContainsSearchText(account.Email, searchText))
            && (SelectedAccountRoleFilter == "Tất cả vai trò"
                || (SelectedAccountRoleFilter == "Quản trị viên" && account.Role == UserRole.Admin)
                || (SelectedAccountRoleFilter == "Nhân viên" && account.Role == UserRole.Staff))
            && (SelectedAccountProfileFilter == "Tất cả hồ sơ"
                || (SelectedAccountProfileFilter == "Đã tạo hồ sơ" && account.HasEmployeeProfile)
                || (SelectedAccountProfileFilter == "Chưa tạo hồ sơ"
                    && !account.HasEmployeeProfile && !account.EmployeeProfileDeleted)
                || (SelectedAccountProfileFilter == "Hồ sơ đã bị xóa" && account.EmployeeProfileDeleted)));

        FilteredAccounts.Clear();
        foreach (var account in filteredAccounts)
        {
            FilteredAccounts.Add(account);
        }

        if (SelectedAccount is not null && !FilteredAccounts.Contains(SelectedAccount))
        {
            SelectedAccount = null;
        }
    }

    private void ApplyEmployeeFilters()
    {
        var searchText = EmployeeSearchText.Trim();
        var filteredEmployees = Employees.Where(employee =>
            (string.IsNullOrEmpty(searchText)
             || ContainsSearchText(employee.FullName, searchText)
             || ContainsSearchText(employee.Phone, searchText)
             || ContainsSearchText(employee.Email, searchText)
             || ContainsSearchText(employee.Username, searchText))
            && (SelectedEmployeeStatusFilter == "Tất cả trạng thái"
                || (SelectedEmployeeStatusFilter == "Đang làm việc" && employee.IsEmployeeActive)
                || (SelectedEmployeeStatusFilter == "Tạm đình chỉ" && !employee.IsEmployeeActive)));

        FilteredEmployees.Clear();
        foreach (var employee in filteredEmployees)
        {
            FilteredEmployees.Add(employee);
        }

        if (SelectedEmployee is not null && !FilteredEmployees.Contains(SelectedEmployee))
        {
            SelectedEmployee = null;
        }
    }

    private static bool ContainsSearchText(string? value, string searchText) =>
        value?.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) == true;

    private void SynchronizeProductCategoryFilters(string selectedFilter)
    {
        ProductCategoryFilters.Clear();
        ProductCategoryFilters.Add("Tất cả danh mục");
        foreach (var category in Categories)
        {
            ProductCategoryFilters.Add(category.Name);
        }

        SelectedProductCategoryFilter = ProductCategoryFilters.Contains(selectedFilter)
            ? selectedFilter
            : "Tất cả danh mục";
    }

    private async Task RefreshCatalogAsync()
    {
        var selectedTableId = SelectedTable?.Id;
        var selectedOrderTableId = SelectedOrderTable?.Id;
        var selectedCategoryId = SelectedCategory?.Id;
        var selectedProductId = SelectedProduct?.Id;
        var selectedProductCategoryId = SelectedProductCategory?.Id;
        var selectedProductCategoryFilter = SelectedProductCategoryFilter;
        var tables = await restaurantCatalogService.GetTablesAsync();
        var categories = await restaurantCatalogService.GetCategoriesAsync();

        Tables.Clear();
        foreach (var table in tables)
        {
            Tables.Add(table);
        }

        var availableAreas = RestaurantTableAreas.Values
            .Concat(tables.Select(table => table.Area))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (!availableAreas.Contains(TableAreaText, StringComparer.OrdinalIgnoreCase))
        {
            TableAreaText = RestaurantTableAreas.Default;
        }

        var availableAreaFilters = new[] { "Tất cả khu vực" }.Concat(availableAreas).ToArray();
        if (!availableAreaFilters.Contains(SelectedTableAreaFilter, StringComparer.OrdinalIgnoreCase))
        {
            SelectedTableAreaFilter = "Tất cả khu vực";
        }

        SynchronizeAreas(TableAreas, availableAreas);
        SynchronizeAreas(
            TableAreaFilters,
            availableAreaFilters);
        SynchronizeAreas(
            OrderTableAreaFilters,
            availableAreaFilters);
        if (!OrderTableAreaFilters.Contains(
                SelectedOrderTableAreaFilter,
                StringComparer.OrdinalIgnoreCase))
        {
            SelectedOrderTableAreaFilter = "Tất cả khu vực";
        }

        Categories.Clear();
        foreach (var category in categories)
        {
            Categories.Add(category);
        }

        SelectedTable = Tables.FirstOrDefault(table => table.Id == selectedTableId);
        SelectedOrderTable = Tables.FirstOrDefault(table => table.Id == selectedOrderTableId)
            ?? Tables.FirstOrDefault();
        ApplyTableFilters();
        ApplyOrderTableFilters();
        SelectedCategory = Categories.FirstOrDefault(category => category.Id == selectedCategoryId);
        SelectedProductCategory = Categories.FirstOrDefault(category => category.Id == selectedProductCategoryId);
        SynchronizeProductCategoryFilters(selectedProductCategoryFilter);
        await RefreshProductsAsync(selectedProductId);
        await RefreshPromotionsAsync(SelectedPromotion?.Id);
    }

    private async Task RefreshPromotionsAsync(int? selectedPromotionId = null)
    {
        Promotions.Clear();
        if (!IsAdminScreen)
        {
            SelectedPromotion = null;
            return;
        }

        foreach (var promotion in await restaurantCatalogService.GetPromotionsAsync())
        {
            Promotions.Add(promotion);
        }

        SelectedPromotion = Promotions.FirstOrDefault(promotion => promotion.Id == selectedPromotionId);
    }

    private bool TryGetPromotionValues(
        out decimal discountPercentage,
        out DateTimeOffset startsAt,
        out DateTimeOffset endsAt,
        out int? productId,
        out int? categoryId)
    {
        discountPercentage = 0;
        startsAt = default;
        endsAt = default;
        productId = null;
        categoryId = null;

        if (!decimal.TryParse(
                PromotionDiscountPercentageText,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out discountPercentage)
            || discountPercentage is <= 0 or > 100)
        {
            ErrorMessage = "Mức giảm phải lớn hơn 0% và không vượt quá 100%.";
            return false;
        }

        if (PromotionStartsAt is not { } startDate || PromotionEndsAt is not { } endDate)
        {
            ErrorMessage = "Hãy chọn thời gian bắt đầu và kết thúc.";
            return false;
        }

        startsAt = new DateTimeOffset(startDate);
        endsAt = new DateTimeOffset(endDate.Date.AddDays(1).AddTicks(-1));
        if (endsAt <= startsAt)
        {
            ErrorMessage = "Ngày kết thúc phải sau ngày bắt đầu.";
            return false;
        }

        if (SelectedPromotionTargetType == "Món ăn" && SelectedPromotionProduct is { } product)
        {
            productId = product.Id;
        }
        else if (SelectedPromotionTargetType == "Danh mục" && SelectedPromotionCategory is { } category)
        {
            categoryId = category.Id;
        }
        else
        {
            ErrorMessage = "Hãy chọn món ăn hoặc danh mục áp dụng.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(PromotionName))
        {
            ErrorMessage = "Hãy nhập tên khuyến mãi.";
            return false;
        }

        return true;
    }

    private void ApplyTableFilters()
    {
        var searchText = TableSearchText.Trim();
        var filteredTables = Tables.Where(table =>
            (string.IsNullOrEmpty(searchText)
             || table.TableNumber.Contains(searchText, StringComparison.CurrentCultureIgnoreCase)
             || table.Area.Contains(searchText, StringComparison.CurrentCultureIgnoreCase))
            && (SelectedTableAreaFilter == "Tất cả khu vực"
                || string.Equals(table.Area, SelectedTableAreaFilter, StringComparison.OrdinalIgnoreCase))
            && (SelectedTableServiceStatusFilter == "Tất cả trạng thái"
                || string.Equals(
                    table.ServiceStatus,
                    SelectedTableServiceStatusFilter,
                    StringComparison.Ordinal)));

        FilteredTables.Clear();
        foreach (var table in filteredTables)
        {
            FilteredTables.Add(table);
        }

        if (SelectedTable is not null && !FilteredTables.Contains(SelectedTable))
        {
            SelectedTable = null;
        }
    }

    private void ApplyOrderTableFilters()
    {
        var searchText = OrderTableSearchText.Trim();
        var filteredTables = Tables.Where(table =>
            (string.IsNullOrEmpty(searchText)
             || table.TableNumber.Contains(searchText, StringComparison.CurrentCultureIgnoreCase))
            && (SelectedOrderTableAreaFilter == "Tất cả khu vực"
                || string.Equals(
                    table.Area,
                    SelectedOrderTableAreaFilter,
                    StringComparison.OrdinalIgnoreCase))
            && (SelectedOrderTableStatusFilter == "Tất cả trạng thái"
                || (SelectedOrderTableStatusFilter == "Trống" && !table.IsOccupied)
                || (SelectedOrderTableStatusFilter == "Đang có order" && table.IsOccupied)));

        FilteredOrderTables.Clear();
        foreach (var table in filteredTables)
        {
            FilteredOrderTables.Add(table);
        }
    }

    private static void SynchronizeAreas(
        ObservableCollection<string> options,
        IReadOnlyCollection<string> availableAreas)
    {
        foreach (var area in availableAreas.Where(area =>
                     !options.Contains(area, StringComparer.OrdinalIgnoreCase)))
        {
            options.Add(area);
        }

        foreach (var area in options
                     .Where(area => !availableAreas.Contains(area, StringComparer.OrdinalIgnoreCase))
                     .ToArray())
        {
            options.Remove(area);
        }
    }

    private async Task RefreshProductsAsync(int? selectedProductId = null)
    {
        _catalogProducts = await restaurantCatalogService.GetProductsAsync();
        PromotionProducts.Clear();
        foreach (var product in _catalogProducts)
        {
            PromotionProducts.Add(product);
        }

        ApplyProductFilters(selectedProductId);
        OrderableProducts.Clear();
        foreach (var product in _catalogProducts.Where(product => product.IsActive && product.IsAvailable))
        {
            OrderableProducts.Add(product);
        }

        SelectedOrderProduct = OrderableProducts.FirstOrDefault(
            product => product.Id == SelectedOrderProduct?.Id)
            ?? OrderableProducts.FirstOrDefault();
    }

    private void ApplyProductFilters(int? selectedProductId = null)
    {
        selectedProductId ??= SelectedProduct?.Id;
        var searchText = ProductSearchText.Trim();
        var categoryFilter = SelectedProductCategoryFilter;
        var filteredProducts = _catalogProducts.Where(product =>
            (string.IsNullOrEmpty(searchText)
             || ContainsSearchText(product.Name, searchText)
             || ContainsSearchText(product.Tags, searchText)
             || ContainsSearchText(product.CategoryName, searchText))
            && (categoryFilter == "Tất cả danh mục"
                || string.Equals(product.CategoryName, categoryFilter, StringComparison.Ordinal)));

        Products.Clear();
        foreach (var product in filteredProducts)
        {
            Products.Add(product);
        }

        SelectedProduct = Products.FirstOrDefault(product => product.Id == selectedProductId);
    }

    private async Task RefreshOpenOrderAsync(int tableId)
    {
        SelectedOrderDetail = null;
        var order = await GetRestaurantOrderService().GetOpenOrderAsync(tableId);
        var tab = SelectOrCreateOrderTab(tableId);
        tab.Order = order;
        tab.Title = GetOrderTabTitle(tableId, order);
        CurrentOpenOrder = order;
    }

    private void AddNewOrderTab()
    {
        var tab = new OrderWorkspaceTab(null, "Chọn bàn");
        OrderTabs.Add(tab);
        SelectedOrderTab = tab;
    }

    private OrderWorkspaceTab SelectOrCreateOrderTab(int tableId)
    {
        var existingTab = OrderTabs.FirstOrDefault(tab => tab.TableId == tableId);
        if (existingTab is not null)
        {
            SelectedOrderTab = existingTab;
            return existingTab;
        }

        var tab = SelectedOrderTab is { Order: null } availableTab
            ? availableTab
            : new OrderWorkspaceTab();
        if (!OrderTabs.Contains(tab))
        {
            OrderTabs.Add(tab);
        }

        tab.TableId = tableId;
        tab.Title = GetOrderTabTitle(tableId, null);
        SelectedOrderTab = tab;
        return tab;
    }

    private string GetOrderTabTitle(int tableId, OpenOrderSummary? order)
    {
        var table = Tables.FirstOrDefault(candidate => candidate.Id == tableId);
        var tableName = table?.TableNumber ?? $"Bàn {tableId}";
        return order is null
            ? $"{tableName} • Chưa có order"
            : $"{tableName} • Order #{order.OrderId}";
    }

    private IRestaurantOrderService GetRestaurantOrderService() =>
        restaurantOrderService
        ?? throw new InvalidOperationException("Chức năng order chưa được khởi tạo.");

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
                or MySqlException)
        {
            if (exception is DbUpdateException or MySqlException)
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
    }

    partial void OnSelectedOrderDetailChanged(OrderDetailSummary? value)
    {
        EditOrderQuantityText = value?.Quantity.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        EditOrderItemNotes = value?.Notes ?? string.Empty;
    }

    partial void OnSelectedOrderTabChanged(OrderWorkspaceTab? value)
    {
        foreach (var tab in OrderTabs)
        {
            tab.IsSelected = tab == value;
        }

        CurrentOpenOrder = value?.Order;
        SelectedOrderTable = value?.TableId is { } tableId
            ? Tables.FirstOrDefault(table => table.Id == tableId)
            : null;
        SelectedOrderDetail = null;
    }

    partial void OnSelectedOrderTableChanged(RestaurantTableSummary? value)
    {
        if (value is not { IsOccupied: true } || !IsStaffOrderScreen)
        {
            return;
        }

        if (SelectedOrderTab?.TableId == value.Id)
        {
            return;
        }

        var existingTab = OrderTabs.FirstOrDefault(tab => tab.TableId == value.Id);
        if (existingTab is not null)
        {
            SelectedOrderTab = existingTab;
        }
        else if (!IsBusy)
        {
            _ = LoadOpenOrderAsync();
        }
    }

    partial void OnSelectedTableChanged(RestaurantTableSummary? value)
    {
        if (value is null)
        {
            return;
        }

        TableNumberText = value.TableNumber;
        TableAreaText = value.Area;
        TableSeatCountText = value.SeatCount.ToString(CultureInfo.CurrentCulture);
    }

    partial void OnSelectedCategoryChanged(CategorySummary? value)
    {
        if (value is null)
        {
            return;
        }

        CategoryName = value.Name;
    }

    partial void OnSelectedProductChanged(ProductSummary? value)
    {
        if (value is null)
        {
            return;
        }

        ProductName = value.Name;
        SetProductTagSelection(value.Tags);
        ProductPriceText = value.Price.ToString("0.##", CultureInfo.CurrentCulture);
        ProductImagePath = value.ImagePath ?? string.Empty;
        SelectedProductCategory = Categories.FirstOrDefault(category => category.Id == value.CategoryId);
    }

    partial void OnSelectedPromotionChanged(PromotionSummary? value)
    {
        if (value is null)
        {
            return;
        }

        PromotionName = value.Name;
        PromotionDiscountPercentageText = value.DiscountPercentage.ToString(
            "0.##", CultureInfo.CurrentCulture);
        PromotionStartsAt = value.StartsAt.ToLocalTime().DateTime;
        PromotionEndsAt = value.EndsAt.ToLocalTime().DateTime;
        SelectedPromotionTargetType = value.ProductId.HasValue ? "Món ăn" : "Danh mục";
        SelectedPromotionProduct = PromotionProducts.FirstOrDefault(product => product.Id == value.ProductId);
        SelectedPromotionCategory = Categories.FirstOrDefault(category => category.Id == value.CategoryId);
    }

    partial void OnIsSetupRequiredChanged(bool value)
    {
        OnPropertyChanged(nameof(IsSetupScreen));
        OnPropertyChanged(nameof(IsLoginScreen));
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
}
