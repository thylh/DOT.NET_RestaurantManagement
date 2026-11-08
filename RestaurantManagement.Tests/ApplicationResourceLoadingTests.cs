using System.Windows;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.Logging.Abstractions;

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
                var viewModel = new RestaurantManagement.WPF.ViewModels.MainWindowViewModel(
                    null!,
                    null!,
                    new RestaurantManagement.Infrastructure.Services.CurrentUserSession(),
                    null!,
                    null!,
                    NullLogger<RestaurantManagement.WPF.ViewModels.MainWindowViewModel>.Instance);
                _ = new RestaurantManagement.WPF.MainWindow(
                    viewModel,
                    NullLogger<RestaurantManagement.WPF.MainWindow>.Instance);
                var metric = new RestaurantManagement.WPF.Controls.AnimatedMetricTextBlock
                {
                    Value = 12_500,
                    Suffix = " đ"
                };
                Assert.Equal(
                    $"{12_500.ToString("N0", CultureInfo.CurrentCulture)} đ",
                    metric.Text);
                var chart = new RestaurantManagement.WPF.Controls.RevenueTrendChart
                {
                    ItemsSource =
                    [
                        new RestaurantManagement.Core.Models.DailyRevenueSummary(
                            DateOnly.FromDateTime(DateTime.Today),
                            12_500m),
                        new RestaurantManagement.Core.Models.DailyRevenueSummary(
                            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                            18_000m)
                    ]
                };
                var bar = new System.Windows.Controls.Border();
                RestaurantManagement.WPF.Behaviors.ReportBarAnimationBehavior.SetTargetScale(
                    bar,
                    0.65);
                var animationHost = new Window
                {
                    Width = 480,
                    Height = 360,
                    Opacity = 0,
                    ShowInTaskbar = false,
                    Content = new StackPanel
                    {
                        Children = { metric, chart, bar }
                    }
                };
                animationHost.Show();
                WaitForAnimation(TimeSpan.FromMilliseconds(1200));
                Assert.Equal(
                    $"{12_500.ToString("N0", CultureInfo.CurrentCulture)} đ",
                    metric.Text);
                Assert.Equal(0.65, Assert.IsType<ScaleTransform>(bar.RenderTransform).ScaleX, 3);
                var progressProperty = (System.Windows.DependencyProperty)typeof(
                        RestaurantManagement.WPF.Controls.RevenueTrendChart)
                    .GetField(
                        "ProgressProperty",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .GetValue(null)!;
                Assert.Equal(1d, chart.GetValue(progressProperty));
                chart.Measure(new Size(480, 220));
                chart.Arrange(new Rect(0, 0, 480, 220));
                var chartBitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                    480,
                    220,
                    96,
                    96,
                    System.Windows.Media.PixelFormats.Pbgra32);
                chartBitmap.Render(chart);
                Assert.Equal(480, chartBitmap.PixelWidth);
                animationHost.Close();
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
                var bannerBorder = Assert.IsType<System.Windows.Controls.Border>(banner.Content);
                Assert.Equal(new Thickness(1), bannerBorder.BorderThickness);
                Assert.Equal(new CornerRadius(10), bannerBorder.CornerRadius);
                Assert.False(
                    RestaurantManagement.WPF.Behaviors.OpenBottomFrameBehavior.GetIsEnabled(bannerBorder));
                Assert.Equal(Visibility.Collapsed, banner.Visibility);
                banner.Message = "Kiểm tra nạp thông báo.";
                Assert.Equal(Visibility.Visible, banner.Visibility);
                banner.UseOpenBottomFrame = true;
                Assert.Equal(new Thickness(1, 1, 1, 0), bannerBorder.BorderThickness);
                Assert.Equal(new CornerRadius(14, 14, 0, 0), bannerBorder.CornerRadius);
                Assert.True(
                    RestaurantManagement.WPF.Behaviors.OpenBottomFrameBehavior.GetIsEnabled(bannerBorder));
                banner.UseOpenBottomFrame = false;
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
                Assert.Equal(0, textBox.Padding.Top);
                Assert.Equal(0, textBox.Padding.Bottom);
                RestaurantManagement.WPF.Behaviors.TextBoxPlaceholderBehavior.SetPlaceholderText(
                    textBox,
                    "Tìm kiếm nhân viên");
                textBox.ApplyTemplate();
                textBox.UpdateLayout();
                var contentHost = (System.Windows.Controls.ScrollViewer?)textBox.Template.FindName(
                    "PART_ContentHost",
                    textBox);
                Assert.NotNull(contentHost);
                Assert.Equal(
                    System.Windows.Controls.ScrollBarVisibility.Disabled,
                    contentHost.VerticalScrollBarVisibility);
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
                Assert.Equal(
                    System.Windows.Controls.DataGridGridLinesVisibility.Vertical,
                    dataGrid.GridLinesVisibility);

                var dataGridCellStyle = (Style)app.Resources[typeof(System.Windows.Controls.DataGridCell)];
                Assert.DoesNotContain(
                    dataGridCellStyle.Setters.OfType<Setter>(),
                    setter => setter.Property == System.Windows.Controls.Border.BorderThicknessProperty);

                var dataGridFrame = new System.Windows.Controls.Border
                {
                    Style = (Style)app.Resources["DataGridFrameStyle"]
                };
                Assert.Equal(new CornerRadius(0), dataGridFrame.CornerRadius);
                Assert.Equal(new Thickness(1, 1, 1, 0), dataGridFrame.BorderThickness);

                var openBottomFrame = new System.Windows.Controls.Border
                {
                    Style = (Style)app.Resources["OpenBottomFrameStyle"]
                };
                Assert.Equal(new Thickness(1, 1, 1, 0), openBottomFrame.BorderThickness);
                Assert.Equal(new CornerRadius(14, 14, 0, 0), openBottomFrame.CornerRadius);
                Assert.True(
                    RestaurantManagement.WPF.Behaviors.OpenBottomFrameBehavior.GetIsEnabled(openBottomFrame));
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

    private static void WaitForAnimation(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = duration
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
