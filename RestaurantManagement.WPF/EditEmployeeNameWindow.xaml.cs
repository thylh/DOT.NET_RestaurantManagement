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
            MessageDialogWindow.ShowMessage(
                this,
                "Thiếu thông tin",
                "Họ tên không được để trống.");
            return;
        }

        DialogResult = true;
    }
}
