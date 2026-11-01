using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Core.Entities;
using RestaurantManagement.Core.Enums;
using RestaurantManagement.Core.Models;
using RestaurantManagement.Core.Services;
using RestaurantManagement.Infrastructure.Data;
using RestaurantManagement.Infrastructure.Services;

namespace RestaurantManagement.Tests;

public class DatabaseFoundationTests
{
    [Fact]
    public async Task AccountAndRestaurantWorkflowsEnforceRulesAndPersistData()
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

                var catalogService = new RestaurantCatalogService(dbContext, session);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => catalogService.GetTablesAsync());

                var admin = await authenticationService.SignInAsync("test-admin", password);
                Assert.NotNull(admin);
                Assert.Equal(UserRole.Admin, admin.Role);

                await catalogService.CreateTableAsync(1, 4);
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                    () => catalogService.CreateTableAsync(0, 4));
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => catalogService.CreateTableAsync(1, 2));
                await catalogService.CreateTableAsync(2, 2);
                var unusedTable = (await catalogService.GetTablesAsync())
                    .Single(table => table.TableNumber == 2);
                await catalogService.DeleteTableAsync(unusedTable.Id);

                await catalogService.CreateCategoryAsync("Món chính", "Món ăn chính");
                var category = Assert.Single(await catalogService.GetCategoriesAsync());
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => catalogService.CreateCategoryAsync("Món chính", null));
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                    () => catalogService.CreateProductAsync("Món lỗi", null, 0, category.Id, null));
                await catalogService.CreateCategoryAsync("Danh mục trống", null);
                var unusedCategory = (await catalogService.GetCategoriesAsync())
                    .Single(candidate => candidate.Name == "Danh mục trống");
                await catalogService.DeleteCategoryAsync(unusedCategory.Id);
                await catalogService.CreateProductAsync("Phở bò", "Phở bò tái", 55_000m, category.Id, null);
                var product = Assert.Single(await catalogService.GetProductsAsync("Phở"));
                Assert.Equal(category.Name, product.CategoryName);
                Assert.Equal(55_000m, product.Price);
                Assert.Single(await catalogService.GetProductsAsync(categoryId: category.Id));
                await catalogService.CreateProductAsync("Trà đá", null, 5_000m, category.Id, null);
                var unusedProduct = (await catalogService.GetProductsAsync())
                    .Single(candidate => candidate.Name == "Trà đá");
                await catalogService.DeleteProductAsync(unusedProduct.Id);
                await catalogService.UpdateTableAsync(
                    (await catalogService.GetTablesAsync()).Single().Id,
                    1,
                    5);
                Assert.Equal(5, Assert.Single(await catalogService.GetTablesAsync()).SeatCount);
                await catalogService.UpdateCategoryAsync(category.Id, "Món ăn", "Món ăn chính");
                category = Assert.Single(await catalogService.GetCategoriesAsync());
                Assert.Equal("Món ăn", category.Name);
                await catalogService.UpdateProductAsync(
                    product.Id,
                    "Phở bò đặc biệt",
                    product.Description,
                    65_000m,
                    category.Id,
                    null);
                Assert.Empty(await catalogService.GetProductsAsync("Món không tồn tại"));

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
                Assert.Single(await catalogService.GetTablesAsync());
                Assert.Single(await catalogService.GetCategoriesAsync());
                Assert.Single(await catalogService.GetProductsAsync());
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => catalogService.CreateTableAsync(2, 2));
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => catalogService.CreateCategoryAsync("Không được phép", null));

                session.SignIn(admin);
                await catalogService.SetProductActiveAsync(product.Id, false);
                session.SignIn(signedInStaff);
                Assert.Empty(await catalogService.GetProductsAsync());
                session.SignIn(admin);
                await catalogService.SetProductActiveAsync(product.Id, true);
                var table = Assert.Single(await catalogService.GetTablesAsync());
                await catalogService.SetProductAvailabilityAsync(product.Id, false);
                Assert.False(Assert.Single(await catalogService.GetProductsAsync()).IsAvailable);
                await catalogService.SetProductAvailabilityAsync(product.Id, true);

                var order = new RestaurantOrder
                {
                    RestaurantTableId = table.Id,
                    CreatedByEmployeeId = staff.EmployeeId,
                    Status = OrderStatus.Open,
                    OrderDetails =
                    [
                        new OrderDetail
                        {
                            ProductId = product.Id,
                            ProductNameSnapshot = "Phở bò",
                            UnitPriceSnapshot = 55_000m,
                            Quantity = 1
                        }
                    ]
                };
                dbContext.RestaurantOrders.Add(order);
                await dbContext.SaveChangesAsync();

                Assert.True(Assert.Single(await catalogService.GetTablesAsync()).IsOccupied);
                Assert.Equal(55_000m, await dbContext.OrderDetails
                    .Where(detail => detail.RestaurantOrderId == order.Id)
                    .Select(detail => detail.UnitPriceSnapshot)
                    .SingleAsync());
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => catalogService.SetTableActiveAsync(table.Id, false));
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => catalogService.DeleteTableAsync(table.Id));
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => catalogService.DeleteProductAsync(product.Id));
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => catalogService.DeleteCategoryAsync(category.Id));

                await catalogService.SetCategoryActiveAsync(category.Id, false);
                session.SignIn(signedInStaff);
                Assert.Empty(await catalogService.GetCategoriesAsync());
                Assert.Empty(await catalogService.GetProductsAsync());
                session.SignIn(admin);
                await catalogService.SetCategoryActiveAsync(category.Id, true);

                order.Status = OrderStatus.Paid;
                await dbContext.SaveChangesAsync();
                await catalogService.SetTableActiveAsync(table.Id, false);
                session.SignIn(signedInStaff);
                Assert.Empty(await catalogService.GetTablesAsync());
                session.SignIn(admin);
                await catalogService.SetTableActiveAsync(table.Id, true);

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
