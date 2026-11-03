using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Core.Models;
using RestaurantManagement.WPF.ViewModels;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace RestaurantManagement.WPF
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _statusMessageTimer;
        private readonly DispatcherTimer _errorMessageTimer;
        private AccountSummary? _accountRowToCollapse;

        public MainWindow(MainWindowViewModel viewModel, ILogger<MainWindow> logger)
        {
            InitializeComponent();
            DataContext = viewModel;
            _statusMessageTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _errorMessageTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _statusMessageTimer.Tick += StatusMessageTimer_Tick;
            _errorMessageTimer.Tick += ErrorMessageTimer_Tick;
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            PreviewMouseDown += Application_PreviewMouseDown;
            Deactivated += MainWindow_Deactivated;
            Loaded += async (_, _) =>
            {
                LoadCoverImage(logger);
                await viewModel.InitializeAsync();
            };
            Closed += (_, _) =>
            {
                _statusMessageTimer.Stop();
                _errorMessageTimer.Stop();
                _statusMessageTimer.Tick -= StatusMessageTimer_Tick;
                _errorMessageTimer.Tick -= ErrorMessageTimer_Tick;
                PreviewMouseDown -= Application_PreviewMouseDown;
                Deactivated -= MainWindow_Deactivated;
                viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                CollapseAccountActions();
            };
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not MainWindowViewModel viewModel)
            {
                return;
            }

            if (e.PropertyName == nameof(viewModel.StatusMessage))
            {
                _statusMessageTimer.Stop();
                if (!string.IsNullOrWhiteSpace(viewModel.StatusMessage))
                {
                    _statusMessageTimer.Start();
                }
            }
            else if (e.PropertyName == nameof(viewModel.ErrorMessage))
            {
                _errorMessageTimer.Stop();
                if (!string.IsNullOrWhiteSpace(viewModel.ErrorMessage))
                {
                    _errorMessageTimer.Start();
                }
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
                RequiredCurrentPasswordBox.Clear();
            }
            else if (e.PropertyName == nameof(viewModel.NewPassword) && string.IsNullOrEmpty(viewModel.NewPassword))
            {
                RequiredNewPasswordBox.Clear();
            }
        }

        private void StatusMessageTimer_Tick(object? sender, EventArgs e)
        {
            _statusMessageTimer.Stop();
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.StatusMessage = string.Empty;
            }
        }

        private void ErrorMessageTimer_Tick(object? sender, EventArgs e)
        {
            _errorMessageTimer.Stop();
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.ErrorMessage = string.Empty;
            }
        }

        private void Application_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsInsideAccountRow(e.OriginalSource as DependencyObject))
            {
                CollapseAccountActions();
            }

            ClearNotifications();
        }

        private void AccountMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (AccountMenuButton.ContextMenu is not { } contextMenu)
            {
                return;
            }

            contextMenu.PlacementTarget = AccountMenuButton;
            contextMenu.Placement = PlacementMode.Bottom;
            contextMenu.IsOpen = true;
        }

        private void AccountChangePassword_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            viewModel.CurrentPassword = string.Empty;
            viewModel.NewPassword = string.Empty;
            viewModel.ErrorMessage = string.Empty;
            var dialog = new ChangePasswordWindow(viewModel) { Owner = this };
            dialog.ShowDialog();
        }

        private async void AccountSignOut_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel viewModel
                && viewModel.SignOutCommand.CanExecute(null))
            {
                await viewModel.SignOutCommand.ExecuteAsync(null);
            }
        }

        private void ClearNotifications()
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            _statusMessageTimer.Stop();
            _errorMessageTimer.Stop();
            viewModel.ErrorMessage = string.Empty;
            viewModel.StatusMessage = string.Empty;
        }

        private void MainWindow_Deactivated(object? sender, EventArgs e) =>
            CollapseAccountActions();

        private void AdminTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReferenceEquals(sender, e.OriginalSource))
            {
                CollapseAccountActions();
            }
        }

        private void CollapseAccountActions()
        {
            _accountRowToCollapse = null;
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.SelectedAccount = null;
            }
        }

        private void LoadCoverImage(ILogger<MainWindow> logger)
        {
            var imageDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Images");
            try
            {
                if (!Directory.Exists(imageDirectory))
                {
                    return;
                }

                var logoPath = Path.Combine(imageDirectory, "logo.png");
                if (!File.Exists(logoPath))
                {
                    return;
                }

                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(logoPath, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                SetupBrandLogo.Source = image;
                LoginBrandLogo.Source = image;
                HeaderBrandLogo.Source = image;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or NotSupportedException or FormatException)
            {
                logger.LogWarning(exception, "Không thể tải ảnh nền từ thư mục {ImageDirectory}.", imageDirectory);
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

        private async void DeleteEmployeeProfile_Click(object sender, RoutedEventArgs e) =>
            await ConfirmAndExecuteAsync(
                viewModel => viewModel.DeleteEmployeeProfileCommand,
                "Xóa hồ sơ nhân viên đã chọn? Các order và lịch sử nghiệp vụ vẫn được giữ lại. Tài khoản đăng nhập (nếu có) cũng được giữ lại.");

        private void AccountRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridRow { DataContext: AccountSummary account })
            {
                return;
            }

            _accountRowToCollapse = DataContext is MainWindowViewModel viewModel
                && ReferenceEquals(viewModel.SelectedAccount, account)
                && !IsInsideButton(e.OriginalSource as DependencyObject)
                    ? account
                    : null;
        }

        private void AccountRow_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow { DataContext: AccountSummary account }
                && ReferenceEquals(_accountRowToCollapse, account)
                && DataContext is MainWindowViewModel viewModel
                && ReferenceEquals(viewModel.SelectedAccount, account))
            {
                viewModel.SelectedAccount = null;
            }

            _accountRowToCollapse = null;
        }

        private bool IsInsideAccountRow(DependencyObject? source)
        {
            while (source is not null)
            {
                if (source is DataGridRow)
                {
                    return true;
                }

                if (ReferenceEquals(source, AccountDataGrid))
                {
                    return false;
                }

                source = System.Windows.Media.VisualTreeHelper.GetParent(source)
                    ?? LogicalTreeHelper.GetParent(source);
            }

            return false;
        }

        private static bool IsInsideButton(DependencyObject? source)
        {
            while (source is not null)
            {
                if (source is Button)
                {
                    return true;
                }

                source = System.Windows.Media.VisualTreeHelper.GetParent(source)
                    ?? LogicalTreeHelper.GetParent(source);
            }

            return false;
        }

        private void CopyTemporaryPassword_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: AccountSummary { TemporaryPassword: { } password } })
            {
                return;
            }

            try
            {
                Clipboard.SetText(password);
                if (DataContext is MainWindowViewModel viewModel)
                {
                    viewModel.StatusMessage = "Đã sao chép mật khẩu tạm.";
                }
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                if (DataContext is MainWindowViewModel viewModel)
                {
                    viewModel.ErrorMessage = "Không thể sao chép mật khẩu. Hãy thử lại.";
                }
            }
        }

        private async void CreateEmployeeProfile_Click(object sender, RoutedEventArgs e)
        {
            CollapseAccountActions();
            if (!TrySelectAccount(sender, out var viewModel, out _)
                || viewModel is null)
            {
                return;
            }

            if (viewModel.CreateEmployeeProfileCommand.CanExecute(null))
            {
                await viewModel.CreateEmployeeProfileCommand.ExecuteAsync(null);
            }

            viewModel.SelectedAccount = null;
        }

        private async void EditAccountEmployeeName_Click(object sender, RoutedEventArgs e)
        {
            CollapseAccountActions();
            if (!TrySelectAccount(sender, out var viewModel, out var account)
                || viewModel is null
                || account is null)
            {
                return;
            }

            var dialog = new EditEmployeeNameWindow(account.FullName) { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            viewModel.SelectedAccount = account;
            viewModel.EditAccountEmployeeName = dialog.EmployeeName;
            if (viewModel.UpdateAccountEmployeeNameCommand.CanExecute(null))
            {
                await viewModel.UpdateAccountEmployeeNameCommand.ExecuteAsync(null);
            }

            viewModel.SelectedAccount = null;
        }

        private async void ToggleAccountStatus_Click(object sender, RoutedEventArgs e)
        {
            CollapseAccountActions();
            if (!TrySelectAccount(sender, out var viewModel, out var account)
                || viewModel is null
                || account is null)
            {
                return;
            }

            if (account.IsAccountActive
                && MessageBox.Show(
                    this,
                    $"Khóa tài khoản “{account.Username}”? Nhân viên sẽ không thể đăng nhập cho tới khi tài khoản được mở khóa.",
                    "Xác nhận khóa tài khoản",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                viewModel.SelectedAccount = null;
                return;
            }

            viewModel.SelectedAccount = account;
            if (viewModel.ToggleAccountStatusCommand.CanExecute(null))
            {
                await viewModel.ToggleAccountStatusCommand.ExecuteAsync(null);
            }

            viewModel.SelectedAccount = null;
        }

        private async void DeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            CollapseAccountActions();
            if (!TrySelectAccount(sender, out var viewModel, out var account)
                || viewModel is null
                || account is null)
            {
                return;
            }

            if (MessageBox.Show(
                    this,
                    $"Bạn có chắc muốn xóa vĩnh viễn tài khoản “{account.Username}” của {account.FullName} không? Thao tác này sẽ xóa cả hồ sơ nhân viên và lịch sử order, thanh toán liên quan; không thể hoàn tác.",
                    "Xác nhận xóa tài khoản",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                viewModel.SelectedAccount = null;
                return;
            }

            viewModel.SelectedAccount = account;
            if (viewModel.DeleteAccountCommand.CanExecute(null))
            {
                await viewModel.DeleteAccountCommand.ExecuteAsync(null);
            }

            viewModel.SelectedAccount = null;
        }

        private bool TrySelectAccount(
            object sender,
            out MainWindowViewModel? viewModel,
            out AccountSummary? account)
        {
            if (DataContext is MainWindowViewModel currentViewModel
                && sender is FrameworkElement { Tag: AccountSummary selectedAccount })
            {
                currentViewModel.SelectedAccount = selectedAccount;
                viewModel = currentViewModel;
                account = selectedAccount;
                return true;
            }

            viewModel = null;
            account = null;
            return false;
        }

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
            }
        }
    }
}