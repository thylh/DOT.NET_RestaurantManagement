using System.Windows;
using RestaurantManagement.Core.Validation;

namespace RestaurantManagement.WPF;

public partial class EditEmployeeContactWindow : Window
{
    public string EmployeeName => NameTextBox.Text.Trim();

    public string EmployeePhone => PhoneTextBox.Text.Trim();

    public string EmployeeEmail => EmailTextBox.Text.Trim();

    public EditEmployeeContactWindow(string fullName, string? phone, string? email)
    {
        InitializeComponent();
        NameTextBox.Text = fullName;
        PhoneTextBox.Text = phone ?? string.Empty;
        EmailTextBox.Text = email ?? string.Empty;
        Loaded += (_, _) => PhoneTextBox.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EmployeeName))
        {
            ValidationBanner.Message = "Họ tên nhân viên không được để trống.";
            return;
        }

        try
        {
            EmployeeContactValidator.NormalizePhone(EmployeePhone);
            EmployeeContactValidator.NormalizeEmail(EmployeeEmail);
        }
        catch (ArgumentException exception)
        {
            ValidationBanner.Message = exception.Message;
            return;
        }

        DialogResult = true;
    }
}
