using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Infrastructure;
using RestaurantManagement.Infrastructure.Data;
using RestaurantManagement.WPF.ViewModels;

namespace RestaurantManagement.WPF
{
    public partial class App : Application
    {
        private IHost? _host;
        private AsyncServiceScope? _windowScope;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddJsonFile(
                        Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
                        optional: false,
                        reloadOnChange: true);
                    configuration.AddEnvironmentVariables();
                })
                .ConfigureServices(services =>
                {
                    services.AddScoped<MainWindowViewModel>();
                    services.AddScoped<MainWindow>();
                })
                .ConfigureServices((context, services) =>
                    services.AddInfrastructure(context.Configuration))
                .Build();

            await _host.StartAsync();
            try
            {
                await using var scope = _host.Services.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<RestaurantDbContext>();
                await dbContext.Database.MigrateAsync();
            }
            catch (Exception exception)
            {
                _host.Services.GetRequiredService<ILogger<App>>()
                    .LogCritical(exception, "Không thể cập nhật cấu trúc cơ sở dữ liệu khi khởi động.");
                MessageBox.Show(
                    "Không thể khởi tạo cơ sở dữ liệu. Hãy kiểm tra LocalDB rồi khởi động lại ứng dụng.",
                    "Lỗi khởi tạo cơ sở dữ liệu",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            _windowScope = _host.Services.CreateAsyncScope();
            _windowScope.Value.ServiceProvider.GetRequiredService<MainWindow>().Show();
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_host is not null)
            {
                await _host.StopAsync();
                if (_windowScope is not null)
                {
                    await _windowScope.Value.DisposeAsync();
                }

                _host.Dispose();
            }

            base.OnExit(e);
        }
    }
}
