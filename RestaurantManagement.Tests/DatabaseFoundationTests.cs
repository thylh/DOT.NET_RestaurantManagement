using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Core.Enums;
using RestaurantManagement.Core.Services;
using RestaurantManagement.Infrastructure.Data;
using RestaurantManagement.Infrastructure.Services;

namespace RestaurantManagement.Tests;

public class DatabaseFoundationTests
{
    [Fact]
    public async Task MigrationPersistsDataAndInitialAdminCanOnlyBeCreatedOnce()
    {
        var connectionString = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\MSSQLLocalDB",
            InitialCatalog = $"RestaurantManagementTests_{Guid.NewGuid():N}",
            IntegratedSecurity = true,
            MultipleActiveResultSets = true,
            TrustServerCertificate = true
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        const string password = "Phase-B-test-password-2026";

        try
        {
            await using (var dbContext = new RestaurantDbContext(options))
            {
                await dbContext.Database.MigrateAsync();

                var setupService = new InitialAdminSetupService(dbContext);
                Assert.True(await setupService.IsSetupRequiredAsync());

                await setupService.CreateInitialAdminAsync("  test-admin  ", password);

                Assert.False(await setupService.IsSetupRequiredAsync());
                var account = await dbContext.UserAccounts.SingleAsync();
                Assert.Equal("test-admin", account.Username);
                Assert.Equal(UserRole.Admin, account.Role);
                Assert.NotEqual(password, account.PasswordHash);
                Assert.True(global::BCrypt.Net.BCrypt.Verify(password, account.PasswordHash));
                await Assert.ThrowsAsync<InitialAdminAlreadyExistsException>(
                    () => setupService.CreateInitialAdminAsync("another-admin", password));

                var session = new CurrentUserSession();
                var authenticationService = new AuthenticationService(dbContext, session);
                Assert.Null(await authenticationService.SignInAsync("test-admin", "incorrect-password"));
                Assert.Null(session.User);

                var admin = await authenticationService.SignInAsync("test-admin", password);
                Assert.NotNull(admin);
                Assert.Equal(UserRole.Admin, admin.Role);

                var employeeService = new EmployeeManagementService(dbContext, session);
                await employeeService.CreateEmployeeAsync(
                    "Sample Staff",
                    "0123456789",
                    "staff@example.test",
                    "sample-staff",
                    "Phase-C-staff-password-2026",
                    UserRole.Staff);

                var employees = await employeeService.GetEmployeesAsync();
                var staff = Assert.Single(employees);
                Assert.Equal("Sample Staff", staff.FullName);
                Assert.Equal(UserRole.Staff, staff.Role);
                Assert.NotNull(staff.AccountId);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => employeeService.SetAccountActiveAsync(admin.Id, false));

                var staffAccountId = staff.AccountId!.Value;
                await employeeService.UpdateAccountAsync(
                    staffAccountId,
                    "renamed-staff",
                    UserRole.Staff,
                    "Phase-C-new-staff-password-2026");
                Assert.Null(await authenticationService.SignInAsync(
                    "sample-staff",
                    "Phase-C-staff-password-2026"));
                await employeeService.SetAccountActiveAsync(staffAccountId, false);
                Assert.Null(await authenticationService.SignInAsync(
                    "renamed-staff",
                    "Phase-C-new-staff-password-2026"));

                await employeeService.SetAccountActiveAsync(staffAccountId, true);
                var signedInStaff = await authenticationService.SignInAsync(
                    "renamed-staff",
                    "Phase-C-new-staff-password-2026");
                Assert.NotNull(signedInStaff);
                Assert.Equal(UserRole.Staff, signedInStaff.Role);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => employeeService.GetEmployeesAsync());

                session.SignIn(admin);
                await employeeService.SetEmployeeActiveAsync(staff.EmployeeId, false);
                Assert.Null(await authenticationService.SignInAsync(
                    "renamed-staff",
                    "Phase-C-new-staff-password-2026"));
                await employeeService.SetEmployeeActiveAsync(staff.EmployeeId, true);

                await authenticationService.ChangePasswordAsync(
                    admin.Id,
                    password,
                    "Phase-C-new-admin-password-2026");
                Assert.Null(await authenticationService.SignInAsync("test-admin", password));
                Assert.NotNull(await authenticationService.SignInAsync(
                    "test-admin",
                    "Phase-C-new-admin-password-2026"));
            }

            await using var verificationContext = new RestaurantDbContext(options);
            var persistedAccount = await verificationContext.UserAccounts
                .SingleAsync(account => account.Username == "test-admin");
            Assert.Equal("test-admin", persistedAccount.Username);
            Assert.Equal(2, await verificationContext.UserAccounts.CountAsync());
        }
        finally
        {
            await using var cleanupContext = new RestaurantDbContext(options);
            await cleanupContext.Database.EnsureDeletedAsync();
        }
    }
}
