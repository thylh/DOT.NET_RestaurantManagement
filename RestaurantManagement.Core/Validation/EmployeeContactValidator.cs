using System.Net.Mail;
using System.Text.RegularExpressions;

namespace RestaurantManagement.Core.Validation;

public static class EmployeeContactValidator
{
    public static string? NormalizePhone(string? value)
    {
        var normalized = NormalizeOptionalText(value, 30, "phone");
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (!Regex.IsMatch(normalized, @"^\+?[0-9\s().-]+$"))
        {
            throw new ArgumentException(
                "Số điện thoại chỉ được chứa chữ số, khoảng trắng và các ký tự + ( ) . -.",
                "phone");
        }

        var digitCount = normalized.Count(char.IsDigit);
        if (digitCount is < 7 or > 15)
        {
            throw new ArgumentException("Số điện thoại phải có từ 7 đến 15 chữ số.", "phone");
        }

        return normalized;
    }

    public static string? NormalizeEmail(string? value)
    {
        var normalized = NormalizeOptionalText(value, 254, "email");
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        try
        {
            var address = new MailAddress(normalized);
            if (!string.Equals(address.Address, normalized, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Email không đúng định dạng.", "email");
            }
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Email không đúng định dạng.", "email", exception);
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(string? value, int maxLength, string parameterName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"Nội dung không được vượt quá {maxLength} ký tự.",
                parameterName);
        }

        return normalized;
    }
}
