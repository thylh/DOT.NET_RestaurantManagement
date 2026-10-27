using System.Collections.ObjectModel;
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
    ILogger<MainWindowViewModel> logger) : ObservableObject
{
    public ObservableCollection<EmployeeAccountSummary> Employees { get; } = [];

    public IReadOnlyList<UserRole> AvailableRoles { get; } = Enum.GetValues<UserRole>();

    public string Title => "Restaurant Management";

    public bool IsAuthenticated => currentUserSession.User is not null;

    public bool IsAdmin => currentUserSession.User?.Role == UserRole.Admin;

    public bool IsSetupScreen => IsSetupRequired;

    public bool IsLoginScreen => !IsSetupRequired && !IsAuthenticated;

    public bool IsAdminScreen => IsAuthenticated && IsAdmin;

    public bool IsStaffScreen => IsAuthenticated && !IsAdmin;

    public string CurrentUserDescription => currentUserSession.User is { } user
        ? $"{user.Username} ({user.Role})"
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

    public async Task InitializeAsync()
    {
        try
        {
            IsSetupRequired = await initialAdminSetupService.IsSetupRequiredAsync();
            if (!IsSetupRequired)
            {
                StatusMessage = "Sign in with your account to continue.";
            }
        }
        catch (Exception exception) when (
            exception is DbUpdateException or InvalidOperationException or SqlException)
        {
            logger.LogError(exception, "Could not check whether the initial Admin setup is required.");
            ErrorMessage = "Could not check the database setup state. Restart the application after checking LocalDB.";
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
            StatusMessage = "Admin account created. Sign in to continue.";
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
                ErrorMessage = "Username or password is incorrect, or the account is inactive.";
                return;
            }

            OnPropertyChanged(nameof(IsAuthenticated));
            OnPropertyChanged(nameof(IsAdmin));
            OnPropertyChanged(nameof(IsAdminScreen));
            OnPropertyChanged(nameof(IsStaffScreen));
            OnPropertyChanged(nameof(IsLoginScreen));
            OnPropertyChanged(nameof(CurrentUserDescription));
            StatusMessage = $"Signed in as {user.Username}.";
            if (user.Role == UserRole.Admin)
            {
                await RefreshEmployeesAsync();
            }
        });
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        currentUserSession.SignOut();
        Employees.Clear();
        SelectedEmployee = null;
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(IsAdmin));
        OnPropertyChanged(nameof(IsAdminScreen));
        OnPropertyChanged(nameof(IsStaffScreen));
        OnPropertyChanged(nameof(IsLoginScreen));
        OnPropertyChanged(nameof(CurrentUserDescription));
        StatusMessage = "Signed out.";
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        if (currentUserSession.User is not { } user)
        {
            ErrorMessage = "Sign in before changing your password.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await authenticationService.ChangePasswordAsync(user.Id, CurrentPassword, NewPassword);
            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            StatusMessage = "Password changed successfully.";
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
            StatusMessage = "Employee and account created.";
        });
    }

    [RelayCommand]
    private async Task UpdateEmployeeAsync()
    {
        if (SelectedEmployee is not { } employee)
        {
            ErrorMessage = "Select an employee to update.";
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
            StatusMessage = "Employee profile updated.";
        });
    }

    [RelayCommand]
    private async Task UpdateAccountAsync()
    {
        if (SelectedEmployee?.AccountId is not { } accountId)
        {
            ErrorMessage = "Select an employee with an account to update.";
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
            StatusMessage = "Account updated.";
        });
    }

    [RelayCommand]
    private async Task ToggleEmployeeStatusAsync()
    {
        if (SelectedEmployee is not { } employee)
        {
            ErrorMessage = "Select an employee first.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await employeeManagementService.SetEmployeeActiveAsync(
                employee.EmployeeId,
                !employee.IsEmployeeActive);
            await RefreshEmployeesAsync();
            StatusMessage = "Employee status updated.";
        });
    }

    [RelayCommand]
    private async Task ToggleAccountStatusAsync()
    {
        if (SelectedEmployee is not { AccountId: { } accountId, IsAccountActive: { } isActive })
        {
            ErrorMessage = "The selected employee has no account to update.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await employeeManagementService.SetAccountActiveAsync(accountId, !isActive);
            await RefreshEmployeesAsync();
            StatusMessage = "Account status updated.";
        });
    }

    private async Task RefreshEmployeesAsync()
    {
        var employees = await employeeManagementService.GetEmployeesAsync();
        Employees.Clear();
        foreach (var employee in employees)
        {
            Employees.Add(employee);
        }
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
                logger.LogError(exception, "The account operation failed while accessing the database.");
                ErrorMessage = "The operation could not be saved. Check the database connection and try again.";
            }
            else
            {
                logger.LogWarning(exception, "The requested account operation could not be completed.");
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

    partial void OnIsSetupRequiredChanged(bool value)
    {
        OnPropertyChanged(nameof(IsSetupScreen));
        OnPropertyChanged(nameof(IsLoginScreen));
    }
}
