using System.Text;
using System.Windows;
using RestaurantManagement.WPF.ViewModels;

namespace RestaurantManagement.WPF;

public partial class ChangePasswordWindow : Window
{
    public ChangePasswordWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => CurrentPasswordBox.Focus();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            throw new InvalidOperationException("Không thể truy cập dữ liệu đổi mật khẩu.");
        }

        if (string.IsNullOrWhiteSpace(CurrentPasswordBox.Password))
        {
            MessageBox.Show(
                this,
                "Hãy nhập mật khẩu hiện tại.",
                "Thiếu thông tin",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            CurrentPasswordBox.Focus();
            return;
        }

        var newPasswordByteCount = Encoding.UTF8.GetByteCount(NewPasswordBox.Password);
        if (newPasswordByteCount is < 12 or > 72)
        {
            MessageBox.Show(
                this,
                "Mật khẩu mới phải có ít nhất 12 ký tự và không vượt quá 72 byte UTF-8.",
                "Mật khẩu không hợp lệ",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            NewPasswordBox.Focus();
            return;
        }

        viewModel.CurrentPassword = CurrentPasswordBox.Password;
        viewModel.NewPassword = NewPasswordBox.Password;
        if (viewModel.ChangePasswordCommand.CanExecute(null))
        {
            await viewModel.ChangePasswordCommand.ExecuteAsync(null);
        }

        if (!viewModel.IsAuthenticated)
        {
            DialogResult = true;
        }
    }
}
