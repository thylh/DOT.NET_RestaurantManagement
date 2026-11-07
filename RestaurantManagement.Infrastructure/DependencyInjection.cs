using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RestaurantManagement.Core.Services;
using RestaurantManagement.Infrastructure.Data;
using RestaurantManagement.Infrastructure.Services;

namespace RestaurantManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("RestaurantDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'RestaurantDatabase' is missing from application configuration.");
        }

        services.AddScoped<AuditLogSaveChangesInterceptor>();
        services.AddDbContext<RestaurantDbContext>((serviceProvider, options) =>
            options.UseMySQL(connectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<AuditLogSaveChangesInterceptor>()));
        services.AddScoped<IInitialAdminSetupService, InitialAdminSetupService>();
        services.AddScoped<ICurrentUserSession, CurrentUserSession>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IEmployeeManagementService, EmployeeManagementService>();
        services.AddScoped<IRestaurantCatalogService, RestaurantCatalogService>();
        services.AddScoped<IRestaurantOrderService, RestaurantOrderService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        return services;
    }
}
