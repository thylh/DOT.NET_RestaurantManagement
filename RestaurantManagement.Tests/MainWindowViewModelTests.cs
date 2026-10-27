using Microsoft.Extensions.Logging.Abstractions;
using RestaurantManagement.Core.Enums;
using RestaurantManagement.Core.Models;
using RestaurantManagement.Core.Services;
using RestaurantManagement.WPF.ViewModels;

namespace RestaurantManagement.Tests;

public class MainWindowViewModelTests
{
    [Fact]
    public void TitleIsInitializedForMainWindow()
    {
        var viewModel = new MainWindowViewModel(
            new InitialAdminSetupServiceStub(),
            new AuthenticationServiceStub(),
            new CurrentUserSessionStub(),
            new EmployeeManagementServiceStub(),
            NullLogger<MainWindowViewModel>.Instance);

        Assert.Equal("Restaurant Management", viewModel.Title);
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
}
