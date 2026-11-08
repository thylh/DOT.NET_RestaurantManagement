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

        var session = new CurrentUserSession();
        var options = new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseMySQL(connectionString)
            .AddInterceptors(new AuditLogSaveChangesInterceptor(session))
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

                var authenticationService = new AuthenticationService(dbContext, session);
                Assert.Null(await authenticationService.SignInAsync("test-admin", "incorrect-password"));
                Assert.Null(session.User);

                var catalogService = new RestaurantCatalogService(dbContext, session);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => catalogService.GetTablesAsync());

                var admin = await authenticationService.SignInAsync("test-admin", password);
                Assert.NotNull(admin);
                Assert.Equal(UserRole.Admin, admin.Role);

                var employeeService = new EmployeeManagementService(dbContext, session);
                var managerCredentials = await employeeService.CreateAccountAsync(
                    "Quản lý nhà hàng",
                    null,
                    "manager@example.test",
                    UserRole.Manager);
                await employeeService.CreateEmployeeProfileAsync(managerCredentials.AccountId);
                var auditService = new AuditLogService(dbContext, session);
                var accountAudit = Assert.Single(
                    await auditService.GetAuditLogsAsync(new AuditLogFilter(
                        AuditLogCategory.Account,
                        AuditLogAction.Created,
                        managerCredentials.Username,
                        null,
                        null)),
                    log => log.Action == AuditLogAction.Created);
                Assert.Equal(managerCredentials.Username, accountAudit.TargetName);
                Assert.DoesNotContain("PasswordHash", accountAudit.ChangesJson ?? string.Empty);
                Assert.DoesNotContain("EncryptedTemporaryPassword", accountAudit.ChangesJson ?? string.Empty);
                Assert.DoesNotContain(
                    managerCredentials.TemporaryPassword,
                    accountAudit.ChangesJson ?? string.Empty);
                var manager = await authenticationService.SignInAsync(
                    managerCredentials.Username,
                    managerCredentials.TemporaryPassword);
                Assert.NotNull(manager);
                Assert.Equal(UserRole.Manager, manager.Role);
                var managerEmployeeId = Assert.IsType<int>(manager.EmployeeId);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => auditService.GetAuditLogsAsync(new AuditLogFilter(
                        null,
                        null,
                        null,
                        null,
                        null)));

                session.SignIn(admin);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => catalogService.CreateTableAsync("Bàn 1", 4, "Sảnh chính"));
                session.SignIn(manager);
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

                session.SignIn(admin);
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

                var adminEmployeeList = await employeeService.GetEmployeesAsync();
                Assert.Contains(
                    adminEmployeeList,
                    employee => employee.EmployeeId == managerEmployeeId);
                var accountList = await employeeService.GetAccountsAsync();
                Assert.Contains(accountList, account => account.Role == UserRole.Manager);
                Assert.Contains(accountList, account => account.Role == UserRole.Staff);
                var accountBeforeProfile = accountList.Single(account =>
                    account.AccountId == staffCredentials.AccountId);
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
                session.SignIn(manager);
                var employees = await employeeService.GetEmployeesAsync();
                Assert.DoesNotContain(
                    employees,
                    employee => employee.EmployeeId == manager.EmployeeId);
                var staff = employees.Single(employee =>
                    employee.AccountId == staffCredentials.AccountId);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => employeeService.UpdateEmployeeAsync(
                        staff.EmployeeId,
                        staff.FullName,
                        staff.Phone,
                        staff.Email));
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => employeeService.DeleteEmployeeProfileAsync(staff.EmployeeId));
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => employeeService.SetEmployeeActiveAsync(managerEmployeeId, false));
                Assert.Equal("Nguyễn Văn An", staff.FullName);
                Assert.Equal(UserRole.Staff, staff.Role);
                Assert.Equal(staffCredentials.AccountId, staff.AccountId);
                Assert.Equal("an.nguyen", staffCredentials.Username);
                Assert.Equal(staffCredentials.Username, staff.Username);
                Assert.Equal(20, staffCredentials.TemporaryPassword.Length);
                Assert.True(staff.RequiresPasswordChange);
                session.SignIn(admin);
                await employeeService.UpdateAccountEmployeeNameAsync(
                    staffCredentials.AccountId,
                    "Nguyễn An");
                session.SignIn(manager);
                Assert.Equal(
                    "Nguyễn An",
                    (await employeeService.GetEmployeesAsync())
                    .Single(employee => employee.AccountId == staffCredentials.AccountId).FullName);
                session.SignIn(admin);
                Assert.Equal(
                    "Nguyễn An",
                    (await employeeService.GetAccountsAsync())
                    .Single(account => account.AccountId == staffCredentials.AccountId).FullName);
                await employeeService.UpdateAccountEmployeeNameAsync(
                    staffCredentials.AccountId,
                    "Trần Văn Bình");
                var renamedAccount = (await employeeService.GetAccountsAsync())
                    .Single(account => account.AccountId == staffCredentials.AccountId);
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

                session.SignIn(manager);
                await catalogService.SetProductActiveAsync(product.Id, false);
                session.SignIn(signedInStaff);
                Assert.Empty(await catalogService.GetProductsAsync());
                session.SignIn(manager);
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

                session.SignIn(manager);
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
                Assert.True(orderDetailSummary.Id > 0);
                Assert.Equal("Phở bò đặc biệt", orderDetailSummary.ProductName);
                Assert.Equal(65_000m, orderDetailSummary.UnitPrice);
                var order = await dbContext.RestaurantOrders.SingleAsync(
                    candidate => candidate.Id == orderId);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => catalogService.GetPromotionsAsync());
                session.SignIn(admin);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => orderService.CompletePaymentAsync(order.Id, PaymentMethod.Cash));
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => orderService.UpdateItemAsync(orderId, orderDetailSummary.Id, 2, null));

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
                var auditCountAfterFailedSave = await dbContext.AuditLogs.CountAsync();
                await dbContext.SaveChangesAsync();
                Assert.Equal(auditCountAfterFailedSave, await dbContext.AuditLogs.CountAsync());

                session.SignIn(signedInStaff);
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                    () => orderService.UpdateItemAsync(orderId, orderDetailSummary.Id, 0, null));
                await Assert.ThrowsAsync<ArgumentException>(
                    () => orderService.UpdateItemAsync(
                        orderId,
                        orderDetailSummary.Id,
                        1,
                        new string('x', 501)));
                await orderService.UpdateItemAsync(
                    orderId,
                    orderDetailSummary.Id,
                    2,
                    "  Ít cay  ");
                var updatedOrderSummary = Assert.IsType<OpenOrderSummary>(
                    await orderService.GetOpenOrderAsync(table.Id));
                var updatedDetail = Assert.Single(updatedOrderSummary.Details);
                Assert.Equal(2, updatedDetail.Quantity);
                Assert.Equal("Ít cay", updatedDetail.Notes);
                Assert.Equal(130_000m, updatedOrderSummary.Subtotal);
                Assert.Equal(104_000m, updatedOrderSummary.Total);

                await orderService.UpdateItemAsync(orderId, orderDetailSummary.Id, 1, null);
                await orderService.RemoveItemAsync(orderId, orderDetailSummary.Id);
                Assert.Empty(Assert.IsType<OpenOrderSummary>(
                    await orderService.GetOpenOrderAsync(table.Id)).Details);
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => orderService.UpdateItemAsync(orderId, orderDetailSummary.Id, 1, null));
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => orderService.RemoveItemAsync(orderId, orderDetailSummary.Id));
                await orderService.AddItemAsync(orderId, product.Id, 1, null);
                var openOrderAfterReadding = Assert.IsType<OpenOrderSummary>(
                    await orderService.GetOpenOrderAsync(table.Id));
                var detailIdBeforePayment = Assert.Single(openOrderAfterReadding.Details).Id;

                Assert.True(Assert.Single(await catalogService.GetTablesAsync()).IsOccupied);
                Assert.Equal(65_000m, await dbContext.OrderDetails
                    .Where(detail => detail.RestaurantOrderId == order.Id)
                    .Select(detail => detail.UnitPriceSnapshot)
                    .SingleAsync());
                session.SignIn(manager);
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
                session.SignIn(manager);
                await catalogService.SetCategoryActiveAsync(category.Id, true);

                session.SignIn(signedInStaff);
                await orderService.CompletePaymentAsync(order.Id, PaymentMethod.Cash);
                Assert.Equal(OrderStatus.Paid, order.Status);
                Assert.Equal("Ưu đãi món đặc biệt", order.AppliedPromotionNameSnapshot);
                Assert.Equal(13_000m, order.DiscountAmountSnapshot);
                Assert.Equal(52_000m, (await dbContext.Payments
                    .SingleAsync(payment => payment.RestaurantOrderId == order.Id)).Amount);
                var orderHistory = await orderService.GetOrderHistoryAsync(order.Id);
                var historyEntry = Assert.Single(orderHistory);
                Assert.Equal(OrderStatus.Paid, historyEntry.Status);
                Assert.Equal(65_000m, historyEntry.Subtotal);
                Assert.Equal(13_000m, historyEntry.DiscountAmount);
                Assert.Equal(52_000m, historyEntry.Total);
                var receipt = Assert.IsType<OrderReceiptSummary>(
                    await orderService.GetOrderReceiptAsync(order.Id));
                Assert.Equal("Bàn 1", receipt.TableNumber);
                Assert.Equal("Ưu đãi món đặc biệt", receipt.PromotionName);
                Assert.Equal(PaymentMethod.Cash, receipt.PaymentMethod);
                Assert.Equal("Phở bò đặc biệt", Assert.Single(receipt.Details).ProductName);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => orderService.GetSalesReportAsync(
                        DateTimeOffset.UtcNow.AddDays(-1),
                        DateTimeOffset.UtcNow.AddDays(1)));
                session.SignIn(admin);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => orderService.GetSalesReportAsync(
                        DateTimeOffset.UtcNow.AddDays(-1),
                        DateTimeOffset.UtcNow.AddDays(1)));
                session.SignIn(manager);
                var salesReport = await orderService.GetSalesReportAsync(
                    DateTimeOffset.UtcNow.AddDays(-1),
                    DateTimeOffset.UtcNow.AddDays(1));
                Assert.Equal(1, salesReport.PaidOrderCount);
                Assert.Equal(65_000m, salesReport.GrossRevenue);
                Assert.Equal(13_000m, salesReport.DiscountTotal);
                Assert.Equal(52_000m, salesReport.NetRevenue);
                Assert.Equal(52_000m, Assert.Single(salesReport.PaymentMethods).TotalAmount);
                Assert.Equal("Phở bò đặc biệt",
                    Assert.Single(salesReport.TopSellingProducts).ProductName);
                var paidAt = await dbContext.Payments
                    .Where(payment => payment.RestaurantOrderId == order.Id)
                    .Select(payment => payment.PaidAt)
                    .SingleAsync();
                var paidDate = DateOnly.FromDateTime(paidAt.ToLocalTime().Date);
                Assert.Equal(
                    52_000m,
                    Assert.Single(salesReport.DailyRevenue.Where(day => day.Date == paidDate)).NetRevenue);
                Assert.Contains(salesReport.DailyRevenue, day => day.NetRevenue == 0);
                session.SignIn(signedInStaff);
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => orderService.UpdateItemAsync(order.Id, detailIdBeforePayment, 2, null));
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => orderService.RemoveItemAsync(order.Id, detailIdBeforePayment));
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => catalogService.SetTableActiveAsync(table.Id, false));
                session.SignIn(manager);
                await catalogService.SetTableActiveAsync(table.Id, false);
                Assert.False(Assert.Single(await catalogService.GetTablesAsync()).IsActive);
                session.SignIn(signedInStaff);
                Assert.Empty(await catalogService.GetTablesAsync());
                session.SignIn(manager);
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
                var staffOrderHistory = await orderService.GetOrderHistoryAsync();
                Assert.Single(staffOrderHistory, history => history.Status == OrderStatus.Paid);
                Assert.DoesNotContain(
                    staffOrderHistory,
                    history => history.Status is OrderStatus.Open or OrderStatus.Cancelled);
                Assert.Null(await orderService.GetOrderReceiptAsync(cancelledOrderId));
                session.SignIn(admin);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => orderService.GetOrderHistoryAsync());
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => orderService.GetOrderReceiptAsync(cancelledOrderId));
                session.SignIn(manager);
                var managerOrderHistory = await orderService.GetOrderHistoryAsync();
                Assert.Contains(managerOrderHistory, history => history.Status == OrderStatus.Paid);
                Assert.Contains(managerOrderHistory, history => history.Status == OrderStatus.Cancelled);
                Assert.Equal(
                    OrderStatus.Cancelled,
                    Assert.IsType<OrderReceiptSummary>(
                        await orderService.GetOrderReceiptAsync(cancelledOrderId)).Status);

                await employeeService.SetEmployeeActiveAsync(staff.EmployeeId, false);
                var suspendedSignIn = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => authenticationService.SignInAsync(
                        "renamed-staff",
                        "Phase-C-rotated-staff-password-2026"));
                Assert.Equal("Nhân viên đang bị tạm đình chỉ.", suspendedSignIn.Message);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => employeeService.SetEmployeeActiveAsync(staff.EmployeeId, true));
                session.SignIn(admin);
                var adminEmployees = await employeeService.GetEmployeesAsync();
                Assert.Contains(adminEmployees, employee => employee.EmployeeId == managerEmployeeId);
                await employeeService.SetEmployeeActiveAsync(staff.EmployeeId, true);

                session.SignIn(admin);
                await authenticationService.ChangePasswordAsync(
                    admin.Id,
                    password,
                    "Phase-C-new-admin-password-2026");
                Assert.Null(await authenticationService.SignInAsync("test-admin", password));
                Assert.NotNull(await authenticationService.SignInAsync(
                    "test-admin",
                    "Phase-C-new-admin-password-2026"));
                session.SignIn(admin);
                Assert.Equal(
                    "Đã đổi mật khẩu",
                    (await employeeService.GetAccountsAsync())
                    .Single(account => account.AccountId == staffAccountId)
                    .PasswordDisplay);
                var relatedOrderId = order.Id;
                session.SignIn(admin);
                await employeeService.DeleteEmployeeProfileAsync(staff.EmployeeId);
                session.SignIn(manager);
                Assert.DoesNotContain(
                    await employeeService.GetEmployeesAsync(),
                    employee => employee.EmployeeId == staff.EmployeeId);
                var retainedOrder = await dbContext.RestaurantOrders
                    .SingleAsync(candidate => candidate.Id == relatedOrderId);
                Assert.Null(retainedOrder.CreatedByEmployeeId);
                Assert.Equal(renamedAccount.FullName, retainedOrder.CreatedByEmployeeNameSnapshot);
                Assert.Equal(staffAccountId, retainedOrder.CreatedByUserId);
                session.SignIn(admin);
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

                var notificationService = new NotificationService(dbContext, session);
                var staffUserId = Assert.IsType<int>(staffAccountId);
                session.SignIn(admin);
                var recipients = await notificationService.GetRecipientsAsync();
                Assert.Contains(recipients, recipient => recipient.UserId == staffUserId);
                await notificationService.SendAsync(
                    "Kiểm tra kho",
                    "Vui lòng kiểm tra lại số lượng hàng.",
                    NotificationPriority.Urgent,
                    staffUserId,
                    null);
                session.SignIn(new AuthenticatedUser(
                    staffUserId,
                    "renamed-staff",
                    UserRole.Staff,
                    staff.EmployeeId,
                    staff.FullName));
                var staffInbox = await notificationService.GetInboxAsync();
                var manualNotification = Assert.Single(
                    staffInbox,
                    notification => notification.Kind == NotificationKind.Manual);
                Assert.True(manualNotification.IsUrgent);
                Assert.False(manualNotification.IsRead);
                await notificationService.MarkReadAsync(manualNotification.Id);

                await notificationService.RequestPasswordResetAsync("renamed-staff");
                session.SignIn(admin);
                var resetRequest = Assert.Single(
                    await notificationService.GetInboxAsync(),
                    notification => notification.Kind == NotificationKind.PasswordResetRequest
                        && !notification.IsResolved);
                session.SignIn(manager);
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => notificationService.IssueTemporaryPasswordAsync(resetRequest.Id));
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => notificationService.SendAsync(
                        "Không hợp lệ",
                        "Quản lý không được gửi thông báo tới vai trò Quản lý.",
                        NotificationPriority.Normal,
                        null,
                        UserRole.Manager));
                session.SignIn(admin);
                var resetCredentials = await notificationService.IssueTemporaryPasswordAsync(resetRequest.Id);
                Assert.Equal("renamed-staff", resetCredentials.Username);
                var resetAccount = await dbContext.UserAccounts.SingleAsync(account => account.Id == staffUserId);
                Assert.True(resetAccount.RequiresPasswordChange);
                Assert.True(global::BCrypt.Net.BCrypt.Verify(
                    resetCredentials.TemporaryPassword,
                    resetAccount.PasswordHash));
                Assert.Null(await authenticationService.SignInAsync(
                    resetCredentials.Username,
                    "Phase-C-rotated-staff-password-2026"));
                var resetAudits = await auditService.GetAuditLogsAsync(new AuditLogFilter(
                    AuditLogCategory.Account,
                    AuditLogAction.PasswordChanged,
                    resetCredentials.Username,
                    null,
                    null));
                Assert.Contains(resetAudits, audit => audit.ActorUsername == admin.Username);
                Assert.DoesNotContain(
                    resetAudits.Select(audit => audit.ChangesJson ?? string.Empty),
                    changes => changes.Contains(resetCredentials.TemporaryPassword, StringComparison.Ordinal));

                session.SignIn(new AuthenticatedUser(
                    staffUserId,
                    "renamed-staff",
                    UserRole.Staff,
                    staff.EmployeeId,
                    staff.FullName,
                    RequiresPasswordChange: true));
                var passwordIssuedNotification = Assert.Single(
                    await notificationService.GetInboxAsync(),
                    notification => notification.Kind == NotificationKind.PasswordResetIssued);
                Assert.Contains("liên hệ Admin", passwordIssuedNotification.Message);

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
                session.SignIn(manager);
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
            "DELETE FROM `Notifications`",
            "DELETE FROM `AuditLogs`",
            "DELETE FROM `Payments`",
            "DELETE FROM `OrderDetails`",
            "DELETE FROM `RestaurantOrders`",
            "DELETE FROM `Promotions`",
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
