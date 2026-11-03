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
            true,
            RequiresPasswordChange: true)
        {
            TemporaryPassword = "Temporary123!"
        };

        Assert.Equal("Temporary123!", account.PasswordDisplay);
        Assert.Equal("Tạo hồ sơ", account.ProfileActionLabel);
        Assert.Equal("Khóa tài khoản", account.AccountStatusActionLabel);
        Assert.Equal("Mở khóa tài khoản", (account with { IsAccountActive = false }).AccountStatusActionLabel);
        Assert.Equal(
            "Đã đổi mật khẩu",
            (account with { RequiresPasswordChange = false }).PasswordDisplay);
        Assert.Equal(
            "Đã tạo hs",
            (account with { EmployeeId = 12 }).ProfileActionLabel);
    }

    [Fact]
    public async Task ToggleAccountStatusCommandLocksSelectedAccount()
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
            SelectedAccount = new AccountSummary(
                7,
                12,
                "Nguyễn Văn An",
                "0123456789",
                null,
                true,
                "an.nguyen",
                UserRole.Staff,
                true,
                RequiresPasswordChange: true)
        };

        await viewModel.ToggleAccountStatusCommand.ExecuteAsync(null);

        Assert.Equal((7, false), employeeService.LastAccountStatusChange!.Value);
        Assert.Equal("Đã khóa tài khoản.", viewModel.StatusMessage);
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

        Assert.Equal("Quản lý nhà hàng", MainWindowViewModel.Title);
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
    public void FirstLoginPasswordRequirementBlocksTheAuthenticatedWorkspace()
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

        public Task<AuthenticatedUser?> SignInAsync(
            string username,
            string password,
            CancellationToken cancellationToken = default) => Task.FromResult<AuthenticatedUser?>(null);

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
        public (int AccountId, bool IsActive)? LastAccountStatusChange { get; private set; }

        public Task<IReadOnlyList<EmployeeAccountSummary>> GetEmployeesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EmployeeAccountSummary>>([]);

        public Task<IReadOnlyList<AccountSummary>> GetAccountsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AccountSummary>>([]);

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
            CancellationToken cancellationToken = default) => Task.CompletedTask;

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
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetAccountActiveAsync(
            int accountId,
            bool isActive,
            CancellationToken cancellationToken = default)
        {
            LastAccountStatusChange = (accountId, isActive);
            return Task.CompletedTask;
        }
    }

    private sealed class RestaurantCatalogServiceStub : IRestaurantCatalogService
    {
        public Task<IReadOnlyList<RestaurantTableSummary>> GetTablesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RestaurantTableSummary>>([]);

        public Task<IReadOnlyList<CategorySummary>> GetCategoriesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CategorySummary>>([]);

        public Task<IReadOnlyList<ProductSummary>> GetProductsAsync(
            string? searchText = null,
            int? categoryId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProductSummary>>([]);

        public Task CreateTableAsync(
            int tableNumber,
            int seatCount,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateTableAsync(
            int tableId,
            int tableNumber,
            int seatCount,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetTableActiveAsync(
            int tableId,
            bool isActive,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteTableAsync(int tableId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CreateCategoryAsync(
            string name,
            string? description,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateCategoryAsync(
            int categoryId,
            string name,
            string? description,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetCategoryActiveAsync(
            int categoryId,
            bool isActive,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteCategoryAsync(int categoryId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CreateProductAsync(
            string name,
            string? description,
            decimal price,
            int categoryId,
            string? imagePath,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateProductAsync(
            int productId,
            string name,
            string? description,
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
