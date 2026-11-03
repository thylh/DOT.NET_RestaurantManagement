using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Core.Entities;

namespace RestaurantManagement.Infrastructure.Data;

public class RestaurantDbContext(DbContextOptions<RestaurantDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    public DbSet<RestaurantTable> RestaurantTables => Set<RestaurantTable>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<RestaurantOrder> RestaurantOrders => Set<RestaurantOrder>();

    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();

    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employees");
            entity.Property(employee => employee.FullName).HasMaxLength(150).IsRequired();
            entity.Property(employee => employee.Phone).HasMaxLength(30);
            entity.Property(employee => employee.Email).HasMaxLength(254);
            entity.HasIndex(employee => employee.Email);
        });

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("UserAccounts");
            entity.Property(account => account.Username).HasMaxLength(50).IsRequired();
            entity.Property(account => account.FullName).HasMaxLength(150).IsRequired();
            entity.Property(account => account.Phone).HasMaxLength(30);
            entity.Property(account => account.Email).HasMaxLength(254);
            entity.Property(account => account.PasswordHash).HasMaxLength(100).IsRequired();
            entity.Property(account => account.EncryptedTemporaryPassword).HasMaxLength(2048);
            entity.Property(account => account.Role).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(account => account.Username).IsUnique();
            entity.HasIndex(account => account.EmployeeId).IsUnique();
            entity.HasOne(account => account.Employee)
                .WithOne(employee => employee.UserAccount)
                .HasForeignKey<UserAccount>(account => account.EmployeeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RestaurantTable>(entity =>
        {
            entity.ToTable("RestaurantTables", table =>
            {
                table.HasCheckConstraint("CK_RestaurantTables_TableNumber", "`TableNumber` > 0");
                table.HasCheckConstraint("CK_RestaurantTables_SeatCount", "`SeatCount` > 0");
            });
            entity.HasIndex(table => table.TableNumber).IsUnique();
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.Property(category => category.Name).HasMaxLength(100).IsRequired();
            entity.Property(category => category.Description).HasMaxLength(500);
            entity.HasIndex(category => category.Name).IsUnique();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products", table =>
                table.HasCheckConstraint("CK_Products_Price", "`Price` > 0"));
            entity.Property(product => product.Name).HasMaxLength(150).IsRequired();
            entity.Property(product => product.Description).HasMaxLength(1000);
            entity.Property(product => product.ImagePath).HasMaxLength(500);
            entity.Property(product => product.Price).HasPrecision(18, 2);
            entity.HasOne(product => product.Category)
                .WithMany(category => category.Products)
                .HasForeignKey(product => product.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RestaurantOrder>(entity =>
        {
            entity.ToTable("RestaurantOrders", table =>
                table.HasCheckConstraint(
                    "CK_RestaurantOrders_Cancellation",
                    "(`Status` <> 2 AND `CancelledAt` IS NULL AND `CancelledByUserId` IS NULL AND `CancellationReason` IS NULL) OR (`Status` = 2 AND `CancelledAt` IS NOT NULL AND `CancelledByUserId` IS NOT NULL AND `CancellationReason` IS NOT NULL)"));
            entity.Property(order => order.Status).HasConversion<int>();
            entity.Property(order => order.CancellationReason).HasMaxLength(500);
            entity.Property(order => order.CreatedByEmployeeNameSnapshot).HasMaxLength(150).IsRequired();
            entity.Property<int?>("OpenRestaurantTableId")
                .HasComputedColumnSql(
                    "CASE WHEN `Status` = 0 THEN `RestaurantTableId` ELSE NULL END",
                    stored: true);
            entity.HasIndex("OpenRestaurantTableId").IsUnique();
            entity.HasOne(order => order.RestaurantTable)
                .WithMany(table => table.Orders)
                .HasForeignKey(order => order.RestaurantTableId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(order => order.CreatedByEmployee)
                .WithMany(employee => employee.Orders)
                .HasForeignKey(order => order.CreatedByEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(order => order.CreatedByUser)
                .WithMany(account => account.CreatedOrders)
                .HasForeignKey(order => order.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(order => order.CancelledByUser)
                .WithMany(account => account.CancelledOrders)
                .HasForeignKey(order => order.CancelledByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.ToTable("OrderDetails", table =>
            {
                table.HasCheckConstraint("CK_OrderDetails_Quantity", "`Quantity` > 0");
                table.HasCheckConstraint("CK_OrderDetails_UnitPriceSnapshot", "`UnitPriceSnapshot` > 0");
            });
            entity.Property(detail => detail.ProductNameSnapshot).HasMaxLength(150).IsRequired();
            entity.Property(detail => detail.UnitPriceSnapshot).HasPrecision(18, 2);
            entity.Property(detail => detail.Notes).HasMaxLength(500);
            entity.HasOne(detail => detail.RestaurantOrder)
                .WithMany(order => order.OrderDetails)
                .HasForeignKey(detail => detail.RestaurantOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(detail => detail.Product)
                .WithMany()
                .HasForeignKey(detail => detail.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments", table =>
            {
                table.HasCheckConstraint("CK_Payments_Amount", "`Amount` > 0");
                table.HasCheckConstraint("CK_Payments_Status", "`Status` = 1");
            });
            entity.Property(payment => payment.Amount).HasPrecision(18, 2);
            entity.Property(payment => payment.Method).HasConversion<string>().HasMaxLength(30);
            entity.Property(payment => payment.Status).HasConversion<int>();
            entity.HasIndex(payment => payment.RestaurantOrderId).IsUnique();
            entity.HasOne(payment => payment.RestaurantOrder)
                .WithOne(order => order.Payment)
                .HasForeignKey<Payment>(payment => payment.RestaurantOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(payment => payment.PaidByUser)
                .WithMany(account => account.Payments)
                .HasForeignKey(payment => payment.PaidByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
