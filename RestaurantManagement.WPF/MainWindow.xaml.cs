using System.Windows;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RestaurantManagement.WPF.ViewModels;

namespace RestaurantManagement.WPF
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            Loaded += async (_, _) => await viewModel.InitializeAsync();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not MainWindowViewModel viewModel)
            {
                return;
            }

            if (e.PropertyName == nameof(viewModel.SetupPassword) && string.IsNullOrEmpty(viewModel.SetupPassword))
            {
                SetupPasswordBox.Clear();
            }
            else if (e.PropertyName == nameof(viewModel.LoginPassword) && string.IsNullOrEmpty(viewModel.LoginPassword))
            {
                LoginPasswordBox.Clear();
            }
            else if (e.PropertyName == nameof(viewModel.CurrentPassword) && string.IsNullOrEmpty(viewModel.CurrentPassword))
            {
                CurrentPasswordBox.Clear();
                StaffCurrentPasswordBox.Clear();
            }
            else if (e.PropertyName == nameof(viewModel.NewPassword) && string.IsNullOrEmpty(viewModel.NewPassword))
            {
                NewPasswordBox.Clear();
                StaffNewPasswordBox.Clear();
            }
            else if (e.PropertyName == nameof(viewModel.AccountPassword) && string.IsNullOrEmpty(viewModel.AccountPassword))
            {
                AccountPasswordBox.Clear();
            }
        }

        private async void DeleteTable_Click(object sender, RoutedEventArgs e) =>
            await ConfirmAndExecuteAsync(
                viewModel => viewModel.DeleteTableCommand,
                "Bạn có chắc muốn xóa bàn chưa phát sinh order?");

        private async void DeleteCategory_Click(object sender, RoutedEventArgs e) =>
            await ConfirmAndExecuteAsync(
                viewModel => viewModel.DeleteCategoryCommand,
                "Bạn có chắc muốn xóa danh mục trống?");

        private async void DeleteProduct_Click(object sender, RoutedEventArgs e) =>
            await ConfirmAndExecuteAsync(
                viewModel => viewModel.DeleteProductCommand,
                "Bạn có chắc muốn xóa món chưa phát sinh order?");

        private async Task ConfirmAndExecuteAsync(
            Func<MainWindowViewModel, IAsyncRelayCommand> getCommand,
            string message)
        {
            if (DataContext is not MainWindowViewModel viewModel
                || MessageBox.Show(
                    this,
                    message,
                    "Xác nhận xóa",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            var command = getCommand(viewModel);
            if (command.CanExecute(null))
            {
                await command.ExecuteAsync(null);
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel || sender is not System.Windows.Controls.PasswordBox passwordBox)
            {
                return;
            }

            switch (passwordBox.Tag as string)
            {
                case "setup":
                    viewModel.SetupPassword = passwordBox.Password;
                    break;
                case "login":
                    viewModel.LoginPassword = passwordBox.Password;
                    break;
                case "current":
                    viewModel.CurrentPassword = passwordBox.Password;
                    break;
                case "new":
                    viewModel.NewPassword = passwordBox.Password;
                    break;
                case "account":
                    viewModel.AccountPassword = passwordBox.Password;
                    break;
            }
        }
    }
}