using Microsoft.Extensions.Logging.Abstractions;
using RestaurantManagement.Core.Enums;
using RestaurantManagement.Core.Models;
using RestaurantManagement.Core.Services;
using RestaurantManagement.WPF.ViewModels;

namespace RestaurantManagement.Tests;

public class MainWindowViewModelTests
{
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

        Assert.Equal("Quản lý nhà hàng", viewModel.Title);
        Assert.False(viewModel.HasError);

        viewModel.ErrorMessage = "Có lỗi xảy ra.";

        Assert.True(viewModel.HasError);
        viewModel.ErrorMessage = string.Empty;
        Assert.False(viewModel.HasError);
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
        public Task<AuthenticatedUser?> SignInAsync(
            string username,
            string password,
            CancellationToken cancellationToken = default) => Task.FromResult<AuthenticatedUser?>(null);

        public Task ChangePasswordAsync(
            int userId,
            string currentPassword,
            string newPassword,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CurrentUserSessionStub : ICurrentUserSession
    {
        public AuthenticatedUser? User { get; private set; }

        public void SignIn(AuthenticatedUser user) => User = user;

        public void SignOut() => User = null;
    }

    private sealed class EmployeeManagementServiceStub : IEmployeeManagementService
    {
        public Task<IReadOnlyList<EmployeeAccountSummary>> GetEmployeesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EmployeeAccountSummary>>([]);

        public Task CreateEmployeeAsync(
            string fullName,
            string? phone,
            string? email,
            string username,
            string password,
            UserRole role,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateEmployeeAsync(
            int employeeId,
            string fullName,
            string? phone,
            string? email,
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
            CancellationToken cancellationToken = default) => Task.CompletedTask;
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
