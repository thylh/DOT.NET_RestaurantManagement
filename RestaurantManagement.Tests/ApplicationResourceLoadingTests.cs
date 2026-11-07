using System.Windows;

namespace RestaurantManagement.Tests;

public class ApplicationResourceLoadingTests
{
    [Fact]
    public void ApplicationResourcesLoadWithoutDuplicateStyles()
    {
        Exception? loadException = null;
        var bannerVisibility = Visibility.Visible;
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
                foreach (var kind in Enum.GetValues<RestaurantManagement.WPF.MessageDialogKind>())
                {
                    _ = new RestaurantManagement.WPF.MessageDialogWindow(
                        "Kiểm tra",
                        "Kiểm tra kiểu thông báo.",
                        isConfirmation: false,
                        kind: kind);
                }

                var banner = new RestaurantManagement.WPF.NotificationBanner
                {
                    Kind = RestaurantManagement.WPF.NotificationKind.Error
                };
                Assert.Equal(Visibility.Collapsed, banner.Visibility);
                banner.Message = "Kiểm tra nạp thông báo.";
                Assert.Equal(Visibility.Visible, banner.Visibility);
                banner.Message = string.Empty;
                bannerVisibility = banner.Visibility;
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
        Assert.Equal(Visibility.Collapsed, bannerVisibility);
    }
}
