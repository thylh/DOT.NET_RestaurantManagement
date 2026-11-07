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
                var app = new RestaurantManagement.WPF.App();
                app.InitializeComponent();
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

                var passwordBox = new System.Windows.Controls.PasswordBox();
                RestaurantManagement.WPF.Behaviors.PasswordBoxPlaceholderBehavior.SetPlaceholderText(
                    passwordBox,
                    "Nhập mật khẩu");
                Assert.False(
                    RestaurantManagement.WPF.Behaviors.PasswordBoxPlaceholderBehavior.GetHasPassword(passwordBox));
                passwordBox.Password = "test";
                Assert.True(
                    RestaurantManagement.WPF.Behaviors.PasswordBoxPlaceholderBehavior.GetHasPassword(passwordBox));
                passwordBox.Clear();
                Assert.False(
                    RestaurantManagement.WPF.Behaviors.PasswordBoxPlaceholderBehavior.GetHasPassword(passwordBox));

                var textBox = new System.Windows.Controls.TextBox();
                textBox.Style = (Style)app.Resources[typeof(System.Windows.Controls.TextBox)];
                RestaurantManagement.WPF.Behaviors.TextBoxPlaceholderBehavior.SetPlaceholderText(
                    textBox,
                    "Tìm kiếm nhân viên");
                textBox.ApplyTemplate();
                textBox.UpdateLayout();
                var placeholder = (System.Windows.Controls.TextBlock?)textBox.Template.FindName(
                    "PlaceholderText",
                    textBox);
                Assert.NotNull(placeholder);
                Assert.Equal("Tìm kiếm nhân viên", placeholder.Text);
                Assert.Equal(Visibility.Visible, placeholder.Visibility);
                textBox.Text = "Nguyễn An";
                Assert.Equal(Visibility.Collapsed, placeholder.Visibility);
                textBox.Clear();
                Assert.Equal(Visibility.Visible, placeholder.Visibility);

                passwordBox.Style = (Style)app.Resources[typeof(System.Windows.Controls.PasswordBox)];
                passwordBox.ApplyTemplate();
                var passwordPlaceholder = (System.Windows.Controls.TextBlock?)passwordBox.Template.FindName(
                    "PasswordPlaceholderText",
                    passwordBox);
                Assert.NotNull(passwordPlaceholder);
                Assert.Equal("Nhập mật khẩu", passwordPlaceholder.Text);
                Assert.Equal(Visibility.Visible, passwordPlaceholder.Visibility);
                passwordBox.Password = "secret";
                Assert.Equal(Visibility.Collapsed, passwordPlaceholder.Visibility);
                passwordBox.Clear();
                Assert.Equal(Visibility.Visible, passwordPlaceholder.Visibility);

                var comboBox = new System.Windows.Controls.ComboBox();
                comboBox.Style = (Style)app.Resources[typeof(System.Windows.Controls.ComboBox)];
                RestaurantManagement.WPF.Behaviors.ComboBoxPlaceholderBehavior.SetPlaceholderText(
                    comboBox,
                    "Chọn danh mục");
                comboBox.ApplyTemplate();
                var comboPlaceholder = (System.Windows.Controls.TextBlock?)comboBox.Template.FindName(
                    "ComboBoxPlaceholderText",
                    comboBox);
                Assert.NotNull(comboPlaceholder);
                Assert.Equal("Chọn danh mục", comboPlaceholder.Text);
                Assert.Equal(Visibility.Visible, comboPlaceholder.Visibility);

                var scrollViewer = new System.Windows.Controls.ScrollViewer
                {
                    Style = (Style)app.Resources[typeof(System.Windows.Controls.ScrollViewer)]
                };
                Assert.Equal(
                    System.Windows.Controls.ScrollBarVisibility.Hidden,
                    scrollViewer.HorizontalScrollBarVisibility);
                Assert.Equal(
                    System.Windows.Controls.ScrollBarVisibility.Hidden,
                    scrollViewer.VerticalScrollBarVisibility);

                var dataGrid = new System.Windows.Controls.DataGrid
                {
                    Style = (Style)app.Resources[typeof(System.Windows.Controls.DataGrid)]
                };
                Assert.Equal(
                    System.Windows.Controls.ScrollBarVisibility.Hidden,
                    System.Windows.Controls.ScrollViewer.GetHorizontalScrollBarVisibility(dataGrid));
                Assert.Equal(
                    System.Windows.Controls.ScrollBarVisibility.Hidden,
                    System.Windows.Controls.ScrollViewer.GetVerticalScrollBarVisibility(dataGrid));
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
