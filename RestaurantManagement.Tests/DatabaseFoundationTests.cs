using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using RestaurantManagement.Core.Entities;
using RestaurantManagement.Core.Enums;
using RestaurantManagement.Core.Models;
using RestaurantManagement.Core.Services;
using RestaurantManagement.Infrastructure.Data;
using RestaurantManagement.Infrastructure.Services;

namespace RestaurantManagement.Tests;

public class DatabaseFoundationTests
{
    private const string TestDatabaseName = "RestaurantManagement_Test";

    [MySqlFact]
    public async Task AccountAndRestaurantWorkflowsEnforceRulesAndPersistData()
    {
        var connectionString = Environment.GetEnvironmentVariable("RESTAURANT_TEST_CONNECTION_STRING")!;
        var connectionStringBuilder = new MySqlConnectionStringBuilder(connectionString);
        if (!string.Equals(
                connectionStringBuilder.Database,
                TestDatabaseName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Database thử nghiệm phải có tên chính xác là {TestDatabaseName} để tránh xóa nhầm dữ liệu.");
        }

        var options = new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseMySQL(connectionString)
            .Options;
        const string password = "Phase-B-test-password-2026";
        var databaseReady = false;

        try
        {
            await using (var dbContext = new RestaurantDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                databaseReady = true;
                await ClearTestDataAsync(dbContext);

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

                await catalogService.CreateTableAsync("Bàn 1", 4, "Sảnh chính");
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                    () => catalogService.CreateTableAsync("Bàn 1", 0));
                await Assert.ThrowsAsync<ArgumentException>(
                    () => catalogService.CreateTableAsync(" ", 4));
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => catalogService.CreateTableAsync(" Bàn 1 ", 2, "Sảnh chính"));
                await catalogService.CreateTableAsync("Bàn 1", 2, "Chung");
                var sameNumberInAnotherArea = (await catalogService.GetTablesAsync())
                    .Single(table => table.TableNumber == "Bàn 1" && table.Area == "Chung");
                await catalogService.DeleteTableAsync(sameNumberInAnotherArea.Id);
                await catalogService.CreateTableAsync("Khu cửa sổ", 2);
                var unusedTable = (await catalogService.GetTablesAsync())
                    .Single(table => table.TableNumber == "Khu cửa sổ");
                await catalogService.DeleteTableAsync(unusedTable.Id);

                await catalogService.CreateCategoryAsync("Món chính");
                var category = Assert.Single(await catalogService.GetCategoriesAsync());
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => catalogService.CreateCategoryAsync("Món chính"));
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                    () => catalogService.CreateProductAsync("Món lỗi", null, 0, category.Id, null));
                await catalogService.CreateCategoryAsync("Danh mục trống");
                var unusedCategory = (await catalogService.GetCategoriesAsync())
                    .Single(candidate => candidate.Name == "Danh mục trống");
                await catalogService.DeleteCategoryAsync(unusedCategory.Id);
                await catalogService.CreateProductAsync(
                    "Phở bò",
                    "Đặc sản, Bán chạy, đặc sản",
                    55_000m,
                    category.Id,
                    null);
                var product = Assert.Single(await catalogService.GetProductsAsync("Phở"));
                Assert.Equal(category.Name, product.CategoryName);
                Assert.Equal(55_000m, product.Price);
                Assert.Equal("Đặc sản, Bán chạy", product.Tags);
                Assert.Single(await catalogService.GetProductsAsync(categoryId: category.Id));
                Assert.Single(await catalogService.GetProductsAsync("Đặc sản"));
                await catalogService.CreateProductAsync("Trà đá", null, 5_000m, category.Id, null);
                var unusedProduct = (await catalogService.GetProductsAsync())
                    .Single(candidate => candidate.Name == "Trà đá");
                await catalogService.DeleteProductAsync(unusedProduct.Id);
                await catalogService.UpdateTableAsync(
                    (await catalogService.GetTablesAsync()).Single().Id,
                    "Bàn 1",
                    5,
                    "Tầng trệt");
                Assert.Equal(5, Assert.Single(await catalogService.GetTablesAsync()).SeatCount);
                Assert.Equal("Tầng trệt", Assert.Single(await catalogService.GetTablesAsync()).Area);
                Assert.Equal("Bàn 1", Assert.Single(await catalogService.GetTablesAsync()).TableNumber);
                await catalogService.UpdateCategoryAsync(category.Id, "Món ăn");
                category = Assert.Single(await catalogService.GetCategoriesAsync());
                Assert.Equal("Món ăn", category.Name);
                await catalogService.UpdateProductAsync(
                    product.Id,
                    "Phở bò đặc biệt",
                    product.Tags,
                    65_000m,
                    category.Id,
                    null);
                Assert.Empty(await catalogService.GetProductsAsync("Món không tồn tại"));

                var employeeService = new EmployeeManagementService(dbContext, session);
                var staffCredentials = await employeeService.CreateAccountAsync(
                    "Nguyễn Văn An",
                    "0123456789",
                    "staff@example.test");
                await Assert.ThrowsAsync<ArgumentException>(
                    () => employeeService.CreateAccountAsync(
                        "Số điện thoại sai",
                        "abc",
                        null));
                await Assert.ThrowsAsync<ArgumentException>(
                    () => employeeService.CreateAccountAsync(
                        "Email sai",
                        null,
                        "not-an-email"));

                Assert.Empty(await employeeService.GetEmployeesAsync());
                var accountList = await employeeService.GetAccountsAsync();
                Assert.All(accountList, account => Assert.Equal(UserRole.Staff, account.Role));
                var accountBeforeProfile = Assert.Single(accountList);
                Assert.Equal(staffCredentials.AccountId, accountBeforeProfile.AccountId);
                Assert.False(accountBeforeProfile.HasEmployeeProfile);
                Assert.False(accountBeforeProfile.EmployeeProfileDeleted);
                Assert.Equal("Chưa tạo hồ sơ", accountBeforeProfile.EmployeeProfileStatusLabel);
                Assert.Equal(staffCredentials.TemporaryPassword, accountBeforeProfile.PasswordDisplay);
                var missingProfileSignIn = Assert.IsType<AuthenticatedUser>(
                    await authenticationService.SignInAsync(
                        staffCredentials.Username,
                        staffCredentials.TemporaryPassword));
                Assert.Null(missingProfileSignIn.EmployeeId);
                Assert.False(missingProfileSignIn.RequiresPasswordChange);
                Assert.True((await dbContext.UserAccounts
                    .SingleAsync(account => account.Id == staffCredentials.AccountId))
                    .RequiresPasswordChange);
                session.SignIn(admin);
                var missingAccountSignIn = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => authenticationService.SignInAsync(
                        "missing-account",
                        staffCredentials.TemporaryPassword));
                Assert.Equal("Tài khoản không tồn tại.", missingAccountSignIn.Message);

                await employeeService.CreateEmployeeProfileAsync(staffCredentials.AccountId);
                var employees = await employeeService.GetEmployeesAsync();
                var staff = Assert.Single(employees);
                Assert.Equal("Nguyễn Văn An", staff.FullName);
                Assert.Equal(UserRole.Staff, staff.Role);
                Assert.Equal(staffCredentials.AccountId, staff.AccountId);
                Assert.Equal("an.nguyen", staffCredentials.Username);
                Assert.Equal(staffCredentials.Username, staff.Username);
                Assert.Equal(20, staffCredentials.TemporaryPassword.Length);
                Assert.True(staff.RequiresPasswordChange);
                await employeeService.UpdateAccountEmployeeNameAsync(
                    staffCredentials.AccountId,
                    "Nguyễn An");
                Assert.Equal(
                    "Nguyễn An",
                    Assert.Single(await employeeService.GetEmployeesAsync()).FullName);
                Assert.Equal(
                    "Nguyễn An",
                    Assert.Single(await employeeService.GetAccountsAsync()).FullName);
                await employeeService.UpdateAccountEmployeeNameAsync(
                    staffCredentials.AccountId,
                    "Trần Văn Bình");
                var renamedAccount = Assert.Single(await employeeService.GetAccountsAsync());
                Assert.Equal("Trần Văn Bình", renamedAccount.FullName);
                Assert.Equal("binh.tran", renamedAccount.Username);
                Assert.Equal(
                    "binh.tran",
                    (await dbContext.UserAccounts.SingleAsync(account => account.Id == staffCredentials.AccountId))
                    .Username);
                Assert.True(global::BCrypt.Net.BCrypt.Verify(
                    staffCredentials.TemporaryPassword,
                    (await dbContext.UserAccounts.SingleAsync(account => account.Id == staffCredentials.AccountId))
                    .PasswordHash));
                var duplicateNameCredentials = await employeeService.CreateAccountAsync(
                    "Nguyễn Văn An",
                    null,
                    "second-staff@example.test");
                Assert.Equal("an.nguyen", duplicateNameCredentials.Username);
                var thirdStaffCredentials = await employeeService.CreateAccountAsync(
                    "Nguyễn Văn An",
                    null,
                    "third-staff@example.test");
                Assert.Equal("an.nguyen2", thirdStaffCredentials.Username);
                Assert.NotEqual(
                    staffCredentials.TemporaryPassword,
                    duplicateNameCredentials.TemporaryPassword);
                await employeeService.DeleteAccountAsync(duplicateNameCredentials.AccountId);
                await employeeService.DeleteAccountAsync(thirdStaffCredentials.AccountId);
                Assert.DoesNotContain(
                    await employeeService.GetAccountsAsync(),
                    account => account.AccountId == duplicateNameCredentials.AccountId);
                await employeeService.CreateAccountAsync("Nguyễn Văn Bình", null, null);

                var staffAccountId = staffCredentials.AccountId;
                await employeeService.UpdateAccountAsync(
                    staffAccountId,
                    "renamed-staff",
                    UserRole.Staff,
                    "Phase-C-new-staff-password-2026");
                var previousUsernameSignIn = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => authenticationService.SignInAsync(
                        staffCredentials.Username,
                        staffCredentials.TemporaryPassword));
                Assert.Equal("Tài khoản không tồn tại.", previousUsernameSignIn.Message);
                Assert.Null(await authenticationService.SignInAsync(
                    "renamed-staff",
                    staffCredentials.TemporaryPassword));
                var signedInStaff = await authenticationService.SignInAsync(
                    "renamed-staff",
                    "Phase-C-new-staff-password-2026");
                Assert.NotNull(signedInStaff);
                Assert.Equal(UserRole.Staff, signedInStaff.Role);
                Assert.True(signedInStaff.RequiresPasswordChange);
                await authenticationService.ChangePasswordAsync(
                    staffAccountId,
                    "Phase-C-new-staff-password-2026",
                    "Phase-C-rotated-staff-password-2026");
                signedInStaff = Assert.IsType<AuthenticatedUser>(session.User);
                Assert.False(signedInStaff.RequiresPasswordChange);
                Assert.False((await dbContext.UserAccounts
                    .SingleAsync(account => account.Id == staffAccountId))
                    .RequiresPasswordChange);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => employeeService.GetEmployeesAsync());
                Assert.Single(await catalogService.GetTablesAsync());
                Assert.Single(await catalogService.GetCategoriesAsync());
                Assert.Single(await catalogService.GetProductsAsync());
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => catalogService.CreateTableAsync("Bàn 2", 2));
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => catalogService.SetTableActiveAsync(1, false));
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => catalogService.CreateCategoryAsync("Không được phép"));

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
                session.SignIn(signedInStaff);
                await catalogService.SetProductAvailabilityAsync(product.Id, false);
                Assert.False(Assert.Single(await catalogService.GetProductsAsync()).IsAvailable);
                await catalogService.SetProductAvailabilityAsync(product.Id, true);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => catalogService.SetProductActiveAsync(product.Id, false));

                session.SignIn(admin);
                var promotionStartsAt = DateTimeOffset.UtcNow.AddMinutes(-1);
                var promotionEndsAt = DateTimeOffset.UtcNow.AddHours(1);
                await catalogService.CreatePromotionAsync(
                    "Giảm 10%",
                    10,
                    promotionStartsAt,
                    promotionEndsAt,
                    null,
                    category.Id);
                await catalogService.CreatePromotionAsync(
                    "Ưu đãi món đặc biệt",
                    20,
                    promotionStartsAt,
                    promotionEndsAt,
                    product.Id,
                    null);
                Assert.Equal(2, (await catalogService.GetPromotionsAsync()).Count);

                var orderService = new RestaurantOrderService(dbContext, session);
                session.SignIn(signedInStaff);
                var orderId = await orderService.StartOrderAsync(table.Id);
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => orderService.StartOrderAsync(table.Id));
                session.SignIn(admin);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => orderService.GetOpenOrderAsync(table.Id));
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => orderService.AddItemAsync(orderId, product.Id, 1, null));
                session.SignIn(signedInStaff);
                await catalogService.SetProductAvailabilityAsync(product.Id, false);
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => orderService.AddItemAsync(orderId, product.Id, 1, null));
                await catalogService.SetProductAvailabilityAsync(product.Id, true);
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => orderService.CompletePaymentAsync(orderId, PaymentMethod.Cash));
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                    () => orderService.AddItemAsync(orderId, product.Id, 0, null));
                await orderService.AddItemAsync(orderId, product.Id, 1, null);
                var openOrderSummary = Assert.IsType<OpenOrderSummary>(
                    await orderService.GetOpenOrderAsync(table.Id));
                Assert.Equal(65_000m, openOrderSummary.Subtotal);
                Assert.Equal("Ưu đãi món đặc biệt", openOrderSummary.PromotionName);
                Assert.Equal(13_000m, openOrderSummary.DiscountAmount);
                Assert.Equal(52_000m, openOrderSummary.Total);
                var orderDetailSummary = Assert.Single(openOrderSummary.Details);
                Assert.Equal("Phở bò đặc biệt", orderDetailSummary.ProductName);
                Assert.Equal(65_000m, orderDetailSummary.UnitPrice);
                var order = await dbContext.RestaurantOrders.SingleAsync(
                    candidate => candidate.Id == orderId);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => catalogService.GetPromotionsAsync());
                session.SignIn(admin);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => orderService.CompletePaymentAsync(order.Id, PaymentMethod.Cash));

                var duplicateOpenOrder = new RestaurantOrder
                {
                    RestaurantTableId = table.Id,
                    CreatedByEmployeeId = staff.EmployeeId,
                    CreatedByEmployeeNameSnapshot = staff.FullName,
                    CreatedByUserId = staffAccountId,
                    Status = OrderStatus.Open
                };
                dbContext.RestaurantOrders.Add(duplicateOpenOrder);
                await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
                dbContext.Entry(duplicateOpenOrder).State = EntityState.Detached;

                Assert.True(Assert.Single(await catalogService.GetTablesAsync()).IsOccupied);
                Assert.Equal(65_000m, await dbContext.OrderDetails
                    .Where(detail => detail.RestaurantOrderId == order.Id)
                    .Select(detail => detail.UnitPriceSnapshot)
                    .SingleAsync());
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => catalogService.SetTableActiveAsync(table.Id, false));
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => catalogService.UpdateTableAsync(
                        table.Id,
                        "Bàn đổi vị trí",
                        6,
                        "VIP"));
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

                session.SignIn(signedInStaff);
                await orderService.CompletePaymentAsync(order.Id, PaymentMethod.Cash);
                Assert.Equal(OrderStatus.Paid, order.Status);
                Assert.Equal("Ưu đãi món đặc biệt", order.AppliedPromotionNameSnapshot);
                Assert.Equal(13_000m, order.DiscountAmountSnapshot);
                Assert.Equal(52_000m, (await dbContext.Payments
                    .SingleAsync(payment => payment.RestaurantOrderId == order.Id)).Amount);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => catalogService.SetTableActiveAsync(table.Id, false));
                session.SignIn(admin);
                await catalogService.SetTableActiveAsync(table.Id, false);
                Assert.False(Assert.Single(await catalogService.GetTablesAsync()).IsActive);
                session.SignIn(signedInStaff);
                Assert.Empty(await catalogService.GetTablesAsync());
                session.SignIn(admin);
                await catalogService.SetTableActiveAsync(table.Id, true);
                session.SignIn(signedInStaff);
                Assert.True(Assert.Single(await catalogService.GetTablesAsync()).IsActive);
                var cancelledOrderId = await orderService.StartOrderAsync(table.Id);
                await orderService.CancelOrderAsync(cancelledOrderId, "Khách đổi ý");
                var cancelledOrder = await dbContext.RestaurantOrders
                    .SingleAsync(candidate => candidate.Id == cancelledOrderId);
                Assert.Equal(OrderStatus.Cancelled, cancelledOrder.Status);
                Assert.Equal("Khách đổi ý", cancelledOrder.CancellationReason);
                Assert.False(Assert.Single(await catalogService.GetTablesAsync()).IsOccupied);
                session.SignIn(admin);

                await employeeService.SetEmployeeActiveAsync(staff.EmployeeId, false);
                var suspendedSignIn = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => authenticationService.SignInAsync(
                        "renamed-staff",
                        "Phase-C-rotated-staff-password-2026"));
                Assert.Equal("Nhân viên đang bị tạm đình chỉ.", suspendedSignIn.Message);
                await employeeService.SetEmployeeActiveAsync(staff.EmployeeId, true);

                await authenticationService.ChangePasswordAsync(
                    admin.Id,
                    password,
                    "Phase-C-new-admin-password-2026");
                Assert.Null(await authenticationService.SignInAsync("test-admin", password));
                Assert.NotNull(await authenticationService.SignInAsync(
                    "test-admin",
                    "Phase-C-new-admin-password-2026"));
                await employeeService.CreateEmployeeAsync(
                    "Hồ sơ chưa cấp tài khoản",
                    null,
                    null);
                var employeeWithoutAccount = (await employeeService.GetEmployeesAsync())
                    .Single(candidate => candidate.FullName == "Hồ sơ chưa cấp tài khoản");
                Assert.Null(employeeWithoutAccount.AccountId);
                Assert.Equal(
                    "Đã đổi mk",
                    (await employeeService.GetAccountsAsync())
                    .Single(account => account.AccountId == staffAccountId)
                    .PasswordDisplay);
                var relatedOrderId = order.Id;
                await employeeService.DeleteEmployeeProfileAsync(staff.EmployeeId);
                Assert.DoesNotContain(
                    await employeeService.GetEmployeesAsync(),
                    employee => employee.EmployeeId == staff.EmployeeId);
                var retainedOrder = await dbContext.RestaurantOrders
                    .SingleAsync(candidate => candidate.Id == relatedOrderId);
                Assert.Null(retainedOrder.CreatedByEmployeeId);
                Assert.Equal(staff.FullName, retainedOrder.CreatedByEmployeeNameSnapshot);
                Assert.Equal(staffAccountId, retainedOrder.CreatedByUserId);
                Assert.Contains(
                    await employeeService.GetAccountsAsync(),
                    account => account.AccountId == staffAccountId);
                var accountAfterProfileDeletion = (await employeeService.GetAccountsAsync())
                    .Single(account => account.AccountId == staffAccountId);
                Assert.False(accountAfterProfileDeletion.HasEmployeeProfile);
                Assert.True(accountAfterProfileDeletion.EmployeeProfileDeleted);
                Assert.Equal("Hồ sơ đã bị xóa", accountAfterProfileDeletion.EmployeeProfileStatusLabel);
                var profileDeletedSignIn = Assert.IsType<AuthenticatedUser>(
                    await authenticationService.SignInAsync(
                        "renamed-staff",
                        "Phase-C-rotated-staff-password-2026"));
                Assert.Null(profileDeletedSignIn.EmployeeId);
                Assert.False(profileDeletedSignIn.RequiresPasswordChange);
                session.SignIn(profileDeletedSignIn);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => orderService.StartOrderAsync(table.Id));
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => catalogService.SetTableActiveAsync(table.Id, false));

                session.SignIn(admin);
                await employeeService.CreateEmployeeProfileAsync(staffAccountId);
                var restoredAccount = (await employeeService.GetAccountsAsync())
                    .Single(account => account.AccountId == staffAccountId);
                Assert.False(restoredAccount.EmployeeProfileDeleted);
                Assert.Equal("Đã tạo hồ sơ", restoredAccount.EmployeeProfileStatusLabel);
                Assert.NotNull(await authenticationService.SignInAsync(
                    "renamed-staff",
                    "Phase-C-rotated-staff-password-2026"));
                session.SignOut();
                session.SignIn(admin);
                await employeeService.DeleteAccountAsync(staffAccountId);
                Assert.DoesNotContain(
                    await employeeService.GetAccountsAsync(),
                    account => account.AccountId == staffAccountId);
                var persistedDeletedAccount = await dbContext.UserAccounts
                    .SingleAsync(account => account.Id == staffAccountId);
                Assert.True(persistedDeletedAccount.IsDeleted);
                Assert.Null(persistedDeletedAccount.EncryptedTemporaryPassword);
                var deletedAccountSignIn = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => authenticationService.SignInAsync(
                        "renamed-staff",
                        "Phase-C-rotated-staff-password-2026"));
                Assert.Equal("Tài khoản không tồn tại.", deletedAccountSignIn.Message);
                var retainedEmployee = Assert.Single(
                    (await employeeService.GetEmployeesAsync())
                    .Where(employee => employee.FullName == "Trần Văn Bình"));
                Assert.Null(retainedEmployee.AccountId);
                Assert.False(retainedEmployee.IsEmployeeActive);
                Assert.True(await dbContext.RestaurantOrders.AnyAsync(
                    candidate => candidate.Id == relatedOrderId));
                Assert.True(await dbContext.OrderDetails.AnyAsync(
                    detail => detail.RestaurantOrderId == relatedOrderId));
                Assert.True(await dbContext.Payments.AnyAsync(
                    payment => payment.RestaurantOrderId == relatedOrderId));
                Assert.True(await dbContext.RestaurantOrders.AnyAsync(
                    candidate => candidate.Id == cancelledOrderId
                        && candidate.Status == OrderStatus.Cancelled));
            }

            await using var verificationContext = new RestaurantDbContext(options);
            var persistedAccount = await verificationContext.UserAccounts
                .SingleAsync(account => account.Username == "test-admin");
            Assert.Equal("test-admin", persistedAccount.Username);
            Assert.True(await verificationContext.UserAccounts
                .AnyAsync(account => account.Username == "renamed-staff" && account.IsDeleted));
        }
        finally
        {
            if (databaseReady)
            {
                await using var cleanupContext = new RestaurantDbContext(options);
                await ClearTestDataAsync(cleanupContext);
            }
        }
    }

    private static async Task ClearTestDataAsync(RestaurantDbContext dbContext)
    {
        string[] statements =
        [
            "DELETE FROM `Payments`",
            "DELETE FROM `OrderDetails`",
            "DELETE FROM `RestaurantOrders`",
            "DELETE FROM `Products`",
            "DELETE FROM `Categories`",
            "DELETE FROM `RestaurantTables`",
            "DELETE FROM `UserAccounts`",
            "DELETE FROM `Employees`"
        ];

        foreach (var statement in statements)
        {
            await dbContext.Database.ExecuteSqlRawAsync(statement);
        }
    }
}
