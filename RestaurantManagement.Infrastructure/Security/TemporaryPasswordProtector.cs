using System.Security.Cryptography;
using System.Text;

namespace RestaurantManagement.Infrastructure.Security;

internal static class TemporaryPasswordProtector
{
    public static string Protect(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Mật khẩu tạm được bảo vệ bằng Windows DPAPI và chỉ có thể sử dụng trên Windows.");
        }

        var plainBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            var encryptedBytes = ProtectedData.Protect(
                plainBytes,
                optionalEntropy: null,
                DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    public static string Unprotect(string encryptedPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedPassword);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Mật khẩu tạm được bảo vệ bằng Windows DPAPI và chỉ có thể sử dụng trên Windows.");
        }

        byte[] encryptedBytes;
        try
        {
            encryptedBytes = Convert.FromBase64String(encryptedPassword);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "Không thể đọc mật khẩu tạm đã mã hóa. Hãy đặt lại mật khẩu tài khoản.",
                exception);
        }

        byte[] plainBytes;
        try
        {
            plainBytes = ProtectedData.Unprotect(
                encryptedBytes,
                optionalEntropy: null,
                DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException(
                "Không thể giải mã mật khẩu tạm trên tài khoản Windows hiện tại. Hãy đặt lại mật khẩu tài khoản.",
                exception);
        }

        try
        {
            return Encoding.UTF8.GetString(plainBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }
}
