using RestaurantManagement.Core.Validation;

namespace RestaurantManagement.Tests;

public class EmployeeContactValidatorTests
{
    [Theory]
    [InlineData("+84 912-345-678", "+84 912-345-678")]
    [InlineData("  0123456789  ", "0123456789")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizePhoneAcceptsOptionalValidPhone(string? value, string? expected)
    {
        Assert.Equal(expected, EmployeeContactValidator.NormalizePhone(value));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("phone-number")]
    [InlineData("+ +84912345678")]
    public void NormalizePhoneRejectsInvalidPhone(string value)
    {
        Assert.Throws<ArgumentException>(() => EmployeeContactValidator.NormalizePhone(value));
    }

    [Theory]
    [InlineData(" person@example.com ", "person@example.com")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizeEmailAcceptsOptionalValidEmail(string? value, string? expected)
    {
        Assert.Equal(expected, EmployeeContactValidator.NormalizeEmail(value));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("Person <person@example.com>")]
    public void NormalizeEmailRejectsInvalidEmail(string value)
    {
        Assert.Throws<ArgumentException>(() => EmployeeContactValidator.NormalizeEmail(value));
    }
}
