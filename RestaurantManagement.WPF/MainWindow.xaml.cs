using System.Windows;
using System.ComponentModel;
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
            }
            else if (e.PropertyName == nameof(viewModel.NewPassword) && string.IsNullOrEmpty(viewModel.NewPassword))
            {
                NewPasswordBox.Clear();
            }
            else if (e.PropertyName == nameof(viewModel.AccountPassword) && string.IsNullOrEmpty(viewModel.AccountPassword))
            {
                AccountPasswordBox.Clear();
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