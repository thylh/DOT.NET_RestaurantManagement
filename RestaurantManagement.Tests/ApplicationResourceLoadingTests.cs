using System.Windows;

namespace RestaurantManagement.Tests;

public class ApplicationResourceLoadingTests
{
    [Fact]
    public void ApplicationResourcesLoadWithoutDuplicateStyles()
    {
        Exception? loadException = null;
        var thread = new Thread(() =>
        {
            try
            {
                new RestaurantManagement.WPF.App().InitializeComponent();
                _ = new RestaurantManagement.WPF.MessageDialogWindow(
                    "Kiểm tra",
                    "Kiểm tra nạp giao diện hộp thoại.",
                    isConfirmation: true,
                    kind: RestaurantManagement.WPF.MessageDialogKind.Warning);
            }
            catch (Exception exception)
            {
                loadException = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.True(loadException is null, loadException?.ToString());
    }
}
