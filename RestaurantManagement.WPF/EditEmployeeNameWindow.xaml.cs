using System.Windows;

namespace RestaurantManagement.WPF;

public partial class EditEmployeeNameWindow : Window
{
    public string EmployeeName => FullNameTextBox.Text.Trim();

    public EditEmployeeNameWindow(string employeeName)
    {
        InitializeComponent();
        FullNameTextBox.Text = employeeName;
        FullNameTextBox.SelectAll();
        Loaded += (_, _) => FullNameTextBox.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EmployeeName))
        {
            MessageBox.Show(
                this,
                "Họ tên không được để trống.",
                "Thiếu thông tin",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }
}
