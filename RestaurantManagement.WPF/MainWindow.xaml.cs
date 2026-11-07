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
        private EmployeeAccountSummary? _employeeRowToCollapse;

        public MainWindow(MainWindowViewModel viewModel, ILogger<MainWindow> logger)
        {
            InitializeComponent();
            var workArea = SystemParameters.WorkArea;
            Width = Math.Min(workArea.Width, Math.Max(MinWidth, workArea.Width * 0.8 * 1.1));
            Height = Math.Min(workArea.Height, Math.Max(MinHeight, workArea.Height * 0.97 * 1.1));
            LoadCoverImage(logger);
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
                CollapseAllRowActions();
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

            if (e.PropertyName is nameof(viewModel.SelectedOrderTab)
                or nameof(viewModel.SelectedOrderTable))
            {
                CenterSelectedOrderTable(viewModel);
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

        private void CenterSelectedOrderTable(MainWindowViewModel viewModel)
        {
            if (!viewModel.IsStaffOrderScreen || viewModel.SelectedOrderTable is not { } selectedTable)
            {
                return;
            }

            var tableId = selectedTable.Id;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (DataContext is not MainWindowViewModel currentViewModel
                    || currentViewModel.SelectedOrderTable?.Id != tableId
                    || !OrderTablesDataGrid.IsLoaded)
                {
                    return;
                }

                var selectedIndex = OrderTablesDataGrid.Items.IndexOf(currentViewModel.SelectedOrderTable);
                if (selectedIndex < 0)
                {
                    return;
                }

                OrderTablesDataGrid.ScrollIntoView(currentViewModel.SelectedOrderTable);
                OrderTablesDataGrid.UpdateLayout();
                var scrollViewer = FindVisualChildren<ScrollViewer>(OrderTablesDataGrid).FirstOrDefault();
                if (scrollViewer is null)
                {
                    return;
                }

                var centeredOffset = selectedIndex - scrollViewer.ViewportHeight / 2;
                scrollViewer.ScrollToVerticalOffset(Math.Max(0, centeredOffset));
            }));
        }

        private async void StateSwitch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton
                {
                    Tag: IAsyncRelayCommand command
                } toggle)
            {
                return;
            }

            try
            {
                if (command.CanExecute(null))
                {
                    await command.ExecuteAsync(null);
                }
            }
            finally
            {
                toggle.GetBindingExpression(ToggleButton.IsCheckedProperty)?.UpdateTarget();
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
            var source = e.OriginalSource as DependencyObject;
            if (!IsInsideAccountRow(source))
            {
                CollapseAccountActions();
            }

            if (!IsInsideEmployeeRow(source))
            {
                CollapseEmployeeActions();
            }

            ClearNotifications();
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
            where T : DependencyObject
        {
            if (parent is not System.Windows.Media.Visual
                && parent is not System.Windows.Media.Media3D.Visual3D)
            {
                yield break;
            }

            var childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (var index = 0; index < childCount; index++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (var descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        private static DependencyObject? GetParent(DependencyObject element)
        {
            if (element is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D)
            {
                return System.Windows.Media.VisualTreeHelper.GetParent(element);
            }

            if (element is ContentElement contentElement)
            {
                return ContentOperations.GetParent(contentElement)
                    ?? (contentElement as FrameworkContentElement)?.Parent;
            }

            return LogicalTreeHelper.GetParent(element);
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
            CollapseAllRowActions();

        private void AdminTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReferenceEquals(sender, e.OriginalSource))
            {
                CollapseAllRowActions();
            }
        }

        private void CollapseAllRowActions()
        {
            CollapseAccountActions();
            CollapseEmployeeActions();
        }

        private void CollapseAccountActions()
        {
            _accountRowToCollapse = null;
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.SelectedAccount = null;
            }
        }

        private void CollapseEmployeeActions()
        {
            _employeeRowToCollapse = null;
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.SelectedEmployee = null;
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

                var iconPath = Path.Combine(imageDirectory, "icon.png");
                if (File.Exists(iconPath))
                {
                    var windowIcon = new BitmapImage();
                    windowIcon.BeginInit();
                    windowIcon.CacheOption = BitmapCacheOption.OnLoad;
                    windowIcon.DecodePixelWidth = 740;
                    windowIcon.DecodePixelHeight = 740;
                    windowIcon.UriSource = new Uri(iconPath, UriKind.Absolute);
                    windowIcon.EndInit();
                    windowIcon.Freeze();
                    Icon = windowIcon;
                }
                else
                {
                    logger.LogWarning("Không tìm thấy biểu tượng ứng dụng tại {IconPath}.", iconPath);
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
                logger.LogWarning(exception, "Không thể tải hình ảnh ứng dụng từ thư mục {ImageDirectory}.", imageDirectory);
            }
        }

        private async void DeleteTable_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel
                || viewModel.SelectedTable is not { } table)
            {
                return;
            }

            var message =
                $"Xóa bàn “{table.TableNumber}” thuộc khu vực “{table.Area}”? "
                + "Bàn đã phát sinh order sẽ không thể xóa.";
            if (!MessageDialogWindow.ShowConfirmation(this, "Xác nhận xóa bàn", message))
            {
                return;
            }

            if (viewModel.DeleteTableCommand.CanExecute(null))
            {
                await viewModel.DeleteTableCommand.ExecuteAsync(null);
            }
        }

        private async void CancelOpenOrder_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel
                || viewModel.CurrentOpenOrder is null)
            {
                return;
            }

            var reason = MessageDialogWindow.ShowInput(
                    this,
                    "Xác nhận hủy order",
                    "Order vẫn được giữ trong lịch sử và bàn sẽ trở lại trạng thái trống. Hãy nhập lý do hủy.",
                    "Nhập lý do hủy order");
            if (reason is null)
            {
                return;
            }

            viewModel.OrderCancellationReason = reason;
            if (viewModel.CancelOpenOrderCommand.CanExecute(null))
            {
                await viewModel.CancelOpenOrderCommand.ExecuteAsync(null);
            }
        }

        private async void DeleteCategory_Click(object sender, RoutedEventArgs e) =>
            await ConfirmAndExecuteAsync(
                viewModel => viewModel.DeleteCategoryCommand,
                "Bạn có chắc muốn xóa danh mục trống?");

        private async void DeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            if (viewModel.SelectedProduct is not { } product)
            {
                viewModel.ErrorMessage = "Hãy chọn món trong danh sách trước khi xóa.";
                return;
            }

            if (!MessageDialogWindow.ShowConfirmation(
                    this,
                    "Xác nhận xóa món",
                    $"Xóa món “{product.Name}”? Món đã phát sinh order sẽ không thể xóa; hãy ngừng kinh doanh món đó thay thế."))
            {
                return;
            }

            if (viewModel.DeleteProductCommand.CanExecute(null))
            {
                await viewModel.DeleteProductCommand.ExecuteAsync(null);
            }
        }

        private void EmployeeRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridRow { DataContext: EmployeeAccountSummary employee })
            {
                return;
            }

            _employeeRowToCollapse = DataContext is MainWindowViewModel viewModel
                && ReferenceEquals(viewModel.SelectedEmployee, employee)
                && !IsInsideButton(e.OriginalSource as DependencyObject)
                    ? employee
                    : null;
        }

        private void EmployeeRow_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow { DataContext: EmployeeAccountSummary employee }
                && ReferenceEquals(_employeeRowToCollapse, employee)
                && DataContext is MainWindowViewModel viewModel
                && ReferenceEquals(viewModel.SelectedEmployee, employee))
            {
                viewModel.SelectedEmployee = null;
            }

            _employeeRowToCollapse = null;
        }

        private async void EditEmployeeContact_Click(object sender, RoutedEventArgs e)
        {
            if (!TrySelectEmployee(sender, out var viewModel, out var employee)
                || viewModel is null
                || employee is null)
            {
                return;
            }

            var dialog = new EditEmployeeContactWindow(employee.Phone, employee.Email) { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            viewModel.SelectedEmployee = employee;
            viewModel.EmployeePhone = dialog.EmployeePhone;
            viewModel.EmployeeEmail = dialog.EmployeeEmail;
            if (viewModel.UpdateEmployeeCommand.CanExecute(null))
            {
                await viewModel.UpdateEmployeeCommand.ExecuteAsync(null);
            }

            viewModel.SelectedEmployee = null;
        }

        private async void ToggleEmployeeStatus_Click(object sender, RoutedEventArgs e)
        {
            if (!TrySelectEmployee(sender, out var viewModel, out var employee)
                || viewModel is null
                || employee is null)
            {
                RefreshSwitchState(sender);
                return;
            }

            if (employee.IsEmployeeActive
                && !MessageDialogWindow.ShowConfirmation(
                    this,
                    "Xác nhận tạm đình chỉ",
                    $"Tạm đình chỉ nhân viên “{employee.FullName}”? Nhân viên sẽ không thể đăng nhập cho tới khi được bỏ đình chỉ."))
            {
                viewModel.SelectedEmployee = null;
                RefreshSwitchState(sender);
                return;
            }

            viewModel.SelectedEmployee = employee;
            if (viewModel.ToggleEmployeeStatusCommand.CanExecute(null))
            {
                await viewModel.ToggleEmployeeStatusCommand.ExecuteAsync(null);
            }

            viewModel.SelectedEmployee = null;
            RefreshSwitchState(sender);
        }

        private static void RefreshSwitchState(object sender)
        {
            if (sender is ToggleButton toggle)
            {
                toggle.GetBindingExpression(ToggleButton.IsCheckedProperty)?.UpdateTarget();
            }
        }

        private async void DeleteEmployeeProfile_Click(object sender, RoutedEventArgs e)
        {
            if (!TrySelectEmployee(sender, out var viewModel, out var employee)
                || viewModel is null
                || employee is null)
            {
                return;
            }

            if (!MessageDialogWindow.ShowConfirmation(
                    this,
                    "Xác nhận xóa hồ sơ",
                    $"Xóa hồ sơ nhân viên “{employee.FullName}”? Lịch sử order và tài khoản đăng nhập vẫn được giữ lại. Có thể tạo lại hồ sơ từ mục Cấp tài khoản."))
            {
                viewModel.SelectedEmployee = null;
                return;
            }

            viewModel.SelectedEmployee = employee;
            if (viewModel.DeleteEmployeeProfileCommand.CanExecute(null))
            {
                await viewModel.DeleteEmployeeProfileCommand.ExecuteAsync(null);
            }

            viewModel.SelectedEmployee = null;
        }


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

        private bool IsInsideEmployeeRow(DependencyObject? source)
        {
            while (source is not null)
            {
                if (source is DataGridRow)
                {
                    return true;
                }

                if (ReferenceEquals(source, EmployeeDataGrid))
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
                if (source is ButtonBase)
                {
                    return true;
                }

                source = GetParent(source);
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

        private async void DeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            CollapseAccountActions();
            if (!TrySelectAccount(sender, out var viewModel, out var account)
                || viewModel is null
                || account is null)
            {
                return;
            }

            if (!MessageDialogWindow.ShowConfirmation(
                    this,
                    "Xác nhận xóa tài khoản",
                    $"Bạn có chắc muốn thu hồi vĩnh viễn quyền đăng nhập của tài khoản “{account.Username}” ({account.FullName}) không? Hồ sơ nhân viên và lịch sử order/thanh toán sẽ được giữ lại. Không thể khôi phục quyền đăng nhập."))
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

        private bool TrySelectEmployee(
            object sender,
            out MainWindowViewModel? viewModel,
            out EmployeeAccountSummary? employee)
        {
            if (DataContext is MainWindowViewModel currentViewModel
                && sender is FrameworkElement { Tag: EmployeeAccountSummary selectedEmployee })
            {
                currentViewModel.SelectedEmployee = selectedEmployee;
                viewModel = currentViewModel;
                employee = selectedEmployee;
                return true;
            }

            viewModel = null;
            employee = null;
            return false;
        }

        private async Task ConfirmAndExecuteAsync(
            Func<MainWindowViewModel, IAsyncRelayCommand> getCommand,
            string message)
        {
            if (DataContext is not MainWindowViewModel viewModel
                || !MessageDialogWindow.ShowConfirmation(this, "Xác nhận xóa", message))
            {
                return;
            }

            var command = getCommand(viewModel);
            if (command.CanExecute(null))
            {
                await command.ExecuteAsync(null);
            }
        }

        private async void CompleteCashPayment_Click(object sender, RoutedEventArgs e) =>
            await CompletePaymentAsync(isCash: true);

        private async void CompleteBankTransferPayment_Click(object sender, RoutedEventArgs e) =>
            await CompletePaymentAsync(isCash: false);

        private async Task CompletePaymentAsync(bool isCash)
        {
            if (DataContext is not MainWindowViewModel viewModel
                || viewModel.CurrentOpenOrder is not { } order)
            {
                return;
            }

            var paymentMethod = isCash ? "tiền mặt" : "chuyển khoản";
            if (!MessageDialogWindow.ShowConfirmation(
                    this,
                    "Xác nhận thanh toán",
                    $"Xác nhận thanh toán {order.Total:N0} đ bằng {paymentMethod}? "
                    + "Sau khi xác nhận, order sẽ được hoàn tất và bàn trở lại trạng thái trống.",
                    "Xác nhận thanh toán"))
            {
                return;
            }

            var command = isCash
                ? viewModel.CompleteCashPaymentCommand
                : viewModel.CompleteBankTransferPaymentCommand;
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