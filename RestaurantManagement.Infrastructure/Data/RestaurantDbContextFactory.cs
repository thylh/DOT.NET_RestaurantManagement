using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RestaurantManagement.Infrastructure.Data;

public sealed class RestaurantDbContextFactory : IDesignTimeDbContextFactory<RestaurantDbContext>
{
    public RestaurantDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__RestaurantDatabase")
            ?? "Server=127.0.0.1;Port=3306;Database=RestaurantManagement;User ID=restaurant_app;CharSet=utf8mb4";
        var options = new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseMySQL(connectionString)
            .Options;

        return new RestaurantDbContext(options);
    }
}
