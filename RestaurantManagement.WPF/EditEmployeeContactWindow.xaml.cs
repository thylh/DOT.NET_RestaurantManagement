using System.Windows;
using RestaurantManagement.Core.Validation;

namespace RestaurantManagement.WPF;

public partial class EditEmployeeContactWindow : Window
{
    public string EmployeePhone => PhoneTextBox.Text.Trim();

    public string EmployeeEmail => EmailTextBox.Text.Trim();

    public EditEmployeeContactWindow(string? phone, string? email)
    {
        InitializeComponent();
        PhoneTextBox.Text = phone ?? string.Empty;
        EmailTextBox.Text = email ?? string.Empty;
        Loaded += (_, _) => PhoneTextBox.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
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
