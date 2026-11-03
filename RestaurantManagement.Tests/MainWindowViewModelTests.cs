using Microsoft.Extensions.Logging.Abstractions;
using RestaurantManagement.Core.Enums;
using RestaurantManagement.Core.Models;
using RestaurantManagement.Core.Services;
using RestaurantManagement.WPF.ViewModels;

namespace RestaurantManagement.Tests;

public class MainWindowViewModelTests
{
    [Fact]
    public void AccountSummaryShowsTemporaryPasswordUntilItHasBeenChanged()
    {
        var account = new AccountSummary(
            1,
            null,
            "Nguyễn Văn An",
            "0123456789",
            null,
            true,
            "an.nguyen",
            UserRole.Staff,
            RequiresPasswordChange: true)
        {
            TemporaryPassword = "Temporary123!"
        };

        Assert.Equal("Temporary123!", account.PasswordDisplay);
        Assert.Equal("Tạo hồ sơ", account.ProfileActionLabel);
        Assert.Equal("Chưa tạo hồ sơ", account.EmployeeProfileStatusLabel);
        Assert.Equal(
            "Đã đổi mật khẩu",
            (account with { RequiresPasswordChange = false }).PasswordDisplay);
        Assert.Equal(
            "Đã tạo hồ sơ",
            (account with { EmployeeId = 12 }).ProfileActionLabel);
        Assert.Equal(
            "Đã tạo hồ sơ",
            (account with { EmployeeId = 12 }).EmployeeProfileStatusLabel);
        Assert.Equal(
            "Hồ sơ đã bị xóa",
            (account with { EmployeeProfileDeleted = true }).EmployeeProfileStatusLabel);

        var employee = new EmployeeAccountSummary(
            3,
            "Nguyễn Văn An",
            "0123456789",
            null,
            true,
            7,
            "an.nguyen",
            UserRole.Staff,
            RequiresPasswordChange: false);
        Assert.Equal("Tạm đình chỉ", employee.EmployeeStatusActionLabel);
        Assert.Equal(
            "Bỏ tạm đình chỉ",
            (employee with { IsEmployeeActive = false }).EmployeeStatusActionLabel);
    }

    [Fact]
    public void TableStatusReflectsOccupancyAndAvailability()
    {
        var table = new RestaurantTableSummary(1, "Bàn 1", "Tầng trệt", 4, true, false);

        Assert.Equal("Trống", table.OccupancyStatus);
        Assert.Equal("Có khách", (table with { IsOccupied = true }).OccupancyStatus);
        Assert.Equal("Đang phục vụ", table.ServiceStatus);
        Assert.Equal("Ngừng phục vụ", (table with { IsActive = false }).ServiceStatus);
        Assert.Equal("Trống", (table with { IsActive = false }).OccupancyStatus);
    }

    [Fact]
    public void OpenOrderSummaryShowsDiscountedTotalWithoutChangingSubtotal()
    {
        var order = new OpenOrderSummary(
            7,
            3,
            [new OrderDetailSummary("Món thử", 50_000m, 2, null)],
            "Ưu đãi cuối tuần",
            10_000m);

        Assert.Equal(100_000m, order.Subtotal);
        Assert.Equal(10_000m, order.DiscountAmount);
        Assert.Equal(90_000m, order.Total);
    }

    [Fact]
    public void AccountFiltersMatchSearchRoleAndEmployeeProfileStatus()
    {
        var viewModel = CreateViewModel();
        viewModel.Accounts.Add(new AccountSummary(
            1, 10, "Nguyễn An", "0900000001", "an@example.com", true,
            "an.staff", UserRole.Staff, false));
        viewModel.Accounts.Add(new AccountSummary(
            2, null, "Trần Bình", null, null, false,
            "binh.staff", UserRole.Staff, true));
        viewModel.Accounts.Add(new AccountSummary(
            3, 11, "Lê Chi", null, null, true,
            "admin.chi", UserRole.Admin, false));
        viewModel.Accounts.Add(new AccountSummary(
            4, null, "Phạm Dung", null, null, false,
            "dung.staff", UserRole.Staff, true) { EmployeeProfileDeleted = true });

        viewModel.SelectedAccountRoleFilter = "Nhân viên";
        viewModel.SelectedAccountProfileFilter = "Chưa tạo hồ sơ";
        Assert.Equal(new[] { 2 }, viewModel.FilteredAccounts.Select(account => account.AccountId));

        viewModel.SelectedAccountProfileFilter = "Hồ sơ đã bị xóa";
        Assert.Equal(new[] { 4 }, viewModel.FilteredAccounts.Select(account => account.AccountId));

        viewModel.SelectedAccountProfileFilter = "Tất cả hồ sơ";
        viewModel.AccountSearchText = "0900000001";
        Assert.Equal(new[] { 1 }, viewModel.FilteredAccounts.Select(account => account.AccountId));
    }

    [Fact]
    public void EmployeeFiltersMatchSearchAndWorkStatus()
    {
        var viewModel = CreateViewModel();
        viewModel.Employees.Add(new EmployeeAccountSummary(
            1, "Nguyễn An", "0900000001", "an@example.com", true,
            10, "an.staff", UserRole.Staff, false));
        viewModel.Employees.Add(new EmployeeAccountSummary(
            2, "Trần Bình", "0900000002", null, false,
            11, "binh.staff", UserRole.Staff, false));

        viewModel.SelectedEmployeeStatusFilter = "Tạm đình chỉ";
        Assert.Equal(new[] { 2 }, viewModel.FilteredEmployees.Select(employee => employee.EmployeeId));

        viewModel.EmployeeSearchText = "binh.staff";
        Assert.Equal(new[] { 2 }, viewModel.FilteredEmployees.Select(employee => employee.EmployeeId));

        viewModel.EmployeeSearchText = "an@example.com";
        Assert.Empty(viewModel.FilteredEmployees);
    }

    [Fact]
    public void TableAreasProvideCommonPrivateVipAndOutdoorOptions()
    {
        var viewModel = new MainWindowViewModel(
            new InitialAdminSetupServiceStub(),
            new AuthenticationServiceStub(),
            new CurrentUserSessionStub(),
            new EmployeeManagementServiceStub(),
            new RestaurantCatalogServiceStub(),
            NullLogger<MainWindowViewModel>.Instance);

        Assert.Equal(
            new[] { "Chung", "Riêng tư", "VIP", "Ngoài trời" },
            viewModel.TableAreas);
    }

    private static MainWindowViewModel CreateViewModel() =>
        new(
            new InitialAdminSetupServiceStub(),
            new AuthenticationServiceStub(),
            new CurrentUserSessionStub(),
            new EmployeeManagementServiceStub(),
            new RestaurantCatalogServiceStub(),
            NullLogger<MainWindowViewModel>.Instance);

    [Fact]
    public void TableFiltersMatchSearchAreaAndServiceStatus()
    {
        var viewModel = new MainWindowViewModel(
            new InitialAdminSetupServiceStub(),
            new AuthenticationServiceStub(),
            new CurrentUserSessionStub(),
            new EmployeeManagementServiceStub(),
            new RestaurantCatalogServiceStub(),
            NullLogger<MainWindowViewModel>.Instance);
        viewModel.Tables.Add(new RestaurantTableSummary(1, "Bàn 1", "Chung", 4, true, false));
        viewModel.Tables.Add(new RestaurantTableSummary(2, "A2", "VIP", 2, false, false));
        viewModel.Tables.Add(new RestaurantTableSummary(3, "Bàn 3", "VIP", 6, true, true));
        viewModel.SelectedTableAreaFilter = "Chung";
        viewModel.SelectedTableAreaFilter = "Tất cả khu vực";

        Assert.Equal(3, viewModel.FilteredTables.Count);

        viewModel.SelectedTableAreaFilter = "VIP";
        Assert.Equal(new[] { 2, 3 }, viewModel.FilteredTables.Select(table => table.Id));

        viewModel.SelectedTableServiceStatusFilter = "Đang phục vụ";
        Assert.Equal(new[] { 3 }, viewModel.FilteredTables.Select(table => table.Id));

        viewModel.TableSearchText = "bàn 3";
        Assert.Equal(new[] { 3 }, viewModel.FilteredTables.Select(table => table.Id));

        viewModel.TableSearchText = "bàn 1";
        Assert.Empty(viewModel.FilteredTables);
    }

    [Fact]
    public async Task UpdatingTablePreservesAvailableFilters()
    {
        var catalogService = new RestaurantCatalogServiceStub
        {
            TablesToReturn = [new RestaurantTableSummary(2, "A2", "VIP", 2, false, false)]
        };
        var viewModel = new MainWindowViewModel(
            new InitialAdminSetupServiceStub(),
            new AuthenticationServiceStub(),
            new CurrentUserSessionStub(),
            new EmployeeManagementServiceStub(),
            catalogService,
            NullLogger<MainWindowViewModel>.Instance);
        var table = new RestaurantTableSummary(2, "A2", "VIP", 2, false, false);
        viewModel.Tables.Add(table);
        viewModel.SelectedTable = table;
        viewModel.SelectedTableAreaFilter = "VIP";
        viewModel.SelectedTableServiceStatusFilter = "Ngừng phục vụ";
        viewModel.TableSearchText = "A2";

        await viewModel.UpdateTableCommand.ExecuteAsync(null);

        Assert.Equal("VIP", viewModel.SelectedTableAreaFilter);
        Assert.Equal("Ngừng phục vụ", viewModel.SelectedTableServiceStatusFilter);
        Assert.Equal("A2", viewModel.TableSearchText);
        Assert.Equal(2, Assert.Single(viewModel.FilteredTables).Id);
        Assert.NotNull(viewModel.SelectedTable);
    }

    [Fact]
    public void TableStatusSwitchIsDisabledOnlyForOccupiedActiveTables()
    {
        Assert.True(new RestaurantTableSummary(1, "Bàn 1", "Chung", 4, true, false).CanToggleServiceStatus);
        Assert.False(new RestaurantTableSummary(2, "Bàn 2", "Chung", 4, true, true).CanToggleServiceStatus);
        Assert.True(new RestaurantTableSummary(3, "Bàn 3", "Chung", 4, false, false).CanToggleServiceStatus);
    }

    [Fact]
    public void StaffWithoutEmployeeProfileCanEnterReadOnlyWorkspace()
    {
        var session = new CurrentUserSessionStub();
        session.SignIn(new AuthenticatedUser(
            7,
            "an.nguyen",
            UserRole.Staff,
            null,
            "Nguyễn Văn An",
            RequiresPasswordChange: false));
        var viewModel = new MainWindowViewModel(
            new InitialAdminSetupServiceStub(),
            new AuthenticationServiceStub(),
            session,
            new EmployeeManagementServiceStub(),
            new RestaurantCatalogServiceStub(),
            NullLogger<MainWindowViewModel>.Instance);

        Assert.True(viewModel.IsAuthenticatedWorkspace);
        Assert.True(viewModel.IsStaffScreen);
        Assert.True(viewModel.IsReadOnlyStaff);
        Assert.False(viewModel.IsStaffOrderScreen);
        Assert.False(viewModel.IsPasswordChangeRequired);
        Assert.Contains(
            "Bạn chỉ có thể xem nghiệp vụ, không thể thao tác ảnh hưởng đến dữ liệu.",
            viewModel.ReadOnlyStaffWarning);
        Assert.Contains("Hãy đổi mật khẩu được cấp", viewModel.ReadOnlyStaffWarning);
    }

    [Fact]
    public async Task UpdateEmployeeCommandChangesContactDetailsWithoutRenamingEmployee()
    {
        var employeeService = new EmployeeManagementServiceStub();
        var viewModel = new MainWindowViewModel(
            new InitialAdminSetupServiceStub(),
            new AuthenticationServiceStub(),
            new CurrentUserSessionStub(),
            employeeService,
            new RestaurantCatalogServiceStub(),
            NullLogger<MainWindowViewModel>.Instance)
        {
            SelectedEmployee = new EmployeeAccountSummary(
                12,
                "Nguyễn Văn An",
                "0123456789",
                "old@example.test",
                true,
                7,
                "an.nguyen",
                UserRole.Staff,
                RequiresPasswordChange: false),
            EmployeePhone = "0987654321",
            EmployeeEmail = "new@example.test"
        };

        await viewModel.UpdateEmployeeCommand.ExecuteAsync(null);

        Assert.Equal(
            (12, "Nguyễn Văn An", "0987654321", "new@example.test"),
            employeeService.LastEmployeeUpdate);
        Assert.Equal("Đã cập nhật số điện thoại và email nhân viên.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ToggleEmployeeStatusCommandSuspendsSelectedEmployee()
    {
        var employeeService = new EmployeeManagementServiceStub();
        var viewModel = new MainWindowViewModel(
            new InitialAdminSetupServiceStub(),
            new AuthenticationServiceStub(),
            new CurrentUserSessionStub(),
            employeeService,
            new RestaurantCatalogServiceStub(),
            NullLogger<MainWindowViewModel>.Instance)
        {
            SelectedEmployee = new EmployeeAccountSummary(
                12,
                "Nguyễn Văn An",
                "0123456789",
                null,
                true,
                7,
                "an.nguyen",
                UserRole.Staff,
                RequiresPasswordChange: false)
        };

        await viewModel.ToggleEmployeeStatusCommand.ExecuteAsync(null);

        Assert.Equal((12, false), employeeService.LastEmployeeStatusChange);
        Assert.Equal("Đã tạm đình chỉ nhân viên.", viewModel.StatusMessage);
    }

    [Theory]
    [InlineData("Tài khoản không tồn tại.")]
    [InlineData("Nhân viên đang bị tạm đình chỉ.")]
    public async Task SignInFailureIsShownOnLoginScreen(string message)
    {
        var authenticationService = new AuthenticationServiceStub
        {
            SignInFailure = new InvalidOperationException(message)
        };
        var viewModel = new MainWindowViewModel(
            new InitialAdminSetupServiceStub(),
            authenticationService,
            new CurrentUserSessionStub(),
            new EmployeeManagementServiceStub(),
            new RestaurantCatalogServiceStub(),
            NullLogger<MainWindowViewModel>.Instance)
        {
            IsSetupRequired = false,
            LoginUsername = "staff",
            LoginPassword = "Temporary123!"
        };

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsLoginScreen);
        Assert.False(viewModel.IsAuthenticated);
        Assert.Equal(message, viewModel.ErrorMessage);
        Assert.Empty(viewModel.LoginPassword);
    }

    [Fact]
    public async Task ChangingPasswordSignsOutAndRequestsLoginAgain()
    {
        var session = new CurrentUserSessionStub();
        session.SignIn(new AuthenticatedUser(
            1,
            "admin",
            UserRole.Admin,
            null,
            "Admin",
            RequiresPasswordChange: false));
        var viewModel = new MainWindowViewModel(
            new InitialAdminSetupServiceStub(),
            new AuthenticationServiceStub(),
            session,
            new EmployeeManagementServiceStub(),
            new RestaurantCatalogServiceStub(),
            NullLogger<MainWindowViewModel>.Instance)
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "NewPassword123!"
        };

        await viewModel.ChangePasswordCommand.ExecuteAsync(null);

        Assert.Null(session.User);
        Assert.False(viewModel.IsAuthenticated);
        Assert.Equal("Đổi mật khẩu thành công. Hãy đăng nhập lại.", viewModel.StatusMessage);
        Assert.Empty(viewModel.CurrentPassword);
        Assert.Empty(viewModel.NewPassword);
    }

    [Fact]
    public async Task IncorrectCurrentPasswordRemainsAnErrorWithoutSigningOut()
    {
        var session = new CurrentUserSessionStub();
        var user = new AuthenticatedUser(
            1,
            "admin",
            UserRole.Admin,
            null,
            "Admin",
            RequiresPasswordChange: false);
        session.SignIn(user);
        var authenticationService = new AuthenticationServiceStub
        {
            ChangePasswordFailure = new InvalidOperationException("Mật khẩu hiện tại không chính xác.")
        };
        var viewModel = new MainWindowViewModel(
            new InitialAdminSetupServiceStub(),
            authenticationService,
            session,
            new EmployeeManagementServiceStub(),
            new RestaurantCatalogServiceStub(),
            NullLogger<MainWindowViewModel>.Instance)
        {
            CurrentPassword = "WrongPassword123!",
            NewPassword = "NewPassword123!"
        };

        await viewModel.ChangePasswordCommand.ExecuteAsync(null);

        Assert.Same(user, session.User);
        Assert.Equal("Mật khẩu hiện tại không chính xác.", viewModel.ErrorMessage);
    }

    [Fact]
    public void ViewModelExposesVietnameseTitleAndErrorState()
    {
        var viewModel = new MainWindowViewModel(
            new InitialAdminSetupServiceStub(),
            new AuthenticationServiceStub(),
            new CurrentUserSessionStub(),
            new EmployeeManagementServiceStub(),
            new RestaurantCatalogServiceStub(),
            NullLogger<MainWindowViewModel>.Instance);

        Assert.Equal("DineEase - Quản lý nhà hàng", MainWindowViewModel.Title);
        Assert.False(viewModel.HasError);
        Assert.False(viewModel.HasStatusMessage);

        viewModel.ErrorMessage = "Có lỗi xảy ra.";

        Assert.True(viewModel.HasError);
        viewModel.StatusMessage = "Đã đăng nhập lại.";
        Assert.True(viewModel.HasStatusMessage);
        viewModel.StatusMessage = string.Empty;
        Assert.False(viewModel.HasStatusMessage);
        viewModel.ErrorMessage = string.Empty;
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public void ProductTagsCombineSelectedDropdownOptions()
    {
        var viewModel = new MainWindowViewModel(
            new InitialAdminSetupServiceStub(),
            new AuthenticationServiceStub(),
            new CurrentUserSessionStub(),
            new EmployeeManagementServiceStub(),
            new RestaurantCatalogServiceStub(),
            NullLogger<MainWindowViewModel>.Instance);

        viewModel.ProductTagOptions.Single(option => option.Name == "Đặc sản").IsSelected = true;
        viewModel.ProductTagOptions.Single(option => option.Name == "Bán chạy").IsSelected = true;

        Assert.Equal("Đặc sản, Bán chạy", viewModel.ProductTags);
        Assert.Equal("Đặc sản, Bán chạy", viewModel.ProductTagsDisplay);

        viewModel.ProductTagOptions.Single(option => option.Name == "Đặc sản").IsSelected = false;

        Assert.Equal("Bán chạy", viewModel.ProductTags);
    }

    [Fact]
    public void SelectingProductLoadsTagsIntoTheMultiSelectDropdown()
    {
        var viewModel = new MainWindowViewModel(
            new InitialAdminSetupServiceStub(),
            new AuthenticationServiceStub(),
            new CurrentUserSessionStub(),
            new EmployeeManagementServiceStub(),
            new RestaurantCatalogServiceStub(),
            NullLogger<MainWindowViewModel>.Instance);

        viewModel.SelectedProduct = new ProductSummary(
            1,
            "Món thử",
            null,
            "Đặc sản, Nhãn cũ",
            25_000m,
            IsAvailable: true,
            IsActive: true,
            CategoryId: 1,
            CategoryName: "Món chính");

        Assert.True(viewModel.ProductTagOptions.Single(option => option.Name == "Đặc sản").IsSelected);
        Assert.True(viewModel.ProductTagOptions.Single(option => option.Name == "Nhãn cũ").IsSelected);
        Assert.Equal("Đặc sản, Nhãn cũ", viewModel.ProductTags);
    }

    [Fact]
    public void FirstLoginPasswordRequirementBlocksStaffButNotAdminWorkspace()
    {
        var session = new CurrentUserSessionStub();
        session.SignIn(new AuthenticatedUser(
            1,
            "an.nguyen",
            UserRole.Staff,
            1,
            "Nguyễn Văn An",
            RequiresPasswordChange: true));
        var viewModel = new MainWindowViewModel(
            new InitialAdminSetupServiceStub(),
            new AuthenticationServiceStub(),
            session,
            new EmployeeManagementServiceStub(),
            new RestaurantCatalogServiceStub(),
            NullLogger<MainWindowViewModel>.Instance);

        Assert.True(viewModel.IsPasswordChangeRequired);
        Assert.False(viewModel.IsAuthenticatedWorkspace);
        Assert.False(viewModel.IsStaffScreen);

        session.SignIn(new AuthenticatedUser(
            2,
            "admin",
            UserRole.Admin,
            null,
            "Quản trị viên",
            RequiresPasswordChange: true));

        Assert.False(viewModel.IsPasswordChangeRequired);
        Assert.True(viewModel.IsAuthenticatedWorkspace);
        Assert.True(viewModel.IsAdminScreen);
        Assert.False(viewModel.IsReadOnlyStaff);
        Assert.False(viewModel.IsStaffOrderScreen);
    }

    [Fact]
    public async Task AdminWithTemporaryPasswordCanSignInAndLoadWorkspaceData()
    {
        var session = new CurrentUserSessionStub();
        var employeeService = new EmployeeManagementServiceStub();
        var catalogService = new RestaurantCatalogServiceStub();
        var authenticationService = new AuthenticationServiceStub
        {
            SignInResult = new AuthenticatedUser(
                2,
                "admin",
                UserRole.Admin,
                null,
                "Quản trị viên",
                RequiresPasswordChange: true),
            OnSuccessfulSignIn = session.SignIn
        };
        var viewModel = new MainWindowViewModel(
            new InitialAdminSetupServiceStub(),
            authenticationService,
            session,
            employeeService,
            catalogService,
            NullLogger<MainWindowViewModel>.Instance);

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsAdminScreen);
        Assert.False(viewModel.IsReadOnlyStaff);
        Assert.False(viewModel.IsStaffOrderScreen);
        Assert.Equal(1, employeeService.EmployeeListReadCount);
        Assert.Equal(1, employeeService.AccountListReadCount);
        Assert.Equal(1, catalogService.TableListReadCount);
        Assert.Equal(1, catalogService.CategoryListReadCount);
        Assert.Equal(1, catalogService.ProductListReadCount);
    }

    private sealed class InitialAdminSetupServiceStub : IInitialAdminSetupService
    {
        public Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task CreateInitialAdminAsync(
            string username,
            string password,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class AuthenticationServiceStub : IAuthenticationService
    {
        public Exception? ChangePasswordFailure { get; init; }

        public Exception? SignInFailure { get; init; }

        public AuthenticatedUser? SignInResult { get; init; }

        public Action<AuthenticatedUser>? OnSuccessfulSignIn { get; init; }

        public Task<AuthenticatedUser?> SignInAsync(
            string username,
            string password,
            CancellationToken cancellationToken = default)
        {
            if (SignInFailure is not null)
            {
                return Task.FromException<AuthenticatedUser?>(SignInFailure);
            }

            if (SignInResult is { } user)
            {
                OnSuccessfulSignIn?.Invoke(user);
                return Task.FromResult<AuthenticatedUser?>(user);
            }

            return Task.FromResult<AuthenticatedUser?>(null);
        }

        public Task ChangePasswordAsync(
            int userId,
            string currentPassword,
            string newPassword,
            CancellationToken cancellationToken = default) =>
            ChangePasswordFailure is null
                ? Task.CompletedTask
                : Task.FromException(ChangePasswordFailure);
    }

    private sealed class CurrentUserSessionStub : ICurrentUserSession
    {
        public AuthenticatedUser? User { get; private set; }

        public void SignIn(AuthenticatedUser user) => User = user;

        public void SignOut() => User = null;
    }

    private sealed class EmployeeManagementServiceStub : IEmployeeManagementService
    {
        public int EmployeeListReadCount { get; private set; }

        public int AccountListReadCount { get; private set; }

        public (int EmployeeId, string FullName, string? Phone, string? Email)? LastEmployeeUpdate { get; private set; }

        public (int EmployeeId, bool IsActive)? LastEmployeeStatusChange { get; private set; }

        public Task<IReadOnlyList<EmployeeAccountSummary>> GetEmployeesAsync(
            CancellationToken cancellationToken = default)
        {
            EmployeeListReadCount++;
            return Task.FromResult<IReadOnlyList<EmployeeAccountSummary>>([]);
        }

        public Task<IReadOnlyList<AccountSummary>> GetAccountsAsync(
            CancellationToken cancellationToken = default)
        {
            AccountListReadCount++;
            return Task.FromResult<IReadOnlyList<AccountSummary>>([]);
        }

        public Task CreateEmployeeAsync(
            string fullName,
            string? phone,
            string? email,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<GeneratedEmployeeCredentials> CreateAccountAsync(
            string fullName,
            string? phone,
            string? email,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new GeneratedEmployeeCredentials(1, "an.nguyen", "Temporary123!"));

        public Task CreateEmployeeProfileAsync(
            int accountId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAccountEmployeeNameAsync(
            int accountId,
            string fullName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAccountAsync(
            int accountId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateEmployeeAsync(
            int employeeId,
            string fullName,
            string? phone,
            string? email,
            CancellationToken cancellationToken = default)
        {
            LastEmployeeUpdate = (employeeId, fullName, phone, email);
            return Task.CompletedTask;
        }

        public Task DeleteEmployeeProfileAsync(
            int employeeId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAccountAsync(
            int accountId,
            string username,
            UserRole role,
            string? newPassword,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetEmployeeActiveAsync(
            int employeeId,
            bool isActive,
            CancellationToken cancellationToken = default)
        {
            LastEmployeeStatusChange = (employeeId, isActive);
            return Task.CompletedTask;
        }

    }

    private sealed class RestaurantCatalogServiceStub : IRestaurantCatalogService
    {
        public IReadOnlyList<RestaurantTableSummary> TablesToReturn { get; set; } = [];

        public int TableListReadCount { get; private set; }

        public int CategoryListReadCount { get; private set; }

        public int ProductListReadCount { get; private set; }

        public Task<IReadOnlyList<RestaurantTableSummary>> GetTablesAsync(
            CancellationToken cancellationToken = default)
        {
            TableListReadCount++;
            return Task.FromResult(TablesToReturn);
        }

        public Task<IReadOnlyList<CategorySummary>> GetCategoriesAsync(
            CancellationToken cancellationToken = default)
        {
            CategoryListReadCount++;
            return Task.FromResult<IReadOnlyList<CategorySummary>>([]);
        }

        public Task<IReadOnlyList<ProductSummary>> GetProductsAsync(
            string? searchText = null,
            int? categoryId = null,
            CancellationToken cancellationToken = default)
        {
            ProductListReadCount++;
            return Task.FromResult<IReadOnlyList<ProductSummary>>([]);
        }

        public Task<IReadOnlyList<PromotionSummary>> GetPromotionsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PromotionSummary>>([]);

        public Task CreatePromotionAsync(
            string name,
            decimal discountPercentage,
            DateTimeOffset startsAt,
            DateTimeOffset endsAt,
            int? productId,
            int? categoryId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdatePromotionAsync(
            int promotionId,
            string name,
            decimal discountPercentage,
            DateTimeOffset startsAt,
            DateTimeOffset endsAt,
            int? productId,
            int? categoryId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetPromotionActiveAsync(
            int promotionId,
            bool isActive,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CreateTableAsync(
            string tableNumber,
            int seatCount,
            string area = "Chung",
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateTableAsync(
            int tableId,
            string tableNumber,
            int seatCount,
            string area = "Chung",
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetTableActiveAsync(
            int tableId,
            bool isActive,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteTableAsync(int tableId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CreateCategoryAsync(
            string name,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateCategoryAsync(
            int categoryId,
            string name,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetCategoryActiveAsync(
            int categoryId,
            bool isActive,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteCategoryAsync(int categoryId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CreateProductAsync(
            string name,
            string? tags,
            decimal price,
            int categoryId,
            string? imagePath,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateProductAsync(
            int productId,
            string name,
            string? tags,
            decimal price,
            int categoryId,
            string? imagePath,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetProductAvailabilityAsync(
            int productId,
            bool isAvailable,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetProductActiveAsync(
            int productId,
            bool isActive,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteProductAsync(int productId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
